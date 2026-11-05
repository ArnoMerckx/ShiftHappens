using System;

public sealed class DurationTimer
{
    private readonly EventScheduler scheduler;

    private GameTime expiresAt;
    private long storedRemainingMilliseconds;

    public DurationTimerState State { get; private set; } = DurationTimerState.Idle;

    public long DurationMilliseconds { get; private set; }

    public long RemainingMilliseconds => State == DurationTimerState.Running ? Math.Max(0L, expiresAt.TotalMilliseconds - scheduler.Now.TotalMilliseconds) : storedRemainingMilliseconds;

    internal GameTime ExpiresAt => expiresAt;

    public DurationTimer(EventScheduler scheduler)
    {
        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));

        this.scheduler = scheduler;
    }

    public bool TryStart(int durationSeconds)
    {
        if (durationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));

        if (scheduler.IsStopped || State == DurationTimerState.Running || State == DurationTimerState.Paused)
        {
            return false;
        }

        long duration =
            (long)durationSeconds * GameTime.MillisecondsPerSecond;

        var expiry = new GameTime(scheduler.Now.TotalMilliseconds + duration);

        DurationMilliseconds = duration;
        storedRemainingMilliseconds = duration;
        expiresAt = expiry;
        State = DurationTimerState.Running;

        return true;
    }

    public bool TryPause()
    {
        if (State != DurationTimerState.Running)
            return false;

        long remaining = RemainingMilliseconds;

        if (remaining == 0)
            return false;

        storedRemainingMilliseconds = remaining;
        State = DurationTimerState.Paused;

        return true;
    }

    public bool TryResume()
    {
        if (scheduler.IsStopped || State != DurationTimerState.Paused)
            return false;

        var expiry = new GameTime(scheduler.Now.TotalMilliseconds + storedRemainingMilliseconds);

        expiresAt = expiry;
        State = DurationTimerState.Running;

        return true;
    }

    public void Reset()
    {
        expiresAt = default(GameTime);
        storedRemainingMilliseconds = DurationMilliseconds;
        State = DurationTimerState.Idle;
    }

    internal bool TryComplete()
    {
        if (scheduler.IsStopped || State != DurationTimerState.Running || RemainingMilliseconds > 0)
        {
            return false;
        }

        storedRemainingMilliseconds = 0;
        State = DurationTimerState.Completed;

        return true;
    }

    internal void Freeze()
    {
        if (State != DurationTimerState.Running)
            return;

        storedRemainingMilliseconds = RemainingMilliseconds;
        State = DurationTimerState.Paused;
    }
}