using System;
using System.Collections.Generic;

public sealed class WorkdayProgress
{
    private readonly CredibilityRules rules;
    private readonly EventScheduler scheduler;

    private readonly List<ReviewHistoryEntry> history = new List<ReviewHistoryEntry>();

    private readonly HashSet<string> recordedIncidents = new HashSet<string>(StringComparer.Ordinal);

    private readonly List<MissedDeadlineEntry> missedDeadlines = new List<MissedDeadlineEntry>();

    public int Credibility { get; private set; }
    public IReadOnlyList<ReviewHistoryEntry> History { get; }


    public IReadOnlyList<MissedDeadlineEntry> MissedDeadlines { get; }

    public bool HasReachedFiringThreshold => Credibility <= rules.FiringThreshold;

    public event Action Changed;

    public WorkdayProgress(CredibilityRules rules, HrReviewProcessor processor, EventScheduler scheduler)
    {
        if (rules == null)
            throw new ArgumentNullException(nameof(rules));

        if (processor == null)
            throw new ArgumentNullException(nameof(processor));

        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));

        this.rules = rules;
        this.scheduler = scheduler;

        Credibility = rules.StartingCredibility;
        History = history.AsReadOnly();

        MissedDeadlines = missedDeadlines.AsReadOnly();

        processor.ReviewCompleted += RecordReview;
    }

    private void RecordReview(QueuedHrReview review, HrEvaluationResult decision)
    {
        string instanceId = review.Report.IncidentInstanceId;

        if (recordedIncidents.Contains(instanceId))
            return;

        if (!ReferenceEquals(review.Incident.Decision, decision) || !ReferenceEquals(review.Incident.SubmittedReport, review.Report) || !ReferenceEquals(decision.Report, review.Report))
        {
            return;
        }

        int before = Credibility;

        int after = decision.Verdict == HrVerdict.Rejected ? Math.Max(0, before - rules.RejectedReportPenalty) : before;

        var entry = new ReviewHistoryEntry(review, decision, scheduler.Now, before, after);

        recordedIncidents.Add(instanceId);
        history.Add(entry);
        Credibility = after;

        Changed?.Invoke();
    }

    internal bool RecordMissedDeadline(IncidentInstance incident)
    {
        if (incident == null)
            throw new ArgumentNullException(nameof(incident));

        if (incident.Status != IncidentStatus.Overdue || recordedIncidents.Contains(incident.InstanceId))
        {
            return false;
        }

        int before = Credibility;
        int after = Math.Max(0, before - rules.MissedDeadlinePenalty);

        var entry = new MissedDeadlineEntry(incident, scheduler.Now, before, after);

        recordedIncidents.Add(incident.InstanceId);
        missedDeadlines.Add(entry);
        Credibility = after;

        Changed?.Invoke();
        return true;
    }
}