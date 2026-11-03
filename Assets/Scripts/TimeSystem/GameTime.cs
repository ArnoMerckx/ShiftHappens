using System;

public readonly struct GameTime
{
    public const long MillisecondsPerSecond = 1000;
    public const long MillisecondsPerMinute = 60000;
    public const long MillisecondsPerDay = 86400000;

    public long TotalMilliseconds { get; }

    public int DayIndex => (int)(TotalMilliseconds / MillisecondsPerDay);

    public int MinuteOfDay => (int)((TotalMilliseconds % MillisecondsPerDay) / MillisecondsPerMinute);

    public int SecondOfMinute => (int)((TotalMilliseconds % MillisecondsPerMinute) / MillisecondsPerSecond);

    public int MillisecondOfSecond => (int)(TotalMilliseconds % MillisecondsPerSecond);

    public GameTime(long totalMilliseconds)
    {
        if (totalMilliseconds < 0 || totalMilliseconds / MillisecondsPerDay > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(totalMilliseconds));
        }

        TotalMilliseconds = totalMilliseconds;
    }

    public GameTime(int dayIndex, int minuteOfDay, int secondOfMinute = 0, int millisecondOfSecond = 0)
    {
        if (dayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(dayIndex));

        if (minuteOfDay < 0 || minuteOfDay >= 1440)
            throw new ArgumentOutOfRangeException(nameof(minuteOfDay));

        if (secondOfMinute < 0 || secondOfMinute >= 60)
            throw new ArgumentOutOfRangeException(nameof(secondOfMinute));

        if (millisecondOfSecond < 0 || millisecondOfSecond >= 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(millisecondOfSecond));
        }

        TotalMilliseconds = (long)dayIndex * MillisecondsPerDay + minuteOfDay * MillisecondsPerMinute + secondOfMinute * MillisecondsPerSecond + millisecondOfSecond;
    }
}