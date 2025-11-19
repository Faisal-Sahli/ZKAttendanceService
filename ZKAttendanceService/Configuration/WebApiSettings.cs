namespace ZKAttendanceService.Configuration
{
    public class WebApiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string SyncEndpoint { get; set; } = string.Empty;
        public int Timeout { get; set; } = 120;
        public int RetryCount { get; set; } = 3;
        public string ApiKey { get; set; } = string.Empty;
    }
}
