using System;
using System.Collections.Generic;

public sealed class WorkdaySession
{
    private readonly GameClock clock;
    private readonly EventScheduler scheduler;
    private readonly EvidenceService evidence;

    private readonly IReadOnlyList<EmployeeViewData> employeeRoster;
    private readonly SwarmsRequestReplyComposer swarmsRequestReplies;
    private readonly SwarmsCoworkerReactionComposer swarmsCoworkerReactions;

    public WorkdaySettings Settings { get; }

    public GameTime Now => clock.Now;
    public bool IsPaused => clock.IsPaused;

    public DayDeliveryService Deliveries { get; }
    public ReportSubmissionService Submissions { get; }
    public HrReviewProcessor Reviews { get; }
    public WorkdayProgress Progress { get; }
    public EmployeeTrustState Trust { get; }
    public EmployeeWorkloadHistory Workload { get; }
    public ConsequenceCoordinator Consequences { get; }
    public SwarmsReplyFeed SwarmsReplies { get; }
    public IncidentDeadlineService Deadlines { get; }
    public WorkdayController Workday { get; }

    public DelegationService Delegation { get; }
    public DurationTimerService Timer { get; }

    public ShiftOSNotificationState Notifications { get; } = new ShiftOSNotificationState();

    public event Action<string> EvidenceChanged;

    public event Action TimeAdvanced;

    public WorkdaySession(DayData day, CredibilityRules credibilityRules = null, ConsequenceRules consequenceRules = null, WorkdayOpeningState openingState = null, ReplyTimingRules replyTimingRules = null, CoworkerReactionRules coworkerReactionRules = null)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));

        Settings = day.ScheduleData.Settings;

        var scoreRules = credibilityRules ?? new CredibilityRules();
        var relationshipRules = consequenceRules ?? new ConsequenceRules();

        if (openingState != null)
        {
            foreach (var assignment in openingState.Assignments)
            {
                if (assignment.DayIndex >= Settings.DayIndex || string.Equals(assignment.DayId, Settings.DayId, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Opening assignments must belong to earlier days.", nameof(openingState));
                }
            }
        }

        Trust = new EmployeeTrustState( new List<string>(day.ScheduleData.EmployeeIds), relationshipRules, openingState == null ? null : new List<EmployeeTrustEntry>(openingState.EmployeeTrusts));

        Workload = new EmployeeWorkloadHistory(openingState == null ? null : new List<AssignmentHistoryEntry>(openingState.Assignments));

        employeeRoster = day.ScheduleData.EmployeeRoster;

        clock = new GameClock(Settings.OpensAt, Settings.GameSecondsPerRealSecond);

        scheduler = new EventScheduler(clock);

        evidence = new EvidenceService(day.EvidenceDatabase);

        Deliveries = new DayDeliveryService(day, scheduler);

        var validator = new ReportSubmissionValidator(new List<string>(day.ScheduleData.EmployeeIds), evidence);

        Submissions = new ReportSubmissionService(scheduler, Deliveries, validator);

        Reviews = new HrReviewProcessor(Submissions, scheduler, day.ScheduleData.HrKnowledge, day.EvidenceDatabase);

        Progress = new WorkdayProgress(scoreRules, Reviews, scheduler, openingState?.Credibility);

        Deadlines = new IncidentDeadlineService(Deliveries, scheduler, Progress);

        Workday = new WorkdayController(Settings, scheduler, Deliveries, Submissions, Progress);

        Delegation = new DelegationService(day, scheduler, () => Workday.CanAcceptWork);

        Consequences = new ConsequenceCoordinator(day, scheduler, Progress, Trust, Workload, relationshipRules, Workday);

        Workday.SetConsequenceGate(() => !Consequences.IsApplying);

        Consequences.AttachDelegation(Delegation);
        Consequences.AttachReports(Submissions);

        SwarmsReplies = new SwarmsReplyFeed(day, scheduler, Workday);

        swarmsRequestReplies = new SwarmsRequestReplyComposer(day, Delegation, Consequences, Workload, SwarmsReplies, replyTimingRules ?? new ReplyTimingRules());

        swarmsCoworkerReactions = new SwarmsCoworkerReactionComposer(day, Delegation, Consequences, Workload, Trust, SwarmsReplies, replyTimingRules ?? new ReplyTimingRules(), coworkerReactionRules ?? new CoworkerReactionRules());

        SwarmsReplies.ReplyDelivered += OnSwarmsReplyDelivered;

        Timer = new DurationTimerService(scheduler, Workday);

        Timer.Completed += OnTimerCompleted;

        Reviews.ReviewCompleted += OnHrReviewCompleted;
    }

    public void AdvanceRealSeconds(double realSeconds)
    {
        if (Consequences.IsApplying)
            return;

        scheduler.AdvanceRealSeconds(realSeconds);

        TimeAdvanced?.Invoke();
    }

    public void SetPaused(bool isPaused)
    {
        clock.SetPaused(isPaused);
    }

    public bool TryDiscoverEvidence(string evidenceId)
    {
        if (!Workday.CanAcceptWork)
            return false;

        bool changed = evidence.MarkDiscovered(evidenceId, Now.DayIndex, Now.MinuteOfDay);

        if (changed)
            EvidenceChanged?.Invoke(evidenceId);

        return changed;
    }

    public bool TryReadEvidence(string evidenceId)
    {
        if (!Workday.CanAcceptWork)
            return false;

        bool changed = evidence.MarkRead(evidenceId, Now.DayIndex, Now.MinuteOfDay);

        if (changed)
            EvidenceChanged?.Invoke(evidenceId);

        return changed;
    }

    public bool IsEvidenceAvailable(string evidenceId)
    {
        return evidence.IsAvailable(evidenceId, Now.DayIndex, Now.MinuteOfDay);
    }

    public bool IsEvidenceDiscovered(string evidenceId)
    {
        return evidence.IsDiscovered(evidenceId);
    }

    public bool IsEvidenceRead(string evidenceId)
    {
        return evidence.IsRead(evidenceId);
    }

    public bool CanAttachEvidence(string evidenceId)
    {
        return evidence.CanAttach(evidenceId, Now.DayIndex, Now.MinuteOfDay);
    }

    public List<EvidenceDocumentViewData> GetAvailableDigitalDocuments(EvidenceSource source)
    {
        return evidence.GetAvailableDigitalDocuments(source, Now.DayIndex, Now.MinuteOfDay);
    }

    public List<EvidenceDocumentViewData> GetAttachableDocuments()
    {
        return evidence.GetAttachableDocuments(Now.DayIndex, Now.MinuteOfDay);
    }

    public List<EmployeeViewData> GetEmployeeRoster()
    {
        return new List<EmployeeViewData>(employeeRoster);
    }

    public List<ReportOverviewViewData> GetReportOverview()
    {
        var entries = new List<ReportOverviewViewData>();

        foreach (IncidentInstance incident in Deliveries.DeliveredIncidents)
            entries.Add(new ReportOverviewViewData(incident));

        return entries;
    }

    public List<RequestOverviewViewData> GetRequestOverview()
    {
        var entries = new List<RequestOverviewViewData>();

        foreach (RequestInstance request in Delegation.DeliveredRequests)
            entries.Add(new RequestOverviewViewData(request));

        return entries;
    }

    public DelegationSubmissionResult TryDelegate(string instanceId, string employeeId)
    {
        return Delegation.TrySubmit(instanceId, employeeId);
    }

    private void OnSwarmsReplyDelivered(SwarmsReplyData reply)
    {
        Notifications.Publish(ShiftOSNotificationKind.SwarmsReply, "Swarms — " + reply.SenderName, "New reply: " + reply.Subject);
    }

    private void OnTimerCompleted()
    {
        Notifications.Publish(ShiftOSNotificationKind.TimerExpired, "TickTock", "Your duration timer has finished.");
    }

    private void OnHrReviewCompleted(QueuedHrReview review, HrEvaluationResult result)
    {
        Notifications.Publish(ShiftOSNotificationKind.HrDecision, $"HR — {review.Incident.DisplayName}", $"{result.Verdict.ToString().ToUpperInvariant()}. " + "See BlamePoint's Report Overview for HR's reasoning.");
    }
}