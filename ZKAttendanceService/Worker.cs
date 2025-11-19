using Microsoft.Extensions.Options;
using ZKAttendanceService.Configuration;
using ZKAttendanceService.Services;

namespace ZKAttendanceService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly SyncConfiguration _syncConfig;
        private readonly BranchConfiguration _branchConfig;
        private DateTime _lastRunTime = DateTime.MinValue;  // ✅ جديد

        public Worker(
            ILogger<Worker> logger,
            IServiceProvider serviceProvider,
            IOptions<SyncConfiguration> syncConfig,
            IOptions<BranchConfiguration> branchConfig)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _syncConfig = syncConfig.Value;
            _branchConfig = branchConfig.Value;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("═══════════════════════════════════════");
            _logger.LogInformation("🚀 بدء ZKAttendanceService");
            _logger.LogInformation($"🕐 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _logger.LogInformation("═══════════════════════════════════════");

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ بدأ التنفيذ");

            try
            {
                _logger.LogInformation($"📍 الفرع: {_branchConfig.BranchName}");
                _logger.LogInformation($"⏰ كل {_syncConfig.SyncIntervalMinutes} دقيقة");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في الإعدادات");
                throw;
            }

            if (!_syncConfig.EnableAutoSync)
            {
                _logger.LogWarning("⚠️ المزامنة معطلة!");
                return;
            }

            Models.Branch? branch = null;
            List<Models.Device> devices = new();

            using (var scope = _serviceProvider.CreateScope())
            {
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                (branch, devices) = await configService.LoadAndSyncConfigurationAsync();

                if (branch == null || !devices.Any())
                {
                    _logger.LogError("❌ فشل تحميل الإعدادات");
                    return;
                }

                _logger.LogInformation($"✅ الفرع: {branch.BranchName}");
                _logger.LogInformation($"✅ الأجهزة: {devices.Count}");
            }

            int cycleNumber = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                cycleNumber++;

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var peakHourService = scope.ServiceProvider.GetRequiredService<PeakHourService>();

                        // ✅ فحص: هل الوقت الحالي في فترة ذروة؟
                        if (peakHourService.IsCurrentlyPeakHour(out var currentPeakHour))
                        {
                            _logger.LogInformation("");
                            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            _logger.LogInformation($"⏸️ دورة #{cycleNumber} - تم التجاهل");
                            _logger.LogInformation($"🚫 وقت ذروة: {currentPeakHour}");
                            _logger.LogInformation($"⏰ {DateTime.Now:HH:mm:ss}");
                            _logger.LogInformation($"⏰ الانتظار {_syncConfig.SyncIntervalMinutes} دقيقة...");
                            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                            await Task.Delay(TimeSpan.FromMinutes(_syncConfig.SyncIntervalMinutes), stoppingToken);
                            continue;
                        }

                        // ✅ فحص: هل يجب سحب فوري بعد انتهاء ذروة؟
                        bool isImmediateRun = peakHourService.ShouldRunImmediatelyAfterPeakHour(_lastRunTime, out var justEndedPeakHour);

                        _logger.LogInformation("");
                        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                        if (isImmediateRun)
                        {
                            _logger.LogInformation($"⚡ دورة #{cycleNumber} - سحب فوري");
                            _logger.LogInformation($"✅ انتهت فترة الذروة: {justEndedPeakHour}");
                        }
                        else
                        {
                            _logger.LogInformation($"🔄 دورة #{cycleNumber} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        }

                        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                        (branch, devices) = await configService.LoadAndSyncConfigurationAsync();

                        if (branch == null || !devices.Any())
                        {
                            _logger.LogWarning("⚠️ لا توجد أجهزة");
                        }
                        else
                        {
                            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                            await syncService.SyncAllDevicesParallelAsync(devices, branch.BranchId, stoppingToken);
                        }

                        _lastRunTime = DateTime.Now;  // ✅ تحديث آخر وقت تشغيل

                        _logger.LogInformation($"✅ دورة #{cycleNumber} انتهت");
                        _logger.LogInformation($"⏰ الانتظار {_syncConfig.SyncIntervalMinutes} دقيقة...");
                        _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ خطأ في الدورة #{cycleNumber}");
                }

                await Task.Delay(TimeSpan.FromMinutes(_syncConfig.SyncIntervalMinutes), stoppingToken);
            }

            _logger.LogInformation("🛑 خدمة توقفت");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 إيقاف Worker...");
            await base.StopAsync(cancellationToken);
        }
    }
}
