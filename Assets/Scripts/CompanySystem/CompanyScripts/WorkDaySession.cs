using System;
using System.Collections.Generic;

public sealed class WorkdaySession
{
    private readonly GameClock clock;
    private readonly EventScheduler scheduler;
    private readonly EvidenceService evidence;

    private readonly IReadOnlyList<EmployeeViewData> employeeRoster;

    public WorkdaySettings Settings { get; }

    public GameTime Now => clock.Now;
    public bool IsPaused => clock.IsPaused;

    public DayDeliveryService Deliveries { get; }
    public ReportSubmissionService Submissions { get; }
    public HrReviewProcessor Reviews { get; }
    public WorkdayProgress Progress { get; }
    public IncidentDeadlineService Deadlines { get; }
    public WorkdayController Workday { get; }
    public DurationTimerService Timer { get; }

    public ShiftOSNotificationState Notifications { get; } = new ShiftOSNotificationState();

    public event Action<string> EvidenceChanged;

    public event Action TimeAdvanced;

    public WorkdaySession(DayData day, CredibilityRules credibilityRules = null)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));

        Settings = day.ScheduleData.Settings;

        employeeRoster = day.ScheduleData.EmployeeRoster;

        clock = new GameClock(Settings.OpensAt, Settings.GameSecondsPerRealSecond);

        scheduler = new EventScheduler(clock);

        evidence = new EvidenceService(day.EvidenceDatabase);

        Deliveries = new DayDeliveryService(day, scheduler);

        var validator = new ReportSubmissionValidator(new List<string>(day.ScheduleData.EmployeeIds), evidence);

        Submissions = new ReportSubmissionService(scheduler, Deliveries, validator);

        Reviews = new HrReviewProcessor(Submissions, scheduler, day.ScheduleData.HrKnowledge, day.EvidenceDatabase);

        Progress = new WorkdayProgress(credibilityRules ?? new CredibilityRules(), Reviews, scheduler);

        Deadlines = new IncidentDeadlineService(Deliveries, scheduler, Progress);

        Workday = new WorkdayController(Settings, scheduler, Deliveries, Submissions, Progress);

        Timer = new DurationTimerService(scheduler, Workday);

        Timer.Completed += OnTimerCompleted;

        Reviews.ReviewCompleted += OnHrReviewCompleted;
    }

    public void AdvanceRealSeconds(double realSeconds)
    {
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

    private void OnTimerCompleted()
    {
        Notifications.Publish(ShiftOSNotificationKind.TimerExpired, "TickTock", "Your duration timer has finished.");
    }

    private void OnHrReviewCompleted(QueuedHrReview review, HrEvaluationResult result)
    {
        Notifications.Publish(ShiftOSNotificationKind.HrDecision, $"HR — {review.Incident.DisplayName}", $"{result.Verdict.ToString().ToUpperInvariant()}. " + "See BlamePoint's Report Overview for HR's reasoning.");
    }
}