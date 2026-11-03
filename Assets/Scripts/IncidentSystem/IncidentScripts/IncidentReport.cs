using System;
using System.Collections.Generic;

public sealed class IncidentReport
{
    public string IncidentInstanceId { get; }
    public string SuspectId { get; }
    public IReadOnlyList<string> EvidenceIds { get; }
    public int SubmittedDayIndex { get; }
    public int SubmittedMinute { get; }

    public IncidentReport(string incidentInstanceId, string suspectId, List<string> evidenceIds, int submittedDayIndex, int submittedMinute)
    {
        if (string.IsNullOrWhiteSpace(incidentInstanceId))
            throw new ArgumentException("Incident instance ID is required.", nameof(incidentInstanceId));

        if (string.IsNullOrWhiteSpace(suspectId))
            throw new ArgumentException("Suspect ID is required.", nameof(suspectId));

        if (evidenceIds == null)
            throw new ArgumentNullException(nameof(evidenceIds));

        if (submittedDayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(submittedDayIndex));

        if (submittedMinute < 0 || submittedMinute >= 1440)
            throw new ArgumentOutOfRangeException(nameof(submittedMinute));

        var selected = new List<string>();
        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var evidenceId in evidenceIds)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                throw new ArgumentException("Selected evidence IDs cannot be blank.", nameof(evidenceIds));

            if (!uniqueIds.Add(evidenceId))
                throw new ArgumentException($"Duplicate selected evidence ID: {evidenceId}", nameof(evidenceIds));

            selected.Add(evidenceId);
        }

        IncidentInstanceId = incidentInstanceId;
        SuspectId = suspectId;
        EvidenceIds = selected.AsReadOnly();
        SubmittedDayIndex = submittedDayIndex;
        SubmittedMinute = submittedMinute;
    }
}