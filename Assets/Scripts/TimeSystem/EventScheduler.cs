using System;
using System.Collections.Generic;

public sealed class EventScheduler
{
    private readonly GameClock clock;

    private readonly List<ScheduledEvent> pending = new List<ScheduledEvent>();

    private readonly HashSet<string> usedEventIds = new HashSet<string>(StringComparer.Ordinal);

    private bool isAdvancing;

    private ScheduledEventPriority? allowedPriority;

    public int PendingCount => pending.Count;

    public GameTime Now => clock.Now;

    public bool IsStopped { get; private set; }

    public event Action<ScheduledEvent> EventDispatched;

    public EventScheduler(GameClock clock)
    {
        if (clock == null)
            throw new ArgumentNullException(nameof(clock));

        this.clock = clock;
    }

    public void Schedule(ScheduledEvent scheduledEvent)
    {
        if (scheduledEvent == null)
            throw new ArgumentNullException(nameof(scheduledEvent));

        if (IsStopped)
        {
            throw new InvalidOperationException("Cannot schedule work after the scheduler has stopped.");
        }

        if (allowedPriority.HasValue && scheduledEvent.Priority != allowedPriority.Value)
        {
            throw new InvalidOperationException("This event priority is no longer allowed.");
        }

        if (scheduledEvent.DueTime.TotalMilliseconds < clock.Now.TotalMilliseconds)
        {
            throw new ArgumentException("Cannot schedule an event in the past.", nameof(scheduledEvent));
        }

        if (usedEventIds.Contains(scheduledEvent.EventId))
        {
            throw new ArgumentException("Event ID has already been used.", nameof(scheduledEvent));
        }

        int index = 0;

        while (index < pending.Count && Compare(pending[index], scheduledEvent) <= 0)
        {
            index++;
        }

        pending.Insert(index, scheduledEvent);
        usedEventIds.Add(scheduledEvent.EventId);
    }

    public bool Cancel(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("Event ID is required.", nameof(eventId));
        }

        for (int i = 0; i < pending.Count; i++)
        {
            if (string.Equals(pending[i].EventId, eventId, StringComparison.Ordinal))
            {
                pending.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public void Stop()
    {
        if (IsStopped)
            return;

        IsStopped = true;
        pending.Clear();
    }

    public void RestrictToPriority(ScheduledEventPriority priority)
    {
        if (!Enum.IsDefined(typeof(ScheduledEventPriority), priority))
            throw new ArgumentOutOfRangeException(nameof(priority));

        if (IsStopped)
        {
            throw new InvalidOperationException("Cannot configure a stopped scheduler.");
        }

        if (allowedPriority.HasValue && allowedPriority.Value != priority)
        {
            throw new InvalidOperationException("The scheduler priority restriction cannot be changed.");
        }

        allowedPriority = priority;
        pending.RemoveAll(item => item.Priority != priority);
    }

    public ScheduledEvent PeekNext()
    {
        if (pending.Count == 0)
            return null;

        return pending[0];
    }

    private static int Compare(ScheduledEvent first, ScheduledEvent second)
    {
        int timeOrder = first.DueTime.TotalMilliseconds.CompareTo(second.DueTime.TotalMilliseconds);

        if (timeOrder != 0)
            return timeOrder;

        return first.Priority.CompareTo(second.Priority);
    }

    public void AdvanceRealSeconds(double realSeconds)
    {
        ThrowIfAdvancing();

        if (IsStopped)
            return;

        GameTime target = clock.ConsumeElapsedSeconds(realSeconds);
        AdvanceTo(target);
    }

    public void AdvanceTo(GameTime target)
    {
        ThrowIfAdvancing();

        if (target.TotalMilliseconds < clock.Now.TotalMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(target));

        if (IsStopped || clock.IsPaused)
            return;

        isAdvancing = true;

        try
        {
            while (!IsStopped && !clock.IsPaused && pending.Count > 0)
            {
                ScheduledEvent next = pending[0];

                if (next.DueTime.TotalMilliseconds > target.TotalMilliseconds)
                {
                    break;
                }

                clock.AdvanceTo(next.DueTime);
                pending.RemoveAt(0);
                next.Callback();
                EventDispatched?.Invoke(next);
            }

            if (!IsStopped && !clock.IsPaused)
                clock.AdvanceTo(target);
        }
        finally
        {
            isAdvancing = false;
        }
    }

    private void ThrowIfAdvancing()
    {
        if (isAdvancing)
        {
            throw new InvalidOperationException("Cannot advance the scheduler during event dispatch.");
        }
    }
}