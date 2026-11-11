using System;
using System.Collections.Generic;

public sealed class ConsequenceCoordinator
{
    private readonly DayData day;
    private readonly EventScheduler scheduler;
    private readonly WorkdayProgress progress;
    private readonly EmployeeTrustState trust;
    private readonly EmployeeWorkloadHistory workload;
    private readonly ConsequenceRules rules;
    private readonly WorkdayController workday;
    private DelegationService delegation;
    private ReportSubmissionService submissions;

    private readonly List<ConsequenceEntry> history = new List<ConsequenceEntry>();

    private readonly HashSet<string> processedRequests = new HashSet<string>(StringComparer.Ordinal);

    private readonly HashSet<string> processedReports = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyList<ConsequenceEntry> History { get; }
    public bool IsApplying { get; private set; }

    public event Action<ConsequenceEntry> ConsequenceCommitted;

    public ConsequenceCoordinator(DayData day, EventScheduler scheduler, WorkdayProgress progress, EmployeeTrustState trust, EmployeeWorkloadHistory workload, ConsequenceRules rules, WorkdayController workday)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));
        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));
        if (progress == null)
            throw new ArgumentNullException(nameof(progress));
        if (trust == null)
            throw new ArgumentNullException(nameof(trust));
        if (workload == null)
            throw new ArgumentNullException(nameof(workload));
        if (rules == null)
            throw new ArgumentNullException(nameof(rules));
        if (workday == null)
            throw new ArgumentNullException(nameof(workday));

        if (scheduler.IsStopped || scheduler.Now.TotalMilliseconds != day.ScheduleData.Settings.OpensAt.TotalMilliseconds)
        {
            throw new ArgumentException("Configure consequences at opening on a running scheduler.", nameof(scheduler));
        }

        var employeeIds = new HashSet<string>(day.ScheduleData.EmployeeIds, StringComparer.Ordinal);

        if (!employeeIds.SetEquals(trust.EmployeeIds))
            throw new ArgumentException("Trust must cover exactly the day's roster.", nameof(trust));

        this.day = day;
        this.scheduler = scheduler;
        this.progress = progress;
        this.trust = trust;
        this.workload = workload;
        this.rules = rules;
        this.workday = workday;

        History = history.AsReadOnly();
    }

    internal void AttachDelegation(DelegationService service)
    {
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        if (delegation != null)
            throw new InvalidOperationException(
                "Delegation consequences are already attached.");

        if (!service.UsesContext(day, scheduler))
            throw new ArgumentException("Delegation must use the same day and scheduler.", nameof(service));

        if (scheduler.IsStopped || scheduler.Now.TotalMilliseconds != day.ScheduleData.Settings.OpensAt.TotalMilliseconds)
        {
            throw new InvalidOperationException("Attach delegation consequences at opening.");
        }

        delegation = service;
        delegation.DecisionResolved += OnDecisionResolved;
        delegation.RequestExpired += OnRequestExpired;
    }

    internal void AttachReports(ReportSubmissionService service)
    {
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        if (submissions != null)
            throw new InvalidOperationException("Report consequences are already attached.");

        if (!service.UsesContext(scheduler))
            throw new ArgumentException("Reports must use the same scheduler.", nameof(service));

        if (scheduler.IsStopped || scheduler.Now.TotalMilliseconds != day.ScheduleData.Settings.OpensAt.TotalMilliseconds)
        {
            throw new InvalidOperationException("Attach report consequences at opening.");
        }

        submissions = service;
        submissions.ReviewQueued += OnReportAccepted;
    }

    private void OnDecisionResolved(DelegationDecision decision)
    {
        if (decision == null || delegation == null || scheduler.IsStopped || workday.IsTerminal)
        {
            return;
        }

        if (!delegation.TryGetDelivered(decision.RequestInstanceId, out RequestInstance request))
        {
            return;
        }

        if (request.Status != RequestStatus.Resolved || !ReferenceEquals(request.Decision, decision) || decision.SubmittedAt.TotalMilliseconds != scheduler.Now.TotalMilliseconds)
        {
            return;
        }

        var employeeIds = new HashSet<string>(day.ScheduleData.EmployeeIds, StringComparer.Ordinal);

        if (!employeeIds.Contains(decision.HelperId))
            return;

        var assignment = new AssignmentHistoryEntry(day.ScheduleData.Settings.DayId, decision);

        Commit(decision.RequestInstanceId, ConsequenceKind.RequestResolved, rules.GetCredibilityDelta(decision.Outcome),new List<string>{request.Request.RequesterId,decision.HelperId},rules.GetTrustDelta(decision.Outcome),assignment);
    }

    private void OnRequestExpired(RequestExpiryEntry entry)
    {
        if (entry == null || delegation == null || scheduler.IsStopped || workday.IsTerminal)
        {
            return;
        }

        if (!delegation.TryGetDelivered(entry.RequestInstanceId, out RequestInstance request))
        {
            return;
        }

        if (request.Status != RequestStatus.Expired ||
            entry.RecordedAt.TotalMilliseconds !=
            scheduler.Now.TotalMilliseconds ||
            entry.ExpiresAt.TotalMilliseconds !=
            request.ExpiresAt.TotalMilliseconds ||
            !string.Equals(entry.RequestId,
                request.Request.RequestId, StringComparison.Ordinal) ||
            !string.Equals(entry.RequesterId,
                request.Request.RequesterId, StringComparison.Ordinal))
        {
            return;
        }

        bool storedEntry = false;

        foreach (var recorded in delegation.Expiries)
        {
            if (ReferenceEquals(recorded, entry))
            {
                storedEntry = true;
                break;
            }
        }

        if (!storedEntry)
            return;

        Commit(entry.RequestInstanceId, ConsequenceKind.RequestExpired, rules.UnansweredCredibilityDelta, new List<string> { entry.RequesterId }, rules.UnansweredTrustDelta);
    }

    private void OnReportAccepted(QueuedHrReview review)
    {
        if (review == null || submissions == null || scheduler.IsStopped || workday.IsTerminal)
        {
            return;
        }

        if (!submissions.ContainsReview(review) || review.Incident.Status != IncidentStatus.Pending || !ReferenceEquals(review.Incident.SubmittedReport, review.Report) || review.SubmittedAt.TotalMilliseconds != scheduler.Now.TotalMilliseconds)
        {
            return;
        }

        bool knownIncident = false;

        foreach (var plan in day.Incidents)
        {
            if (ReferenceEquals(plan.Schedule, review.Incident.Schedule))
            {
                knownIncident = true;
                break;
            }
        }

        if (!knownIncident)
            return;

        string culpritId = day.GetActualCulpritId(review.Report.IncidentInstanceId);

        bool wrongful = !string.Equals(review.Report.SuspectId, culpritId, StringComparison.Ordinal);

        var affectedEmployees = new List<string>();

        if (wrongful)
            affectedEmployees.Add(review.Report.SuspectId);

        Commit(review.Report.IncidentInstanceId, ConsequenceKind.ReportAccepted, 0, affectedEmployees, wrongful ? rules.WrongfulBlameTrustDelta : 0);
    }

    private bool Commit(string instanceId, ConsequenceKind kind, int credibilityDelta, List<string> affectedEmployeeIds, int trustDelta, AssignmentHistoryEntry assignment = null)
    {
        if (IsApplying || scheduler.IsStopped || workday.IsTerminal || progress.HasReachedFiringThreshold || workday.State != WorkdayState.Working)
        {
            return false;
        }

        bool isExpiry = kind == ConsequenceKind.RequestExpired;

        if (isExpiry)
        {
            if (scheduler.Now.TotalMilliseconds > day.ScheduleData.Settings.ClosesAt.TotalMilliseconds)
            {
                return false;
            }
        }
        else if (!workday.CanAcceptWork)
        {
            return false;
        }

        var processed = kind == ConsequenceKind.ReportAccepted ? processedReports : processedRequests;

        if (processed.Contains(instanceId))
            return false;

        if (kind == ConsequenceKind.RequestResolved && assignment == null)
            throw new InvalidOperationException("A resolved request requires an assignment record.");

        if (assignment != null && (kind != ConsequenceKind.RequestResolved || !string.Equals(assignment.DayId, day.ScheduleData.Settings.DayId, StringComparison.Ordinal) || !string.Equals(assignment.RequestInstanceId, instanceId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The assignment must match the resolved request and day.");
        }

        var beforeTrust = new List<EmployeeTrustEntry>();
        var distinctIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (string employeeId in affectedEmployeeIds)
        {
            if (distinctIds.Add(employeeId))
            {
                beforeTrust.Add(new EmployeeTrustEntry(employeeId, trust.GetTrust(employeeId)));
            }
        }

        IsApplying = true;

        try
        {
            if (assignment != null && !workload.TryRecord(assignment))
                return false;

            int credibilityBefore = progress.Credibility;
            int credibilityAfter = progress.ApplyCredibilityDelta(credibilityDelta);

            var trustChanges = new List<EmployeeTrustChange>();

            foreach (var before in beforeTrust)
            {
                int after = trust.ApplyTrustDelta(before.EmployeeId, trustDelta);

                trustChanges.Add(new EmployeeTrustChange(before.EmployeeId, before.Trust, after));
            }

            var entry = new ConsequenceEntry(day.ScheduleData.Settings.DayId, instanceId, kind, scheduler.Now, credibilityBefore, credibilityAfter, trustChanges);

            processed.Add(instanceId);
            history.Add(entry);

            workday.RefreshAfterConsequence();
            progress.NotifyChanged();
            ConsequenceCommitted?.Invoke(entry);

            return true;
        }
        finally
        {
            IsApplying = false;
        }
    }
}