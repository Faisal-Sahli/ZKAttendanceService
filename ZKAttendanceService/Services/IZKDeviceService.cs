using ZKAttendanceService.Models;

namespace ZKAttendanceService.Services
{
    public interface IZKDeviceService
    {
        Task<bool> ConnectAsync(string ip, int port);
        Task<bool> DisconnectAsync();
        Task<List<AttendanceLog>> GetAttendanceLogsAsync(int deviceId, int branchId);
        Task<bool> IsConnectedAsync();
        Task<DeviceStatus> GetDeviceStatusAsync(int deviceId, int branchId);
        Task<int> GetDeviceLogCountAsync();
    }
}
