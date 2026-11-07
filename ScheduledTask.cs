using System;

namespace RADYJKO;

public class ScheduledRecording
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StationName { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public DayOfWeek Day { get; set; } = DayOfWeek.Friday;
    public TimeSpan StartTime { get; set; } = new TimeSpan(20, 0, 0);
    public int DurationMinutes { get; set; } = 60;
    public bool IsEnabled { get; set; } = true;
}
