using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZKAttendanceService.Configuration;
using ZKAttendanceService.Data;
using ZKAttendanceService.Models;

namespace ZKAttendanceService.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly ZKAttendanceWebDbContext _context;
        private readonly ILogger<ConfigurationService> _logger;
        private readonly BranchConfiguration _branchConfig;
        private readonly DeviceConfiguration _deviceConfig;

        public ConfigurationService(
            ZKAttendanceWebDbContext context,
            ILogger<ConfigurationService> logger,
            IOptions<BranchConfiguration> branchConfig,
            IOptions<DeviceConfiguration> deviceConfig)
        {
            _context = context;
            _logger = logger;
            _branchConfig = branchConfig.Value;
            _deviceConfig = deviceConfig.Value;
        }

        // ✅ Method الجديد - يطابق JSON مع قاعدة البيانات
        public async Task<(Branch? branch, List<Device> devices)> LoadAndSyncConfigurationAsync()
        {
            try
            {
                _logger.LogInformation("🔄 بدء تحميل ومطابقة الإعدادات...");

                // ✅ 1. التحقق من الفرع في قاعدة البيانات
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.BranchCode == _branchConfig.BranchCode);

                if (branch == null)
                {
                    // ❌ الفرع غير موجود - إضافته من JSON
                    _logger.LogWarning($"⚠️ الفرع {_branchConfig.BranchCode} غير موجود في القاعدة، جاري الإضافة...");

                    branch = new Branch
                    {
                        BranchCode = _branchConfig.BranchCode,
                        BranchName = _branchConfig.BranchName,
                        City = _branchConfig.City ?? string.Empty,
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    };

                    _context.Branches.Add(branch);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"✅ تم إضافة الفرع: {branch.BranchName} (ID: {branch.BranchId})");
                }
                else
                {
                    _logger.LogInformation($"✅ الفرع موجود: {branch.BranchName} (ID: {branch.BranchId})");
                }

                // ✅ 2. التحقق من الأجهزة ومطابقتها
                var devices = await SyncDevicesAsync(branch.BranchId);

                _logger.LogInformation($"📊 الإعدادات النهائية: {devices.Count} جهاز نشط");

                return (branch, devices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تحميل ومطابقة الإعدادات");
                return (null, new List<Device>());
            }
        }

        // ✅ Method خاص - مطابقة الأجهزة
        private async Task<List<Device>> SyncDevicesAsync(int branchId)
        {
            var syncedDevices = new List<Device>();

            try
            {
                _logger.LogInformation($"🔍 مطابقة {_deviceConfig.Devices.Count} جهاز من JSON مع قاعدة البيانات...");

                foreach (var deviceJson in _deviceConfig.Devices)
                {
                    if (!deviceJson.IsActive)
                    {
                        _logger.LogInformation($"⏭️ تجاوز جهاز غير نشط: {deviceJson.DeviceName}");
                        continue;
                    }

                    // ✅ البحث عن الجهاز في قاعدة البيانات بالـ IP و Port
                    var existingDevice = await _context.Devices
                        .FirstOrDefaultAsync(d => d.DeviceIP == deviceJson.DeviceIP
                                                && d.DevicePort == deviceJson.DevicePort);

                    if (existingDevice != null)
                    {
                        // ✅ الجهاز موجود - تحديث البيانات
                        _logger.LogInformation($"✅ الجهاز موجود: {existingDevice.DeviceName} ({existingDevice.DeviceIP}:{existingDevice.DevicePort})");

                        // تحديث البيانات من JSON
                        existingDevice.DeviceName = deviceJson.DeviceName;
                        existingDevice.IsActive = deviceJson.IsActive;
                        existingDevice.BranchId = branchId; // التأكد من الفرع الصحيح
                        existingDevice.ModifiedDate = DateTime.Now;

                        _context.Devices.Update(existingDevice);
                        await _context.SaveChangesAsync();

                        syncedDevices.Add(existingDevice);
                    }
                    else
                    {
                        // ❌ الجهاز غير موجود - إضافته
                        _logger.LogWarning($"⚠️ الجهاز {deviceJson.DeviceName} غير موجود، جاري الإضافة...");

                        var newDevice = new Device
                        {
                            BranchId = branchId,
                            DeviceName = deviceJson.DeviceName,
                            DeviceIP = deviceJson.DeviceIP,
                            DevicePort = deviceJson.DevicePort,
                            IsActive = deviceJson.IsActive,
                            ConnectionStatus = "NotConnected",
                            CreatedDate = DateTime.Now
                        };

                        _context.Devices.Add(newDevice);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"✅ تم إضافة الجهاز: {newDevice.DeviceName} (ID: {newDevice.DeviceId})");

                        syncedDevices.Add(newDevice);
                    }
                }

                // ✅ 3. التحقق من الأجهزة في القاعدة غير موجودة في JSON (اختياري)
                var dbDevices = await _context.Devices
                    .Where(d => d.BranchId == branchId && d.IsActive)
                    .ToListAsync();

                foreach (var dbDevice in dbDevices)
                {
                    var existsInJson = _deviceConfig.Devices.Any(d =>
                        d.DeviceIP == dbDevice.DeviceIP &&
                        d.DevicePort == dbDevice.DevicePort);

                    if (!existsInJson)
                    {
                        _logger.LogWarning($"⚠️ الجهاز {dbDevice.DeviceName} موجود في القاعدة لكن غير موجود في JSON");

                        // إضافته للقائمة النهائية (لأنه موجود في القاعدة)
                        if (!syncedDevices.Any(d => d.DeviceId == dbDevice.DeviceId))
                        {
                            syncedDevices.Add(dbDevice);
                        }
                    }
                }

                _logger.LogInformation($"✅ تمت مطابقة {syncedDevices.Count} جهاز بنجاح");

                return syncedDevices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في مطابقة الأجهزة");
                return new List<Device>();
            }
        }

        // ✅ Methods الموجودة مسبقاً (للتوافق)
        public async Task<bool> LoadConfigurationFromDatabaseAsync(string branchCode)
        {
            try
            {
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.BranchCode == branchCode && b.IsActive);

                if (branch == null)
                {
                    _logger.LogWarning($"⚠️ الفرع {branchCode} غير موجود");
                    return false;
                }

                var devices = await _context.Devices
                    .Where(d => d.BranchId == branch.BranchId && d.IsActive)
                    .ToListAsync();

                _logger.LogInformation($"✅ تم تحميل {devices.Count} جهاز للفرع {branchCode}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تحميل الإعدادات");
                return false;
            }
        }

        public async Task<Branch?> GetBranchAsync(string branchCode)
        {
            try
            {
                return await _context.Branches
                    .FirstOrDefaultAsync(b => b.BranchCode == branchCode && b.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في سحب الفرع {branchCode}");
                return null;
            }
        }

        public async Task<List<Device>> GetBranchDevicesAsync(int branchId)
        {
            try
            {
                return await _context.Devices
                    .Where(d => d.BranchId == branchId && d.IsActive)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في سحب أجهزة الفرع {branchId}");
                return new List<Device>();
            }
        }

        public async Task<int> GetActiveDeviceCountAsync(int branchId)
        {
            try
            {
                return await _context.Devices
                    .CountAsync(d => d.BranchId == branchId && d.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في حساب عدد الأجهزة");
                return 0;
            }
        }
    }
}
