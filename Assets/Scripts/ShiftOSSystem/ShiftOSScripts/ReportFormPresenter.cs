using System;
using System.Collections.Generic;

public sealed class ReportFormPresenter
{
    private readonly WorkdaySession session;

    public ReportDraftState Draft { get; }

    public ReportFormPresenter(WorkdaySession session, ReportDraftState draft)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));

        Draft = draft ?? throw new ArgumentNullException(nameof(draft));
    }

    public List<IncidentNoticeViewData> GetOpenIncidents()
    {
        var choices = new List<IncidentNoticeViewData>();

        if (!session.Workday.CanAcceptWork)
            return choices;

        GameTime now = session.Now;

        foreach (IncidentInstance incident in session.Deliveries.DeliveredIncidents)
        {
            if (incident.Status != IncidentStatus.Open)
                continue;

            var deadline = new GameTime(incident.DayIndex, incident.DeadlineMinute);

            if (now.TotalMilliseconds > deadline.TotalMilliseconds)
                continue;

            choices.Add(new IncidentNoticeViewData(incident));
        }

        return choices;
    }

    public List<EmployeeViewData> GetEmployees()
    {
        return session.GetEmployeeRoster();
    }

    public List<EvidenceDocumentViewData> GetEvidence()
    {
        return session.GetAttachableDocuments();
    }

    public void ReconcileDraft()
    {
        List<string> incidents = GetOpenIncidents().ConvertAll(incident => incident.InstanceId);

        List<string> employees = GetEmployees().ConvertAll(employee => employee.EmployeeId);

        List<string> evidence = GetEvidence().ConvertAll(document => document.EvidenceId);

        Draft.Reconcile(incidents, employees, evidence);
    }

    public bool TrySelectIncident(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        ReconcileDraft();

        if (!GetOpenIncidents().Exists(incident =>
            string.Equals(incident.InstanceId, instanceId,
                StringComparison.Ordinal)))
        {
            return false;
        }

        Draft.SelectIncident(instanceId);
        return true;
    }

    public bool TrySelectSuspect(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return false;

        ReconcileDraft();

        if (Draft.SelectedIncidentInstanceId == null)
            return false;

        if (!GetEmployees().Exists(employee =>
            string.Equals(employee.EmployeeId, employeeId,
                StringComparison.Ordinal)))
        {
            return false;
        }

        Draft.SelectSuspect(employeeId);
        return true;
    }

    public bool TrySetEvidenceSelected(string evidenceId, bool selected)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
            return false;

        ReconcileDraft();

        if (Draft.SelectedIncidentInstanceId == null)
            return false;

        if (!GetEvidence().Exists(document =>
            string.Equals(document.EvidenceId, evidenceId,
                StringComparison.Ordinal)))
        {
            return false;
        }

        Draft.SetEvidenceSelected(evidenceId, selected);
        return true;
    }

    public ReportValidationResult TrySubmit()
    {
        if (!session.Workday.CanAcceptWork)
            return Fail("The workday is not accepting new reports.");

        if (string.IsNullOrWhiteSpace(Draft.SelectedIncidentInstanceId))
            return Fail("Select an incident.");

        if (string.IsNullOrWhiteSpace(Draft.SelectedSuspectId))
            return Fail("Select a suspect.");

        if (!GetOpenIncidents().Exists(incident => string.Equals(incident.InstanceId, Draft.SelectedIncidentInstanceId, StringComparison.Ordinal)))
        {
            return Fail("The selected incident is no longer open for submission.");
        }

        if (!GetEmployees().Exists(employee => string.Equals(employee.EmployeeId, Draft.SelectedSuspectId, StringComparison.Ordinal)))
        {
            return Fail("The selected suspect is not a registered employee.");
        }

        var attachable = new HashSet<string>(GetEvidence().ConvertAll(document => document.EvidenceId), StringComparer.Ordinal);

        foreach (string evidenceId in Draft.EvidenceIds)
        {
            if (!attachable.Contains(evidenceId))
                return Fail("Selected evidence is no longer available for attachment.");
        }

        ReportValidationResult result = session.Submissions.TrySubmit(Draft.SelectedIncidentInstanceId, Draft.SelectedSuspectId, new List<string>(Draft.EvidenceIds));

        if (result.IsValid)
            Draft.Clear();

        return result;
    }

    private static ReportValidationResult Fail(string reason)
    {
        return new ReportValidationResult(false, reason);
    }
}