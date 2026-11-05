using System;

public sealed class ScheduledEvent
{
    public string EventId { get; }
    public GameTime DueTime { get; }
    public ScheduledEventPriority Priority { get; }
    public Action Callback { get; }

    public ScheduledEvent(string eventId, GameTime dueTime, ScheduledEventPriority priority, Action callback)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            throw new ArgumentException("Event ID is required.", nameof(eventId));

        if (!Enum.IsDefined(typeof(ScheduledEventPriority), priority))
            throw new ArgumentOutOfRangeException(nameof(priority));

        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        EventId = eventId;
        DueTime = dueTime;
        Priority = priority;
        Callback = callback;
    }
}