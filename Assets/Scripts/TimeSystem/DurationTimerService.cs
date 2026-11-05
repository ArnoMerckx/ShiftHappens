using System;

public sealed class DurationTimerService
{
    private readonly EventScheduler scheduler;
    private readonly WorkdayController workday;
    private readonly DurationTimer timer;

    private readonly string eventPrefix = $"timer:{Guid.NewGuid():N}";

    private long registrationNumber;
    private string pendingEventId;

    public DurationTimerState State => timer.State;
    public long DurationMilliseconds => timer.DurationMilliseconds;
    public long RemainingMilliseconds => timer.RemainingMilliseconds;

    public event Action Completed;

    public DurationTimerService(EventScheduler scheduler, WorkdayController workday)
    {
        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));

        if (workday == null)
            throw new ArgumentNullException(nameof(workday));

        this.scheduler = scheduler;
        this.workday = workday;
        timer = new DurationTimer(scheduler);

        workday.StateChanged += OnWorkdayStateChanged;
    }

    public bool TryStart(int durationSeconds)
    {
        if (durationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));

        if (!workday.CanAcceptWork)
            return false;

        if (!timer.TryStart(durationSeconds))
            return false;

        ScheduleExpiry();
        return true;
    }

    public bool TryPause()
    {
        if (!workday.CanAcceptWork || !timer.TryPause())
            return false;

        CancelExpiry();
        return true;
    }

    public bool TryResume()
    {
        if (!workday.CanAcceptWork || !timer.TryResume())
            return false;

        ScheduleExpiry();
        return true;
    }

    public bool TryReset()
    {
        if (!workday.CanAcceptWork)
            return false;

        CancelExpiry();
        timer.Reset();
        return true;
    }

    private void ScheduleExpiry()
    {
        string eventId =$"{eventPrefix}:{++registrationNumber}";

        scheduler.Schedule(new ScheduledEvent(eventId, timer.ExpiresAt, ScheduledEventPriority.TimerExpiry, () => Complete(eventId)));

        pendingEventId = eventId;
    }

    private void CancelExpiry()
    {
        if (pendingEventId == null)
            return;

        scheduler.Cancel(pendingEventId);
        pendingEventId = null;
    }

    private void Complete(string eventId)
    {
        if (pendingEventId != eventId)
            return;

        pendingEventId = null;

        if (workday.State != WorkdayState.Working)
        {
            timer.Freeze();
            return;
        }

        if (timer.TryComplete())
            Completed?.Invoke();
    }

    private void OnWorkdayStateChanged()
    {
        if (workday.State == WorkdayState.Working)
            return;

        CancelExpiry();
        timer.Freeze();
    }
}