using System;

public sealed class IncidentSummary
{
    public string InstanceId { get; }
    public string IncidentId { get; }
    public int DayIndex { get; }
    public string DisplayName { get; }
    public IncidentStatus Status { get; }
    public IncidentReport Report { get; }
    public HrEvaluationResult Decision { get; }

    public IncidentSummary(IncidentInstance incident)
    {
        if (incident == null)
            throw new ArgumentNullException(nameof(incident));

        InstanceId = incident.InstanceId;
        IncidentId = incident.IncidentId;
        DayIndex = incident.DayIndex;
        DisplayName = incident.DisplayName;
        Status = incident.Status;
        Report = incident.SubmittedReport;
        Decision = incident.Decision;
    }
}