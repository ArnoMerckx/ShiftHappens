using System;

public sealed class IncidentNoticeViewData
{
    public string InstanceId { get; }
    public string DisplayName { get; }
    public string BodyText { get; }
    public string DueTimeText { get; }

    public IncidentNoticeViewData(IncidentInstance incident)
    {
        if (incident == null)
            throw new ArgumentNullException(nameof(incident));

        InstanceId = incident.InstanceId;
        DisplayName = incident.DisplayName;
        BodyText = incident.Description;

        int hour = incident.DeadlineMinute / 60;
        int minute = incident.DeadlineMinute % 60;
        DueTimeText = $"{hour:00}:{minute:00}";
    }
}