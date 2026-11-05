using System;

public sealed class WorkdaySettings
{
    public string DayId { get; }
    public string DisplayName { get; }
    public TimeRange WorkHours { get; }
    public GameTime OpensAt { get; }
    public GameTime ClosesAt { get; }
    public double GameSecondsPerRealSecond { get; }

    public int DayIndex => WorkHours.DayIndex;

    public WorkdaySettings(string dayId, string displayName, int dayIndex, int startMinute, int endMinute, double gameSecondsPerRealSecond)
    {
        if (string.IsNullOrWhiteSpace(dayId))
            throw new ArgumentException("Day ID is required.", nameof(dayId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Day display name is required.", nameof(displayName));

        if (double.IsNaN(gameSecondsPerRealSecond) || double.IsInfinity(gameSecondsPerRealSecond) || gameSecondsPerRealSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameSecondsPerRealSecond));
        }

        WorkHours = new TimeRange(dayIndex, startMinute, endMinute);

        OpensAt = new GameTime(dayIndex, startMinute);

        ClosesAt = new GameTime(OpensAt.TotalMilliseconds + (long)(endMinute - startMinute) * GameTime.MillisecondsPerMinute);

        DayId = dayId;
        DisplayName = displayName;
        GameSecondsPerRealSecond = gameSecondsPerRealSecond;
    }
}