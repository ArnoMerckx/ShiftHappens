using System;
using System.Collections.Generic;

public sealed class WorkdayController
{
    private readonly WorkdaySettings settings;
    private readonly EventScheduler scheduler;
    private readonly DayDeliveryService deliveries;
    private readonly ReportSubmissionService submissions;
    private readonly WorkdayProgress progress;


    public WorkdayState State { get; private set; } = WorkdayState.Working;

    public WorkdaySummary Summary { get; private set; }

    public bool IsTerminal => State == WorkdayState.Completed || State == WorkdayState.Fired;
    private Func<bool> consequenceGate;

    public bool CanAcceptWork => State == WorkdayState.Working && !scheduler.IsStopped && !progress.HasReachedFiringThreshold && scheduler.Now.TotalMilliseconds < settings.ClosesAt.TotalMilliseconds && (consequenceGate == null || consequenceGate());

    public event Action StateChanged;

    public WorkdayController(WorkdaySettings settings, EventScheduler scheduler, DayDeliveryService deliveries, ReportSubmissionService submissions, WorkdayProgress progress)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));

        if (deliveries == null)
            throw new ArgumentNullException(nameof(deliveries));

        if (submissions == null)
            throw new ArgumentNullException(nameof(submissions));

        if (progress == null)
            throw new ArgumentNullException(nameof(progress));

        if (scheduler.IsStopped || scheduler.Now.TotalMilliseconds != settings.OpensAt.TotalMilliseconds)
        {
            throw new ArgumentException("Configure workday control at opening on a running scheduler.", nameof(scheduler));
        }

        this.settings = settings;
        this.scheduler = scheduler;
        this.deliveries = deliveries;
        this.submissions = submissions;
        this.progress = progress;

        submissions.SetWorkGate(() => CanAcceptWork);

        scheduler.Schedule(new ScheduledEvent($"workday_closing:{settings.DayId}", settings.ClosesAt, ScheduledEventPriority.WorkdayClosing, BeginClosing));

        progress.Changed += OnProgressChanged;
        scheduler.EventDispatched += OnEventDispatched;
    }

    private void BeginClosing()
    {
        if (State != WorkdayState.Working || scheduler.Now.TotalMilliseconds < settings.ClosesAt.TotalMilliseconds)
        {
            return;
        }

        State = WorkdayState.Closing;
        scheduler.RestrictToPriority(ScheduledEventPriority.HrReview);

        if (!CheckForFinish())
            StateChanged?.Invoke();
    }

    internal void SetConsequenceGate(Func<bool> gate)
    {
        if (gate == null)
            throw new ArgumentNullException(nameof(gate));

        if (consequenceGate != null)
            throw new InvalidOperationException("A consequence gate has already been installed.");

        consequenceGate = gate;
    }

    internal void RefreshAfterConsequence()
    {
        CheckForFinish();
    }

    private void OnProgressChanged()
    {
        CheckForFinish();
    }

    private void OnEventDispatched(ScheduledEvent scheduledEvent)
    {
        CheckForFinish();
    }

    private bool CheckForFinish()
    {
        if (IsTerminal)
            return true;

        if (progress.HasReachedFiringThreshold)
        {
            Finish(WorkdayState.Fired);
            return true;
        }

        if (State == WorkdayState.Closing
            && submissions.PendingReviews.Count == 0)
        {
            Finish(WorkdayState.Completed);
            return true;
        }

        return false;
    }

    private void Finish(WorkdayState finalState)
    {
        if (IsTerminal)
            return;

        var summary = new WorkdaySummary(settings, finalState, scheduler.Now, progress.Credibility, new List<IncidentInstance>(deliveries.DeliveredIncidents), new List<ReviewHistoryEntry>(progress.History), new List<MissedDeadlineEntry>(progress.MissedDeadlines));

        scheduler.Stop();
        Summary = summary;
        State = finalState;

        StateChanged?.Invoke();
    }
}