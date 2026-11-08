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
        int start = incident.Occurrence.StartMinute;
        int end = incident.Occurrence.EndMinute;

        BodyText = $"Occurred: {start / 60:00}:{start % 60:00}–" + $"{end / 60:00}:{end % 60:00}\n\n" + incident.Description;

        int hour = incident.DeadlineMinute / 60;
        int minute = incident.DeadlineMinute % 60;
        DueTimeText = $"{hour:00}:{minute:00}";
    }
}