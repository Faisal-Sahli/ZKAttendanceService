namespace ZKAttendanceService.Configuration
{
    public class BranchConfiguration
    {
        public int BranchId { get; set; }
        public string BranchCode { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string? City { get; set; }  // ✅ أضف هذا السطر
        public List<DeviceConfiguration> Devices { get; set; } = new();
    }
}
