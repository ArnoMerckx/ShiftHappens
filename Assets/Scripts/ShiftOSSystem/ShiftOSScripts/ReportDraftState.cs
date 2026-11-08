using System;
using System.Collections.Generic;

public sealed class ReportDraftState
{
    private readonly List<string> evidenceIds = new List<string>();

    public string SelectedIncidentInstanceId { get; private set; }
    public string SelectedSuspectId { get; private set; }
    public IReadOnlyList<string> EvidenceIds { get; }

    public ReportDraftState()
    {
        EvidenceIds = evidenceIds.AsReadOnly();
    }

    public void SelectIncident(string instanceId)
    {
        ValidateId(instanceId, nameof(instanceId));

        if (string.Equals(SelectedIncidentInstanceId, instanceId, StringComparison.Ordinal))
        {
            return;
        }

        Clear();
        SelectedIncidentInstanceId = instanceId;
    }

    public void SelectSuspect(string employeeId)
    {
        ValidateId(employeeId, nameof(employeeId));
        RequireIncident();
        SelectedSuspectId = employeeId;
    }

    public void ClearSuspect()
    {
        SelectedSuspectId = null;
    }

    public void SetEvidenceSelected(string evidenceId, bool selected)
    {
        ValidateId(evidenceId, nameof(evidenceId));
        RequireIncident();

        if (selected)
        {
            if (!evidenceIds.Contains(evidenceId))
                evidenceIds.Add(evidenceId);
        }
        else
        {
            evidenceIds.Remove(evidenceId);
        }
    }

    public void Clear()
    {
        SelectedIncidentInstanceId = null;
        SelectedSuspectId = null;
        evidenceIds.Clear();
    }

    public void Reconcile(List<string> openIncidentIds, List<string> employeeIds, List<string> attachableEvidenceIds)
    {
        HashSet<string> open = ReadIds(openIncidentIds, nameof(openIncidentIds));

        HashSet<string> employees = ReadIds(employeeIds, nameof(employeeIds));

        HashSet<string> attachable = ReadIds(attachableEvidenceIds, nameof(attachableEvidenceIds));

        if (SelectedIncidentInstanceId == null)
            return;

        if (!open.Contains(SelectedIncidentInstanceId))
        {
            Clear();
            return;
        }

        if (SelectedSuspectId != null && !employees.Contains(SelectedSuspectId))
        {
            SelectedSuspectId = null;
        }

        evidenceIds.RemoveAll(id => !attachable.Contains(id));
    }

    private void RequireIncident()
    {
        if (SelectedIncidentInstanceId == null)
            throw new InvalidOperationException("Select an incident first.");
    }

    private static void ValidateId(string id, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An ID is required.", parameterName);
    }

    private static HashSet<string> ReadIds(List<string> ids, string parameterName)
    {
        if (ids == null)
            throw new ArgumentNullException(parameterName);

        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (string id in ids)
        {
            ValidateId(id, parameterName);
            result.Add(id);
        }

        return result;
    }
}