using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ZKAttendanceService.Configuration;
using ZKAttendanceService.Models;

namespace ZKAttendanceService.Services
{
    public class WebApiService : IWebApiService
    {
        private readonly HttpClient _httpClient;
        private readonly WebApiSettings _settings;
        private readonly ILogger<WebApiService> _logger;

        public WebApiService(
            HttpClient httpClient,
            IOptions<WebApiSettings> settings,
            ILogger<WebApiService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            // ✅ تكوين Headers
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // API Key إذا موجود
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
            }
        }

        public async Task<bool> SendAttendanceToServerAsync(
            List<AttendanceLog> logs,
            int branchId,
            int deviceId)
        {
            try
            {
                _logger.LogInformation($"📤 بدء إرسال {logs.Count} سجل إلى السيرفر المركزي...");

                var payload = new
                {
                    BranchId = branchId,
                    DeviceId = deviceId,
                    AttendanceLogs = logs.Select(l => new
                    {
                        l.BiometricUserId,
                        l.AttendanceTime,
                        l.VerifyMethod,   
                        l.AttendanceType,  
                        l.WorkCode
                    }).ToList(),
                    SyncTime = DateTime.Now
                };

                var jsonContent = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // ✅ محاولة الإرسال مع Retry
                for (int attempt = 1; attempt <= _settings.RetryCount; attempt++)
                {
                    try
                    {
                        var response = await _httpClient.PostAsync(_settings.SyncEndpoint, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            _logger.LogInformation($"✅ تم إرسال {logs.Count} سجل بنجاح - {responseContent}");
                            return true;
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning(
                                $"⚠ فشل الإرسال - المحاولة {attempt}/{_settings.RetryCount} - " +
                                $"Status: {response.StatusCode} - {errorContent}");
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(
                            $"⚠ خطأ في الاتصال - المحاولة {attempt}/{_settings.RetryCount}: {ex.Message}");
                    }
                    catch (TaskCanceledException ex)
                    {
                        _logger.LogWarning(
                            $"⚠ انتهت مهلة الطلب - المحاولة {attempt}/{_settings.RetryCount}: {ex.Message}");
                    }

                    // الانتظار قبل المحاولة التالية
                    if (attempt < _settings.RetryCount)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
                    }
                }

                _logger.LogError($"❌ فشل إرسال البيانات بعد {_settings.RetryCount} محاولات");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ غير متوقع أثناء إرسال البيانات للسيرفر المركزي");
                return false;
            }
        }
    }
}
