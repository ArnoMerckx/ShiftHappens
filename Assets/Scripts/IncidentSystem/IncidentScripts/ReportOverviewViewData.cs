using System;
using System.Collections.Generic;

public sealed class ReportOverviewViewData
{
    public string InstanceId { get; }
    public string DisplayName { get; }
    public string BodyText { get; }
    public string DueTimeText { get; }
    public IncidentStatus Status { get; }

    public string SuspectId { get; }
    public IReadOnlyList<string> EvidenceIds { get; }

    public string DecisionSummary { get; }
    public IReadOnlyList<string> DecisionReasons { get; }

    public ReportOverviewViewData(IncidentInstance incident)
    {
        if (incident == null)
            throw new ArgumentNullException(nameof(incident));

        InstanceId = incident.InstanceId;
        DisplayName = incident.DisplayName;
        BodyText = incident.Description;
        Status = incident.Status;

        int hour = incident.DeadlineMinute / 60;
        int minute = incident.DeadlineMinute % 60;
        DueTimeText = $"{hour:00}:{minute:00}";

        var evidenceIds = new List<string>();

        if (incident.SubmittedReport != null)
        {
            SuspectId = incident.SubmittedReport.SuspectId;
            evidenceIds.AddRange(incident.SubmittedReport.EvidenceIds);
        }

        EvidenceIds = evidenceIds.AsReadOnly();

        var reasons = new List<string>();
        DecisionSummary = string.Empty;

        if (incident.Decision != null)
        {
            DecisionSummary = incident.Decision.Summary;

            foreach (HrRuleResult result in incident.Decision.RuleResults)
                reasons.Add(result.Reason);
        }

        DecisionReasons = reasons.AsReadOnly();
    }
}