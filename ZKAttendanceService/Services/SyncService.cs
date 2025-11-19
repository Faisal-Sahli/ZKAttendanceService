using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using ZKAttendanceService.Configuration;
using ZKAttendanceService.Data;
using ZKAttendanceService.Models;

namespace ZKAttendanceService.Services
{
    /// <summary>
    /// خدمة المزامنة - تسحب البيانات من أجهزة البصمة وتحفظها في قاعدة البيانات
    /// </summary>
    public class SyncService : ISyncService
    {
        private readonly ZKAttendanceWebDbContext _context;
        private readonly IZKDeviceService _deviceService;
        private readonly ILogger<SyncService> _logger;
        private readonly SyncConfiguration _syncConfig;

        public SyncService(
            ZKAttendanceWebDbContext context,
            IZKDeviceService deviceService,
            ILogger<SyncService> logger,
            IOptions<SyncConfiguration> syncConfig)
        {
            _context = context;
            _deviceService = deviceService;
            _logger = logger;
            _syncConfig = syncConfig.Value;
        }

        /// <summary>
        /// مزامنة جهاز واحد - العملية الأساسية
        /// </summary>
        public async Task SyncDeviceAsync(int deviceId, string deviceIP, int devicePort, int branchId, CancellationToken cancellationToken)
        {
            var overallTimer = Stopwatch.StartNew();

            // ═══════════════════════════════════════════════════════════
            // 📝 إنشاء سجل المزامنة (SyncLog)
            // ═══════════════════════════════════════════════════════════
            var syncLog = new SyncLog
            {
                DeviceId = deviceId,
                BranchId = branchId,
                StartTime = DateTime.Now,
                Status = "InProgress",
                ServerName = Environment.MachineName,
                CreatedDate = DateTime.Now
            };

            List<AttendanceLog> newLogs = new();

            try
            {
                _logger.LogInformation($"▶ [{deviceId}] {deviceIP}:{devicePort}");

                _context.Database.SetCommandTimeout(600);

                // ═══════════════════════════════════════════════════════════
                // 🔌 الاتصال بالجهاز
                // ═══════════════════════════════════════════════════════════
                bool connected = await _deviceService.ConnectAsync(deviceIP, devicePort);

                if (!connected)
                {
                    syncLog.Status = "Failed";
                    syncLog.ErrorMessage = "فشل الاتصال بالجهاز";
                    syncLog.EndTime = DateTime.Now;

                    await SaveSyncLogAsync(syncLog);
                    await UpdateDeviceConnectionStatusAsync(deviceId, false, "Disconnected");
                    return;
                }

                await UpdateDeviceConnectionStatusAsync(deviceId, true, "Connected");

                // ═══════════════════════════════════════════════════════════
                // 📊 حفظ حالة الجهاز
                // ═══════════════════════════════════════════════════════════
                var deviceStatus = await _deviceService.GetDeviceStatusAsync(deviceId, branchId);
                await _context.DeviceStatuses.AddAsync(deviceStatus, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                // ═══════════════════════════════════════════════════════════
                // 📅 تحديد تاريخ البداية (Incremental Sync Logic)
                // ═══════════════════════════════════════════════════════════
                DateTime? filterDate = null;

                // ✅ البحث عن آخر مزامنة ناجحة
                var lastSyncTime = await _context.SyncLogs
                    .Where(s => s.DeviceId == deviceId && s.Status == "Success")
                    .OrderByDescending(s => s.EndTime)
                    .Select(s => s.EndTime)
                    .FirstOrDefaultAsync(cancellationToken);

                if (lastSyncTime.HasValue)
                {
                    // ✅ لو فيه مزامنة سابقة → نسحب آخر 365 يوم فقط
                    filterDate = DateTime.Now.AddDays(-_syncConfig.SyncLastNDays);
                    _logger.LogInformation($"✅ آخر مزامنة: {lastSyncTime:yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    // ✅ أول مزامنة → نسحب كل شي أو آخر 365 يوم
                    filterDate = _syncConfig.SyncAllOnFirstTime ? null : DateTime.Now.AddDays(-_syncConfig.SyncLastNDays);
                    _logger.LogInformation($"🆕 أول مزامنة");
                }

                // ═══════════════════════════════════════════════════════════
                // 📥 سحب البيانات من الجهاز
                // ═══════════════════════════════════════════════════════════
                var logs = await _deviceService.GetAttendanceLogsAsync(deviceId, branchId);
                syncLog.RecordCount = logs.Count;

                _logger.LogInformation($"📥 سحب {logs.Count:N0} سجل");

                // ═══════════════════════════════════════════════════════════
                // 🔍 فلترة حسب التاريخ (Incremental Sync)
                // ═══════════════════════════════════════════════════════════
                List<AttendanceLog> filteredLogs;

                if (filterDate.HasValue)
                {
                    filteredLogs = logs.Where(l => l.AttendanceTime >= filterDate.Value).ToList();
                    int filtered = logs.Count - filteredLogs.Count;
                    _logger.LogInformation($"📊 بعد الفلترة: {filteredLogs.Count:N0} سجل (تجاهل {filtered:N0})");
                }
                else
                {
                    filteredLogs = logs;
                    _logger.LogInformation($"📊 معالجة {logs.Count:N0} سجل");
                }

                if (filteredLogs.Count == 0)
                {
                    _logger.LogInformation($"ℹ️ لا توجد سجلات جديدة");
                    syncLog.NewRecordCount = 0;
                    syncLog.DuplicateCount = 0;
                    syncLog.Status = "Success";
                    syncLog.EndTime = DateTime.Now;
                    await SaveSyncLogAsync(syncLog);
                    await _deviceService.DisconnectAsync();
                    return;
                }

                // ═══════════════════════════════════════════════════════════
                // ⭐ فحص التكرار باستخدام UniqueHash
                // ═══════════════════════════════════════════════════════════
                _logger.LogInformation($"🔍 فحص التكرار...");
                var checkStart = DateTime.Now;

                var uniqueHashes = filteredLogs.Select(l => l.UniqueHash).ToList();

                // ✅ سحب البصمات الموجودة مسبقاً
                var existingHashes = await _context.AttendanceLogs
                    .Where(a => uniqueHashes.Contains(a.UniqueHash))
                    .Select(a => a.UniqueHash)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var existingHashesSet = new HashSet<string>(existingHashes);

                var checkDuration = (DateTime.Now - checkStart).TotalSeconds;
                _logger.LogInformation($"✅ فحص {existingHashesSet.Count:N0} في {checkDuration:F2}ث");

                // ═══════════════════════════════════════════════════════════
                // ⚡ فلترة السجلات الجديدة فقط
                // ═══════════════════════════════════════════════════════════
                _logger.LogInformation($"⚡ فلترة الجديد...");
                var filterStart = DateTime.Now;

                newLogs = filteredLogs
                    .Where(l => !existingHashesSet.Contains(l.UniqueHash))
                    .ToList();

                int newRecords = newLogs.Count;
                int duplicates = filteredLogs.Count - newRecords;

                var filterDuration = (DateTime.Now - filterStart).TotalSeconds;
                _logger.LogInformation($"✅ جديد: {newRecords:N0} | مكرر: {duplicates:N0} | {filterDuration:F2}ث");

                // ═══════════════════════════════════════════════════════════
                // 💾 حفظ السجلات الجديدة - Bulk Insert
                // ═══════════════════════════════════════════════════════════
                if (newLogs.Any())
                {
                    _logger.LogInformation($"💾 حفظ {newLogs.Count:N0} سجل...");
                    var insertStart = DateTime.Now;

                    // ✅ تحسين: زيادة Batch Size لـ 10000 (أسرع)
                    int batchSize = 10000;

                    for (int i = 0; i < newLogs.Count; i += batchSize)
                    {
                        int remainingCount = Math.Min(batchSize, newLogs.Count - i);
                        var batch = newLogs.GetRange(i, remainingCount);

                        await _context.BulkInsertAsync(batch, new BulkConfig
                        {
                            BatchSize = batchSize,
                            BulkCopyTimeout = 600,
                            SetOutputIdentity = false,
                            TrackingEntities = false
                        }, cancellationToken: cancellationToken);

                        int totalBatches = (int)Math.Ceiling((double)newLogs.Count / batchSize);
                        if (totalBatches > 1)
                            _logger.LogInformation($"   دفعة {(i / batchSize) + 1}/{totalBatches}");
                    }

                    var insertDuration = (DateTime.Now - insertStart).TotalSeconds;
                    var speed = insertDuration > 0 ? newLogs.Count / insertDuration : 0;
                    _logger.LogInformation($"🎉 حفظ: {newLogs.Count:N0} في {insertDuration:F2}ث ({speed:F0} سجل/ث)");
                }
                else
                {
                    _logger.LogInformation($"ℹ️ كل السجلات موجودة");
                }

                // ═══════════════════════════════════════════════════════════
                // ✅ تحديث سجل المزامنة
                // ═══════════════════════════════════════════════════════════
                syncLog.NewRecordCount = newRecords;
                syncLog.DuplicateCount = duplicates;
                syncLog.Status = "Success";
                syncLog.EndTime = DateTime.Now;

                overallTimer.Stop();
                _logger.LogInformation($"✅ انتهى في {overallTimer.Elapsed.TotalSeconds:F1}ث");

                await _deviceService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                syncLog.Status = "Failed";
                syncLog.ErrorMessage = ex.Message;
                syncLog.EndTime = DateTime.Now;

                _logger.LogError(ex, $"❌ خطأ: {ex.Message}");

                await UpdateDeviceConnectionStatusAsync(deviceId, false, "Error");

                try { await _deviceService.DisconnectAsync(); }
                catch { }
            }
            finally
            {
                await SaveSyncLogAsync(syncLog);
            }
        }

        /// <summary>
        /// حفظ سجل المزامنة (SyncLog) في قاعدة البيانات
        /// </summary>
        private async Task SaveSyncLogAsync(SyncLog syncLog)
        {
            try
            {
                syncLog.SyncId = 0;  // Identity سيولد القيمة تلقائياً

                await _context.SyncLogs.AddAsync(syncLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "فشل حفظ SyncLog");
            }
        }

        /// <summary>
        /// تحديث حالة اتصال الجهاز
        /// </summary>
        private async Task UpdateDeviceConnectionStatusAsync(int deviceId, bool isConnected, string status)
        {
            try
            {
                var device = await _context.Devices.FindAsync(deviceId);
                if (device != null)
                {
                    device.LastConnectionTime = DateTime.Now;
                    device.ConnectionStatus = status;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "فشل تحديث حالة الجهاز");
            }
        }

        /// <summary>
        /// مزامنة عدة أجهزة بالتوازي (Parallel Processing)
        /// - يعالج 5 أجهزة في نفس الوقت
        /// - Retry تلقائي في حالة الفشل
        /// </summary>
        public async Task SyncAllDevicesParallelAsync(List<Models.Device> devices, int branchId, CancellationToken cancellationToken)
        {
            if (!devices.Any())
            {
                _logger.LogWarning("⚠️ لا توجد أجهزة");
                return;
            }

            var totalStart = DateTime.Now;
            _logger.LogInformation($"🚀 مزامنة {devices.Count} جهاز...");

            // ✅ Semaphore: يسمح بمعالجة 5 أجهزة في نفس الوقت فقط
            var semaphore = new SemaphoreSlim(5);
            var successCount = 0;
            var failedCount = 0;
            var lockObj = new object();

            var tasks = devices.Select(async device =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var retryCount = 0;
                    var maxRetries = _syncConfig.MaxRetryAttempts;
                    var success = false;

                    // ✅ Retry Logic: محاولة 3 مرات في حالة الفشل
                    while (retryCount < maxRetries && !success)
                    {
                        try
                        {
                            if (retryCount > 0)
                            {
                                _logger.LogWarning($"🔄 [{device.DeviceId}] محاولة {retryCount + 1}/{maxRetries}");
                                // ✅ Exponential Backoff: انتظار متزايد (2^retry ثوانٍ)
                                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
                            }

                            await SyncDeviceAsync(device.DeviceId, device.DeviceIP, device.DevicePort, branchId, cancellationToken);

                            success = true;
                            lock (lockObj) { successCount++; }
                        }
                        catch (Exception ex)
                        {
                            retryCount++;

                            if (retryCount >= maxRetries)
                            {
                                _logger.LogError(ex, $"❌ [{device.DeviceId}] فشلت كل المحاولات");
                                lock (lockObj) { failedCount++; }
                            }
                            else
                            {
                                _logger.LogWarning($"⚠️ [{device.DeviceId}] محاولة {retryCount}: {ex.Message}");
                            }
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            var totalDuration = (DateTime.Now - totalStart).TotalMinutes;
            _logger.LogInformation($"════════════════════════════════════════");
            _logger.LogInformation($"✅ اكتملت في {totalDuration:F2} دقيقة");
            _logger.LogInformation($"📊 ناجح: {successCount} | فاشل: {failedCount} | الإجمالي: {devices.Count}");
            _logger.LogInformation($"════════════════════════════════════════");
        }
    }
}
