using System;
using System.Collections.Generic;

public sealed class ReportSubmissionValidator
{
    private readonly EvidenceService evidenceService;

    private readonly HashSet<string> knownEmployeeIds = new HashSet<string>(StringComparer.Ordinal);

    public ReportSubmissionValidator(List<string> employeeIds, EvidenceService evidenceService)
    {
        if (employeeIds == null)
            throw new ArgumentNullException(nameof(employeeIds));

        if (evidenceService == null)
            throw new ArgumentNullException(nameof(evidenceService));

        foreach (var employeeId in employeeIds)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
                throw new ArgumentException("Employee IDs cannot be blank.", nameof(employeeIds));

            if (!knownEmployeeIds.Add(employeeId))
                throw new ArgumentException($"Duplicate employee ID: {employeeId}", nameof(employeeIds));
        }

        this.evidenceService = evidenceService;
    }

    public ReportValidationResult Validate(IncidentInstance incident, IncidentReport report, int currentDayIndex, int currentMinute)
    {
        if (incident == null)
            throw new ArgumentNullException(nameof(incident));

        if (report == null)
            throw new ArgumentNullException(nameof(report));

        if (currentDayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(currentDayIndex));

        if (currentMinute < 0 || currentMinute >= 1440)
            throw new ArgumentOutOfRangeException(nameof(currentMinute));

        if (report.IncidentInstanceId != incident.InstanceId)
            return Fail("The report refers to a different incident instance.");

        if (incident.Status != IncidentStatus.Open)
            return Fail("The incident is no longer open for submission.");

        if (report.SubmittedDayIndex != currentDayIndex || report.SubmittedMinute != currentMinute)
        {
            return Fail("The report timestamp does not match submission time.");
        }

        if (currentDayIndex < incident.DayIndex || (currentDayIndex == incident.DayIndex && currentMinute < incident.DeliveryMinute))
        {
            return Fail("The incident has not been delivered yet.");
        }

        if (currentDayIndex > incident.DayIndex || currentMinute > incident.DeadlineMinute)
        {
            return Fail("The reporting deadline has passed.");
        }

        if (!knownEmployeeIds.Contains(report.SuspectId))
            return Fail("The selected suspect is not a registered employee.");

        foreach (var evidenceId in report.EvidenceIds)
        {
            try
            {
                if (!evidenceService.CanAttach(
                    evidenceId, currentDayIndex, currentMinute))
                {
                    return Fail($"Evidence is not attachable: {evidenceId}");
                }
            }
            catch (KeyNotFoundException)
            {
                return Fail($"Unknown evidence ID: {evidenceId}");
            }
        }

        return new ReportValidationResult(true, "The report can be submitted.");
    }

    private static ReportValidationResult Fail(string reason)
    {
        return new ReportValidationResult(false, reason);
    }
}