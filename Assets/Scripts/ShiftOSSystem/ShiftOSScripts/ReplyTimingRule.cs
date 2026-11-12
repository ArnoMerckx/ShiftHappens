using System;

public sealed class ReplyTimingRules
{
    public int DelayMinutes { get; }

    public ReplyTimingRules(int delayMinutes = 10)
    {
        if (delayMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(delayMinutes));

        DelayMinutes = delayMinutes;
    }

    public GameTime GetDelayedDueAt(GameTime recordedAt, WorkdaySettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (recordedAt.TotalMilliseconds < settings.OpensAt.TotalMilliseconds || recordedAt.TotalMilliseconds > settings.ClosesAt.TotalMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(recordedAt));
        }

        long remaining = settings.ClosesAt.TotalMilliseconds - recordedAt.TotalMilliseconds;

        long delay = (long)DelayMinutes * GameTime.MillisecondsPerMinute;

        return new GameTime(recordedAt.TotalMilliseconds + Math.Min(delay, remaining));
    }

    public GameTime GetRequestDueAt(RequestOverviewViewData request, GameTime recordedAt, WorkdaySettings settings)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        GameTime delayed = GetDelayedDueAt(recordedAt, settings);

        if (request.Status != RequestStatus.Resolved && request.Status != RequestStatus.Expired)
        {
            throw new ArgumentException("A reply requires a resolved or expired request.", nameof(request));
        }

        if (request.DeliveryAt.TotalMilliseconds < settings.OpensAt.TotalMilliseconds || request.DeliveryAt.TotalMilliseconds >= settings.ClosesAt.TotalMilliseconds || recordedAt.TotalMilliseconds < request.DeliveryAt.TotalMilliseconds)
        {
            throw new ArgumentException("The request and recorded event must belong to this shift.", nameof(request));
        }

        if (!request.HelpWindow.HasValue)
            return delayed;

        TimeRange window = request.HelpWindow.Value;

        if (window.DayIndex != settings.DayIndex || window.StartMinute < settings.WorkHours.StartMinute || window.EndMinute > settings.WorkHours.EndMinute)
        {
            throw new ArgumentException("The help window must belong to this shift.", nameof(request));
        }

        long windowEnd = (long)window.DayIndex * GameTime.MillisecondsPerDay + (long)window.EndMinute * GameTime.MillisecondsPerMinute;

        long dueAt = Math.Max(recordedAt.TotalMilliseconds, Math.Min(windowEnd, settings.ClosesAt.TotalMilliseconds));

        return new GameTime(dueAt);
    }
}