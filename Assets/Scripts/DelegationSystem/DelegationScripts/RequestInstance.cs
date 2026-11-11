using System;

public sealed class RequestInstance
{
    private readonly DayRequestPlan plan;

    public string InstanceId => plan.InstanceId;
    public RequestData Request => plan.Request;
    public GameTime DeliveryAt => plan.DeliveryAt;
    public TimeRange? HelpWindow => plan.HelpWindow;
    public GameTime? CutoffAt => plan.CutoffAt;

    public GameTime ExpiresAt { get; }
    public RequestStatus Status { get; private set; } = RequestStatus.Open;
    public DelegationDecision Decision { get; private set; }

    public RequestInstance(DayRequestPlan plan, GameTime closesAt)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        if (closesAt.DayIndex != plan.DeliveryAt.DayIndex ||
            closesAt.TotalMilliseconds <= plan.DeliveryAt.TotalMilliseconds)
        {
            throw new ArgumentException("Closing must follow delivery on the same day.", nameof(closesAt));
        }

        if (plan.IsFixedWindow)
        {
            long windowEnd = (long)plan.HelpWindow.Value.DayIndex * GameTime.MillisecondsPerDay + plan.HelpWindow.Value.EndMinute * GameTime.MillisecondsPerMinute;

            if (windowEnd > closesAt.TotalMilliseconds || plan.CutoffAt.Value.TotalMilliseconds >= closesAt.TotalMilliseconds)
            {
                throw new ArgumentException("The help window must end by closing and its cutoff must precede closing.", nameof(closesAt));
            }
        }

        this.plan = plan;

        ExpiresAt = plan.IsFixedWindow ? new GameTime(plan.CutoffAt.Value.TotalMilliseconds + 1) : closesAt;
    }

    public bool TryResolve(DelegationDecision decision)
    {
        if (decision == null)
            throw new ArgumentNullException(nameof(decision));

        if (Status != RequestStatus.Open || !string.Equals(decision.RequestInstanceId, InstanceId, StringComparison.Ordinal) || decision.SubmittedAt.TotalMilliseconds < DeliveryAt.TotalMilliseconds || decision.SubmittedAt.TotalMilliseconds >= ExpiresAt.TotalMilliseconds)
        {
            return false;
        }

        Decision = decision;
        Status = RequestStatus.Resolved;
        return true;
    }

    public bool TryExpire(GameTime now)
    {
        if (Status != RequestStatus.Open || now.TotalMilliseconds < ExpiresAt.TotalMilliseconds)
        {
            return false;
        }

        Status = RequestStatus.Expired;
        return true;
    }
}