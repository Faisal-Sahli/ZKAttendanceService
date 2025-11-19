using ZKAttendanceService.Models;

namespace ZKAttendanceService.Services
{
    public interface IWebApiService
    {
        Task<bool> SendAttendanceToServerAsync(List<AttendanceLog> logs, int branchId, int deviceId);
    }
}
