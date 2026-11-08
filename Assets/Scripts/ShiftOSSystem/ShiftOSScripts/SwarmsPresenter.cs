using System;
using System.Collections.Generic;

public sealed class SwarmsPresenter
{
    private readonly WorkdaySession session;

    public AppReadingState State { get; }

    public SwarmsPresenter(WorkdaySession session, AppReadingState state)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));

        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public List<IncidentNoticeViewData> GetNotices()
    {
        var notices = new List<IncidentNoticeViewData>();

        foreach (IncidentInstance incident in session.Deliveries.DeliveredIncidents)
        {
            notices.Add(new IncidentNoticeViewData(incident));
        }

        return notices;
    }

    public IncidentNoticeViewData GetSelectedNotice()
    {
        foreach (IncidentNoticeViewData notice in GetNotices())
        {
            if (string.Equals(notice.InstanceId, State.SelectedItemId, StringComparison.Ordinal))
            {
                return notice;
            }
        }

        return null;
    }

    public bool TrySelectNotice(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        foreach (IncidentNoticeViewData notice in GetNotices())
        {
            if (!string.Equals(notice.InstanceId, instanceId, StringComparison.Ordinal))
            {
                continue;
            }

            State.SelectItem(instanceId);
            return true;
        }

        return false;
    }
}