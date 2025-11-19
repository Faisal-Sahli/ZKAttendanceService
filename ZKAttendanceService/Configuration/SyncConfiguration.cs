namespace ZKAttendanceService.Configuration
{
    public class SyncConfiguration
    {
        public bool EnableAutoSync { get; set; } = true;
        public int SyncIntervalMinutes { get; set; } = 10;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryIntervalMinutes { get; set; } = 5;
        public int StartDelaySeconds { get; set; } = 0;
        public int SyncLastNDays { get; set; } = 365;
        public bool SyncAllOnFirstTime { get; set; } = true;

        // ✅ الإعدادات الجديدة
        public List<PeakHourWindow> PeakHours { get; set; } = new();
    }

    // ✅ كلاس جديد
    public class PeakHourWindow
    {
        public string Name { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool RunImmediatelyAfter { get; set; }
    }
}
