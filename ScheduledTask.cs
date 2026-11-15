using System;

namespace RADYJKO;

public class ScheduledRecording
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StationName { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public DayOfWeek Day { get; set; } = DayOfWeek.Friday;
    public bool EveryDay { get; set; } = false;
    public TimeSpan StartTime { get; set; } = new TimeSpan(20, 0, 0);
    public int DurationMinutes { get; set; } = 60;
    public bool IsEnabled { get; set; } = true;

    public string DayLabel => EveryDay ? "Codziennie" : Day switch
    {
        DayOfWeek.Monday => "Poniedziałek",
        DayOfWeek.Tuesday => "Wtorek",
        DayOfWeek.Wednesday => "Środa",
        DayOfWeek.Thursday => "Czwartek",
        DayOfWeek.Friday => "Piątek",
        DayOfWeek.Saturday => "Sobota",
        DayOfWeek.Sunday => "Niedziela",
        _ => Day.ToString()
    };

    public string SummaryText => $"{StationName} • {DayLabel} {StartTime.Hours:D2}:{StartTime.Minutes:D2} ({DurationMinutes} min)";
}
