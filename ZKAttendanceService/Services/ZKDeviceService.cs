using System.Runtime.Versioning;
using ZKAttendanceService.Models;

namespace ZKAttendanceService.Services
{
    /// <summary>
    /// خدمة الاتصال بأجهزة البصمة ZKTeco وسحب سجلات الحضور
    /// - تتصل بجهاز البصمة عبر الشبكة
    /// - تسحب جميع سجلات الحضور والانصراف
    /// - تحوّل البيانات الخام إلى كائنات AttendanceLog
    /// - تحسب UniqueHash لكل سجل لمنع التكرار
    /// </summary>
    [SupportedOSPlatform("windows")]  // يعمل فقط على Windows (SDK خاص بـ Windows)
    public class ZKDeviceService : IZKDeviceService
    {
        // ═══════════════════════════════════════════════════════════════════
        // 📌 المتغيرات الأساسية
        // ═══════════════════════════════════════════════════════════════════

        private readonly ILogger<ZKDeviceService> _logger;  // لتسجيل الأحداث والأخطاء
        private bool _isConnected = false;                   // حالة الاتصال بالجهاز
        private int _machineNumber = 1;                      // رقم الجهاز (دائماً 1 للاتصال الشبكي)
        private dynamic? _device = null;                     // كائن جهاز البصمة (COM Object)

        // ═══════════════════════════════════════════════════════════════════
        // 🏗️ Constructor - يتم استدعاؤه عند إنشاء الخدمة
        // ═══════════════════════════════════════════════════════════════════

        public ZKDeviceService(ILogger<ZKDeviceService> logger)
        {
            _logger = logger;  // استلام Logger من Dependency Injection
        }

        // ═══════════════════════════════════════════════════════════════════
        // 🔌 ConnectAsync - الاتصال بجهاز البصمة
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// يتصل بجهاز البصمة عبر IP و Port
        /// </summary>
        /// <param name="ip">عنوان IP للجهاز (مثال: 192.168.1.201)</param>
        /// <param name="port">المنفذ (عادة 4370)</param>
        /// <returns>true إذا نجح الاتصال، false إذا فشل</returns>
        public async Task<bool> ConnectAsync(string ip, int port)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // ═══ الخطوة 1: تحميل zkemkeeper.ZKEM COM Component ═══
                    // هذا المكتبة من ZKTeco للتواصل مع أجهزة البصمة
                    var zkType = Type.GetTypeFromProgID("zkemkeeper.ZKEM");
                    if (zkType == null)
                    {
                        _logger.LogError("❌ zkemkeeper غير مثبت");
                        return false;
                    }

                    // ═══ الخطوة 2: إنشاء instance من الجهاز ═══
                    _device = Activator.CreateInstance(zkType);

                    // ═══ الخطوة 3: محاولة الاتصال بالجهاز ═══
                    _isConnected = (bool)_device.Connect_Net(ip, port);

                    if (_isConnected)
                    {
                        _logger.LogInformation($"✅ اتصال ناجح: {ip}:{port}");
                        return true;
                    }
                    else
                    {
                        // ═══ في حالة فشل الاتصال: احصل على رمز الخطأ ═══
                        int errorCode = 0;
                        _device.GetLastError(ref errorCode);
                        _logger.LogError($"❌ فشل الاتصال: {ip}:{port} (خطأ: {errorCode})");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    // ═══ في حالة حدوث استثناء (مثل: SDK غير مثبت) ═══
                    _logger.LogError(ex, $"❌ استثناء في الاتصال: {ip}:{port}");
                    return false;
                }
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        // 🔌 DisconnectAsync - قطع الاتصال بالجهاز
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// يقطع الاتصال بجهاز البصمة بعد الانتهاء من العمليات
        /// </summary>
        public async Task<bool> DisconnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_isConnected && _device != null)
                    {
                        _device.Disconnect();      // قطع الاتصال
                        _isConnected = false;      // تحديث الحالة
                        _logger.LogInformation("✅ تم قطع الاتصال");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ خطأ في قطع الاتصال");
                    return false;
                }
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        // ✅ IsConnectedAsync - التحقق من حالة الاتصال
        // ═══════════════════════════════════════════════════════════════════

        public async Task<bool> IsConnectedAsync()
        {
            return await Task.FromResult(_isConnected);
        }

        // ═══════════════════════════════════════════════════════════════════
        // 📥 GetAttendanceLogsAsync - سحب جميع سجلات البصمات من الجهاز
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// **الوظيفة الأهم**: يسحب جميع سجلات الحضور من جهاز البصمة
        /// ويحوّلها إلى قائمة من كائنات AttendanceLog
        /// </summary>
        public async Task<List<AttendanceLog>> GetAttendanceLogsAsync(int deviceId, int branchId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // ═══════════════════════════════════════════════════
                    // ✅ الخطوة 1: التحقق من الاتصال
                    // ═══════════════════════════════════════════════════

                    if (_device == null || !_isConnected)
                    {
                        _logger.LogWarning("⚠️ الجهاز غير متصل");
                        return new List<AttendanceLog>();
                    }

                    // ═══════════════════════════════════════════════════
                    // 📊 الخطوة 2: الحصول على عدد السجلات في الجهاز
                    // ═══════════════════════════════════════════════════

                    int logCount = 0;
                    _device.GetDeviceStatus(_machineNumber, 6, ref logCount);  // 6 = عدد السجلات
                    _logger.LogInformation($"📊 السجلات في الجهاز: {logCount:N0}");

                    if (logCount == 0)
                    {
                        _logger.LogInformation("ℹ️ لا توجد سجلات");
                        return new List<AttendanceLog>();
                    }

                    // ═══════════════════════════════════════════════════
                    // 🔒 الخطوة 3: تعطيل الجهاز مؤقتاً أثناء السحب
                    // ═══════════════════════════════════════════════════
                    // يمنع الموظفين من التسجيل أثناء عملية السحب
                    // لضمان عدم فقدان أي سجلات

                    _device.EnableDevice(_machineNumber, false);

                    // ═══════════════════════════════════════════════════
                    // 📥 الخطوة 4: سحب البيانات من الجهاز
                    // ═══════════════════════════════════════════════════

                    var startTime = DateTime.Now;  // لحساب الوقت المستغرق

                    // محاولة 1: ReadAllGLogData (أسرع لكن قد لا يعمل مع جميع الأجهزة)
                    bool readSuccess = _device.ReadAllGLogData(_machineNumber);

                    if (!readSuccess)
                    {
                        // محاولة 2: ReadGeneralLogData (بديل إذا فشلت المحاولة الأولى)
                        int errorCode = 0;
                        _device.GetLastError(ref errorCode);
                        _logger.LogWarning($"⚠️ ReadAllGLogData فشل ({errorCode})");

                        readSuccess = _device.ReadGeneralLogData(_machineNumber);

                        if (!readSuccess)
                        {
                            _device.GetLastError(ref errorCode);
                            _logger.LogError($"❌ فشل القراءة (خطأ: {errorCode})");
                            _device.EnableDevice(_machineNumber, true);  // إعادة تفعيل الجهاز
                            return new List<AttendanceLog>();
                        }
                    }

                    // ═══════════════════════════════════════════════════
                    // 🔄 الخطوة 5: معالجة السجلات واحداً تلو الآخر
                    // ═══════════════════════════════════════════════════

                    var allLogs = new List<AttendanceLog>(logCount);  // إنشاء قائمة بالحجم المتوقع

                    // متغيرات لتخزين بيانات كل سجل
                    string userId = "";
                    int verifyMode = 0, inOutMode = 0;
                    int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                    int workCode = 0;
                    int recordCount = 0;

                    // ═══ Loop عبر جميع السجلات ═══
                    while (_device.SSR_GetGeneralLogData(_machineNumber, ref userId, ref verifyMode, ref inOutMode,
                                                            ref year, ref month, ref day, ref hour, ref minute, ref second, ref workCode))
                    {
                        recordCount++;  // عداد السجلات المعالجة

                        try
                        {
                            // ═══ معالجة userId (تنظيف وإزالة الفراغات) ═══
                            var cleanUserId = string.IsNullOrWhiteSpace(userId) ? "0" : userId.Trim();

                            // ═══ التحقق من صحة التاريخ والوقت ═══
                            // تجنب الأخطاء من بيانات فاسدة أو غير منطقية
                            if (year < 2000 || month < 1 || month > 12 || day < 1 || day > 31 ||
                                hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59)
                            {
                                _logger.LogWarning($"⚠️ سجل {recordCount}: تاريخ غير صحيح ({year}-{month}-{day} {hour}:{minute}:{second})");
                                continue;  // تجاهل هذا السجل والانتقال للتالي
                            }

                            var attendanceTime = new DateTime(year, month, day, hour, minute, second);

                            // ═══════════════════════════════════════════════════
                            // 🔑 حساب UniqueHash - المفتاح الفريد لمنع التكرار
                            // ═══════════════════════════════════════════════════
                            // التنسيق: {UserId}_{DeviceId}_{DateTime بالثانية}
                            // مثال: "1218_1_20251108092650"
                            // هذا يضمن أن كل بصمة لها معرّف فريد

                            var uniqueHash = $"{cleanUserId}_{deviceId}_{attendanceTime:yyyyMMddHHmmss}";

                            // ═══ إنشاء كائن AttendanceLog ═══
                            var log = new AttendanceLog
                            {
                                BiometricUserId = cleanUserId,              // رقم الموظف
                                AttendanceTime = attendanceTime,            // وقت البصمة
                                DeviceId = deviceId,                        // رقم الجهاز
                                BranchId = branchId,                        // رقم الفرع
                                VerifyMethod = GetVerifyMethodName(verifyMode),  // طريقة التحقق (بصمة/وجه/كرت)
                                AttendanceType = GetAttendanceTypeName(inOutMode), // نوع البصمة (دخول/خروج)
                                WorkCode = workCode,                        // كود العمل
                                UniqueHash = uniqueHash,                    // البصمة الرقمية الفريدة
                                IsSynced = true,                            // تم المزامنة
                                SyncedDate = DateTime.Now,                  // وقت المزامنة
                                IsManual = false,                           // ليست يدوية
                                CreatedDate = DateTime.Now                  // وقت الإنشاء
                            };

                            allLogs.Add(log);  // إضافة السجل للقائمة
                        }
                        catch (Exception ex)
                        {
                            // في حالة خطأ في معالجة سجل معين، نتجاهله ونكمل
                            _logger.LogWarning($"⚠️ سجل {recordCount}: {ex.Message}");
                            continue;
                        }

                        // ═══════════════════════════════════════════════════
                        // 📊 عرض التقدم كل 5000 سجل
                        // ═══════════════════════════════════════════════════

                        if (recordCount % 5000 == 0)
                        {
                            var elapsed = (DateTime.Now - startTime).TotalSeconds;
                            if (elapsed > 0)
                            {
                                var speed = recordCount / elapsed;                      // السرعة (سجل/ثانية)
                                var remaining = logCount - recordCount;                 // السجلات المتبقية
                                var estimatedRemaining = speed > 0 ? remaining / speed : 0;  // الوقت المتبقي
                                var progress = (recordCount * 100.0) / logCount;        // النسبة المئوية

                                _logger.LogInformation(
                                    $"   📥 {recordCount:N0}/{logCount:N0} ({progress:F0}%) | " +
                                    $"⚡ {speed:F0} سجل/ث | ⏱️ ~{estimatedRemaining:F0}ث متبقي"
                                );
                            }
                        }
                    }

                    // ═══════════════════════════════════════════════════
                    // ✅ الخطوة 6: الانتهاء من السحب
                    // ═══════════════════════════════════════════════════

                    var readDuration = (DateTime.Now - startTime).TotalSeconds;
                    var avgSpeed = readDuration > 0 ? recordCount / readDuration : 0;

                    _logger.LogInformation($"✅ قراءة: {recordCount:N0} سجل في {readDuration:F1}ث ({avgSpeed:F0} سجل/ث)");

                    // ═══ إعادة تفعيل الجهاز (السماح للموظفين بالتسجيل) ═══
                    _device.EnableDevice(_machineNumber, true);

                    // ═══ ترتيب السجلات حسب الوقت (الأقدم أولاً) ═══
                    return allLogs.OrderBy(l => l.AttendanceTime).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ خطأ في الجهاز {deviceId}");

                    // ═══ محاولة إعادة تفعيل الجهاز في حالة حدوث خطأ ═══
                    try
                    {
                        if (_device != null)
                            _device.EnableDevice(_machineNumber, true);
                    }
                    catch { }

                    throw;  // إعادة رمي الاستثناء لمعالجته في الطبقة الأعلى
                }
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        // 🔧 GetVerifyMethodName - تحويل رقم طريقة التحقق إلى نص
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// يحوّل رقم طريقة التحقق إلى اسم مفهوم
        /// </summary>
        private string GetVerifyMethodName(int verifyMode)
        {
            return verifyMode switch
            {
                0 => "Password",              // كلمة مرور
                1 => "Fingerprint",           // بصمة إصبع
                2 => "Card",                  // كرت
                3 => "Fingerprint+Password",  // بصمة + كلمة مرور
                4 => "Fingerprint+Card",      // بصمة + كرت
                5 => "Face",                  // التعرف على الوجه
                6 => "Face+Fingerprint",      // وجه + بصمة
                7 => "Face+Password",         // وجه + كلمة مرور
                8 => "Face+Card",             // وجه + كرت
                15 => "Palm",                 // بصمة كف اليد
                _ => $"Unknown({verifyMode})" // غير معروف
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // 🔧 GetAttendanceTypeName - تحويل رقم نوع البصمة إلى نص
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// يحوّل رقم نوع البصمة إلى اسم مفهوم
        /// </summary>
        private string GetAttendanceTypeName(int inOutMode)
        {
            return inOutMode switch
            {
                0 => "CheckIn",      // دخول
                1 => "CheckOut",     // خروج
                2 => "BreakOut",     // خروج استراحة
                3 => "BreakIn",      // عودة من الاستراحة
                4 => "OTIn",         // بداية وقت إضافي
                5 => "OTOut",        // نهاية وقت إضافي
                255 => "CheckIn",    // دخول (قيمة بديلة)
                _ => "CheckIn"       // افتراضي: دخول
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // 📊 GetDeviceStatusAsync - الحصول على حالة الجهاز
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// يحصل على معلومات حالة الجهاز (Serial Number, Firmware, إلخ)
        /// </summary>
        public async Task<DeviceStatus> GetDeviceStatusAsync(int deviceId, int branchId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_device == null || !_isConnected)
                    {
                        return new DeviceStatus
                        {
                            DeviceId = deviceId,
                            BranchId = branchId,
                            IsOnline = false,
                            StatusMessage = "غير متصل",
                            StatusTime = DateTime.Now,
                            LastUpdateTime = DateTime.Now,
                            CreatedDate = DateTime.Now
                        };
                    }

                    // ═══ الحصول على معلومات الجهاز ═══
                    string serialNumber = "";
                    string firmwareVersion = "";

                    try { _device.GetSerialNumber(_machineNumber, ref serialNumber); }
                    catch { serialNumber = "Unknown"; }

                    try { _device.GetFirmwareVersion(_machineNumber, ref firmwareVersion); }
                    catch { firmwareVersion = "Unknown"; }

                    int userCount = 0, logCount = 0, faceCount = 0;

                    try
                    {
                        _device.GetDeviceStatus(_machineNumber, 1, ref userCount);   // 1 = عدد المستخدمين
                        _device.GetDeviceStatus(_machineNumber, 6, ref logCount);    // 6 = عدد السجلات
                        _device.GetDeviceStatus(_machineNumber, 21, ref faceCount);  // 21 = عدد الوجوه
                    }
                    catch { }

                    return new DeviceStatus
                    {
                        DeviceId = deviceId,
                        BranchId = branchId,
                        IsOnline = true,
                        SerialNumber = serialNumber,
                        DeviceModel = "ZKTeco",
                        FirmwareVersion = firmwareVersion,
                        UserCount = userCount,
                        LogCount = logCount,
                        FaceCount = faceCount,
                        DeviceTime = DateTime.Now,
                        StatusMessage = "متصل",
                        StatusTime = DateTime.Now,
                        LastUpdateTime = DateTime.Now,
                        LastConnectionTime = DateTime.Now,
                        CreatedDate = DateTime.Now
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ خطأ في حالة الجهاز {deviceId}");

                    return new DeviceStatus
                    {
                        DeviceId = deviceId,
                        BranchId = branchId,
                        IsOnline = false,
                        StatusMessage = $"خطأ: {ex.Message}",
                        StatusTime = DateTime.Now,
                        LastUpdateTime = DateTime.Now,
                        CreatedDate = DateTime.Now
                    };
                }
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        // 🔢 GetDeviceLogCountAsync - الحصول على عدد السجلات فقط
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// يحصل على عدد السجلات في الجهاز بدون سحبها
        /// </summary>
        public async Task<int> GetDeviceLogCountAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!_isConnected || _device == null)
                        return 0;

                    int logCount = 0;
                    _device.GetDeviceStatus(_machineNumber, 6, ref logCount);
                    return logCount;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ خطأ في عدد السجلات");
                    return 0;
                }
            });
        }
    }
}
