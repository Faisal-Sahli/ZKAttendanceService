using ZKAttendanceService.Models;

namespace ZKAttendanceService.Services
{
    public interface IConfigurationService
    {
        // ✅ Method الجديد
        Task<(Branch? branch, List<Device> devices)> LoadAndSyncConfigurationAsync();

        // Methods الموجودة
        Task<bool> LoadConfigurationFromDatabaseAsync(string branchCode);
        Task<Branch?> GetBranchAsync(string branchCode);
        Task<List<Device>> GetBranchDevicesAsync(int branchId);
        Task<int> GetActiveDeviceCountAsync(int branchId);
    }
}
