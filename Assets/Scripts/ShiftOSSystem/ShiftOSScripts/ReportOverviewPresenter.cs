using System;
using System.Collections.Generic;

public sealed class ReportOverviewPresenter
{
    private readonly WorkdaySession session;

    public AppReadingState State { get; }

    public event Action Changed;

    public ReportOverviewPresenter(WorkdaySession session, AppReadingState state)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));

        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public List<ReportOverviewViewData> GetEntries()
    {
        return session.GetReportOverview();
    }

    public ReportOverviewViewData GetSelectedEntry()
    {
        foreach (ReportOverviewViewData entry in GetEntries())
        {
            if (string.Equals(entry.InstanceId, State.SelectedItemId, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    public bool TrySelectEntry(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        foreach (ReportOverviewViewData entry in GetEntries())
        {
            if (!string.Equals(entry.InstanceId, instanceId, StringComparison.Ordinal))
            {
                continue;
            }

            State.SelectItem(instanceId);
            Changed?.Invoke();
            return true;
        }

        return false;
    }

    public void Connect()
    {
        Disconnect();

        session.Deliveries.IncidentDelivered += OnIncidentChanged;
        session.Deadlines.IncidentOverdue += OnIncidentChanged;
        session.Submissions.ReviewQueued += OnReviewQueued;
        session.Reviews.ReviewCompleted += OnReviewCompleted;

        Changed?.Invoke();
    }

    public void Disconnect()
    {
        session.Deliveries.IncidentDelivered -= OnIncidentChanged;
        session.Deadlines.IncidentOverdue -= OnIncidentChanged;
        session.Submissions.ReviewQueued -= OnReviewQueued;
        session.Reviews.ReviewCompleted -= OnReviewCompleted;
    }

    private void OnIncidentChanged(IncidentInstance incident)
    {
        Changed?.Invoke();
    }

    private void OnReviewQueued(QueuedHrReview review)
    {
        Changed?.Invoke();
    }

    private void OnReviewCompleted(QueuedHrReview review, HrEvaluationResult result)
    {
        Changed?.Invoke();
    }
}