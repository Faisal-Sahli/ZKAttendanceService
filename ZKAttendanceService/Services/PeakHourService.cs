using Microsoft.Extensions.Options;
using ZKAttendanceService.Configuration;

namespace ZKAttendanceService.Services
{
    public class PeakHourService
    {
        private readonly SyncConfiguration _config;
        private readonly ILogger<PeakHourService> _logger;

        public PeakHourService(
            IOptions<SyncConfiguration> config,
            ILogger<PeakHourService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public bool IsCurrentlyPeakHour(out string? peakHourName)
        {
            peakHourName = null;

            if (_config.PeakHours == null || !_config.PeakHours.Any())
                return false;

            var now = DateTime.Now;

            foreach (var peakHour in _config.PeakHours)
            {
                if (IsWithinPeakHour(now, peakHour))
                {
                    peakHourName = peakHour.Name;
                    return true;
                }
            }

            return false;
        }

        private bool IsWithinPeakHour(DateTime time, PeakHourWindow peakHour)
        {
            try
            {
                var start = ParseTime(peakHour.StartTime);
                var end = ParseTime(peakHour.EndTime);
                var currentTime = time.TimeOfDay;

                if (start <= end)
                {
                    return currentTime >= start && currentTime <= end;
                }
                else
                {
                    return currentTime >= start || currentTime <= end;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في فحص وقت الذروة: {peakHour.Name}");
                return false;
            }
        }

        private TimeSpan ParseTime(string time)
        {
            var parts = time.Split(':');
            var hours = int.Parse(parts[0]);
            var minutes = int.Parse(parts[1]);
            return new TimeSpan(hours, minutes, 0);
        }

        public bool ShouldRunImmediatelyAfterPeakHour(DateTime lastRunTime, out string? peakHourName)
        {
            peakHourName = null;

            if (_config.PeakHours == null || !_config.PeakHours.Any())
                return false;

            var now = DateTime.Now;

            foreach (var peakHour in _config.PeakHours)
            {
                if (!peakHour.RunImmediatelyAfter)
                    continue;

                var endTime = ParseTime(peakHour.EndTime);
                var peakEndDateTime = now.Date.Add(endTime);

                if (now >= peakEndDateTime &&
                    now <= peakEndDateTime.AddMinutes(10) &&
                    lastRunTime < peakEndDateTime)
                {
                    peakHourName = peakHour.Name;
                    return true;
                }
            }

            return false;
        }
    }
}
