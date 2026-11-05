using System;
using System.Collections.Generic;

public sealed class ReportSubmissionService
{
    private readonly EventScheduler scheduler;
    private readonly DayDeliveryService deliveries;
    private readonly ReportSubmissionValidator validator;

    private readonly List<QueuedHrReview> pendingReviews = new List<QueuedHrReview>();

    public IReadOnlyList<QueuedHrReview> PendingReviews { get; }

    public event Action<QueuedHrReview> ReviewQueued;

    private Func<bool> workGate;

    public ReportSubmissionService(EventScheduler scheduler, DayDeliveryService deliveries, ReportSubmissionValidator validator)
    {
        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));

        if (deliveries == null)
            throw new ArgumentNullException(nameof(deliveries));

        if (validator == null)
            throw new ArgumentNullException(nameof(validator));

        this.scheduler = scheduler;
        this.deliveries = deliveries;
        this.validator = validator;
        PendingReviews = pendingReviews.AsReadOnly();
    }

    public ReportValidationResult TrySubmit(string instanceId, string suspectId, List<string> evidenceIds)
    {
        IncidentInstance incident;

        if (workGate != null && !workGate())
        {
            return new ReportValidationResult(false, "The workday is not accepting new reports.");
        }

        try
        {
            incident = deliveries.GetDelivered(instanceId);
        }
        catch (KeyNotFoundException)
        {
            return new ReportValidationResult(false, "The incident has not been delivered or does not exist.");
        }

        GameTime now = scheduler.Now;

        var deliveredAt = new GameTime(incident.DayIndex, incident.DeliveryMinute);

        var deadline = new GameTime(incident.DayIndex, incident.DeadlineMinute);

        if (now.TotalMilliseconds < deliveredAt.TotalMilliseconds)
        {
            return new ReportValidationResult(false, "The incident has not been delivered yet.");
        }

        if (now.TotalMilliseconds > deadline.TotalMilliseconds)
        {
            return new ReportValidationResult(false, "The reporting deadline has passed.");
        }

        var report = new IncidentReport(instanceId, suspectId, evidenceIds, now.DayIndex, now.MinuteOfDay);

        var review = new QueuedHrReview(incident, report, now);

        ReportValidationResult result = incident.TrySubmit(report, validator, now.DayIndex, now.MinuteOfDay);

        if (result.IsValid)
        {
            pendingReviews.Add(review);
            ReviewQueued?.Invoke(review);
        }

        return result;
    }

    internal void SetWorkGate(Func<bool> gate)
    {
        if (gate == null)
            throw new ArgumentNullException(nameof(gate));

        if (workGate != null)
            throw new InvalidOperationException("A workday gate has already been installed.");

        workGate = gate;
    }

    internal bool ContainsReview(QueuedHrReview review)
    {
        return pendingReviews.Contains(review);
    }

    internal bool RemoveReview(QueuedHrReview review)
    {
        return pendingReviews.Remove(review);
    }
}