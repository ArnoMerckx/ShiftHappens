using System;

public sealed class GameClock
{
    private const long MaximumMilliseconds = ((long)int.MaxValue + 1) * GameTime.MillisecondsPerDay - 1;

    private double fractionalMilliseconds;

    public GameTime Now { get; private set; }
    public double GameSecondsPerRealSecond { get; private set; }
    public bool IsPaused { get; private set; }

    public GameClock(GameTime startTime, double gameSecondsPerRealSecond = 20)
    {
        SetRate(gameSecondsPerRealSecond);
        Now = startTime;
    }

    public void SetRate(double gameSecondsPerRealSecond)
    {
        if (double.IsNaN(gameSecondsPerRealSecond) || double.IsInfinity(gameSecondsPerRealSecond) || gameSecondsPerRealSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameSecondsPerRealSecond));
        }

        GameSecondsPerRealSecond = gameSecondsPerRealSecond;
    }

    public void SetPaused(bool isPaused)
    {
        IsPaused = isPaused;
    }

    public GameTime ConsumeElapsedSeconds(double realSeconds)
    {
        if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(realSeconds));
        }

        if (IsPaused || realSeconds == 0)
            return Now;

        double scaledMilliseconds = realSeconds * GameSecondsPerRealSecond * GameTime.MillisecondsPerSecond + fractionalMilliseconds;

        if (double.IsInfinity(scaledMilliseconds) || scaledMilliseconds >= long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(realSeconds));
        }

        long wholeMilliseconds = (long)Math.Floor(scaledMilliseconds);

        long remainingCapacity = MaximumMilliseconds - Now.TotalMilliseconds;

        if (wholeMilliseconds > remainingCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(realSeconds));
        }

        var target = new GameTime(Now.TotalMilliseconds + wholeMilliseconds);

        fractionalMilliseconds = scaledMilliseconds - wholeMilliseconds;

        return target;
    }

    public void AdvanceTo(GameTime target)
    {
        if (target.TotalMilliseconds < Now.TotalMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        if (IsPaused
            && target.TotalMilliseconds != Now.TotalMilliseconds)
        {
            throw new InvalidOperationException("Cannot advance a paused clock.");
        }

        Now = target;
    }
}