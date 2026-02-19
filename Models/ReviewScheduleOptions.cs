namespace backend.Models;

public class ReviewScheduleOptions
{
    public const string SectionName = "ReviewSchedule";

    /// <summary>
    /// Delay in minutes before sending Message 2 (follow-up).
    /// Dev default: 2 minutes. Prod default: 1440 minutes (24 hours).
    /// </summary>
    public int Message2DelayMinutes { get; set; } = 1440;

    /// <summary>
    /// Delay in minutes before sending Message 3 (final reminder).
    /// Dev default: 5 minutes. Prod default: 4320 minutes (72 hours).
    /// </summary>
    public int Message3DelayMinutes { get; set; } = 4320;

    /// <summary>
    /// How often the background scheduler polls for pending messages (in seconds).
    /// Dev default: 30 seconds. Prod default: 3600 seconds (1 hour).
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 3600;
}
