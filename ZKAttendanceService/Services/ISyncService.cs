using ZKAttendanceService.Models;

namespace ZKAttendanceService.Services
{
    public interface ISyncService
    {
        // Method القديم
        Task SyncDeviceAsync(int deviceId, string deviceIP, int devicePort, int branchId, CancellationToken cancellationToken);

        // Method للمزامنة الموازية
        Task SyncAllDevicesParallelAsync(List<Models.Device> devices, int branchId, CancellationToken cancellationToken);
    }
}
