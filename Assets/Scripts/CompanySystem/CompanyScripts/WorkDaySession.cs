using System;
using System.Collections.Generic;

public sealed class WorkdaySession
{
    private readonly GameClock clock;
    private readonly EventScheduler scheduler;
    private readonly EvidenceService evidence;

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

    public WorkdaySession(DayData day, CredibilityRules credibilityRules = null)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));

        Settings = day.ScheduleData.Settings;

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
    }

    public void AdvanceRealSeconds(double realSeconds)
    {
        scheduler.AdvanceRealSeconds(realSeconds);
    }

    public void SetPaused(bool isPaused)
    {
        clock.SetPaused(isPaused);
    }

    public bool TryDiscoverEvidence(string evidenceId)
    {
        if (!Workday.CanAcceptWork)
            return false;

        return evidence.MarkDiscovered(evidenceId, Now.DayIndex, Now.MinuteOfDay);
    }

    public bool TryReadEvidence(string evidenceId)
    {
        if (!Workday.CanAcceptWork)
            return false;

        return evidence.MarkRead(evidenceId, Now.DayIndex, Now.MinuteOfDay);
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
}