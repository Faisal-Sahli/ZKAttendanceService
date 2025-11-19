namespace ZKAttendanceService.Configuration
{
    public class DeviceConfiguration
    {
        public List<DeviceSettings> Devices { get; set; } = new();
    }

    public class DeviceSettings
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceIP { get; set; } = string.Empty;
        public int DevicePort { get; set; } = 4370;
        public bool IsActive { get; set; } = true;
    }
}
