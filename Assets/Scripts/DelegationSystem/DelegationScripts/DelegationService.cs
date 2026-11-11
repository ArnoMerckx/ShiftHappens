using System;
using System.Collections.Generic;

public sealed class DelegationService
{
    private readonly DayData day;
    private readonly EventScheduler scheduler;
    private readonly Func<bool> canAcceptWork;

    private readonly List<RequestInstance> delivered = new List<RequestInstance>();

    private readonly Dictionary<string, RequestInstance> deliveredById = new Dictionary<string, RequestInstance>(StringComparer.Ordinal);

    private readonly Dictionary<string, DayRequestPlan> plansById = new Dictionary<string, DayRequestPlan>(StringComparer.Ordinal);

    private readonly List<RequestExpiryEntry> expiries = new List<RequestExpiryEntry>();

    public IReadOnlyList<RequestInstance> DeliveredRequests { get; }

    public IReadOnlyList<RequestExpiryEntry> Expiries { get; }

    public event Action<RequestInstance> RequestDelivered;

    public event Action<DelegationDecision> DecisionResolved;

    public event Action<RequestExpiryEntry> RequestExpired;

    public DelegationService(DayData day, EventScheduler scheduler, Func<bool> canAcceptWork)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));

        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));

        if (canAcceptWork == null)
            throw new ArgumentNullException(nameof(canAcceptWork));

        if (scheduler.IsStopped || scheduler.Now.TotalMilliseconds != day.ScheduleData.Settings.OpensAt.TotalMilliseconds)
        {
            throw new ArgumentException("Configure delegation at opening on a running scheduler.", nameof(scheduler));
        }

        this.day = day;
        this.scheduler = scheduler;
        this.canAcceptWork = canAcceptWork;

        DeliveredRequests = delivered.AsReadOnly();
        Expiries = expiries.AsReadOnly();

        foreach (var plan in day.Requests)
        {
            plansById.Add(plan.InstanceId, plan);

            scheduler.Schedule(new ScheduledEvent($"delivery:{day.ScheduleData.Settings.DayId}:request:{plan.InstanceId}", plan.DeliveryAt, ScheduledEventPriority.Delivery, () => DeliverRequest(plan)));

            var expiresAt = plan.IsFixedWindow ? new GameTime(plan.CutoffAt.Value.TotalMilliseconds + 1) : day.ScheduleData.Settings.ClosesAt;

            scheduler.Schedule(new ScheduledEvent( $"expiry:{day.ScheduleData.Settings.DayId}:request:{plan.InstanceId}", expiresAt, ScheduledEventPriority.Overdue, () => ExpireRequest(plan.InstanceId)));
        }
    }

    internal bool UsesContext(DayData day, EventScheduler scheduler)
    {
        return ReferenceEquals(this.day, day) &&ReferenceEquals(this.scheduler, scheduler);
    }

    public bool TryGetDelivered(string instanceId, out RequestInstance instance)
    {
        instance = null;

        return !string.IsNullOrWhiteSpace(instanceId) && deliveredById.TryGetValue(instanceId, out instance);
    }

    public DelegationSubmissionResult TrySubmit(string instanceId, string employeeId)
    {
        if (scheduler.IsStopped || !canAcceptWork())
            return DelegationSubmissionResult.Rejected("The workday is not accepting delegation.");

        GameTime now = scheduler.Now;

        if (now.TotalMilliseconds < day.ScheduleData.Settings.OpensAt.TotalMilliseconds || now.TotalMilliseconds >= day.ScheduleData.Settings.ClosesAt.TotalMilliseconds)
        {
            return DelegationSubmissionResult.Rejected("Delegation must be submitted during the shift.");
        }

        if (!TryGetDelivered(instanceId, out RequestInstance instance))
            return DelegationSubmissionResult.Rejected("The request has not been delivered or does not exist.");

        if (instance.Status != RequestStatus.Open)
            return DelegationSubmissionResult.Rejected("The request is no longer open.");

        if (now.TotalMilliseconds < instance.DeliveryAt.TotalMilliseconds || now.TotalMilliseconds >= instance.ExpiresAt.TotalMilliseconds)
        {
            return DelegationSubmissionResult.Rejected("The request is outside its submission period.");
        }

        bool knownEmployee = false;

        foreach (string id in day.ScheduleData.EmployeeIds)
        {
            if (string.Equals(id, employeeId, StringComparison.Ordinal))
            {
                knownEmployee = true;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(employeeId) || !knownEmployee)
            return DelegationSubmissionResult.Rejected("Select a registered employee.");

        if (!instance.Request.AllowSelfHelp && string.Equals(employeeId, instance.Request.RequesterId, StringComparison.Ordinal))
        {
            return DelegationSubmissionResult.Rejected("This request does not allow self-help.");
        }

        DelegationDecision decision = DelegationEvaluator.EvaluateDecision(plansById[instanceId], employeeId, now, day.ScheduleData);

        if (!instance.TryResolve(decision))
            return DelegationSubmissionResult.Rejected("The request could not accept this submission.");

        var result = DelegationSubmissionResult.Accepted(decision);

        DecisionResolved?.Invoke(decision);

        return result;
    }

    private void DeliverRequest(DayRequestPlan plan)
    {
        long now = scheduler.Now.TotalMilliseconds;

        if (scheduler.IsStopped || !canAcceptWork() || now < plan.DeliveryAt.TotalMilliseconds || now >= day.ScheduleData.Settings.ClosesAt.TotalMilliseconds || deliveredById.ContainsKey(plan.InstanceId))
        {
            return;
        }

        var instance = new RequestInstance(plan, day.ScheduleData.Settings.ClosesAt);

        deliveredById.Add(instance.InstanceId, instance);
        delivered.Add(instance);

        RequestDelivered?.Invoke(instance);
    }

    private void ExpireRequest(string instanceId)
    {
        if (scheduler.IsStopped || !TryGetDelivered(instanceId, out RequestInstance instance))
        {
            return;
        }

        GameTime now = scheduler.Now;

        if (!instance.TryExpire(now))
            return;

        var entry = new RequestExpiryEntry(instance, now);
        expiries.Add(entry);

        RequestExpired?.Invoke(entry);
    }
}