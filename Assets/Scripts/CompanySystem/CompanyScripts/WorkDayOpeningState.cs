using System;
using System.Collections.Generic;

public sealed class WorkdayOpeningState
{
    public int Credibility { get; }
    public IReadOnlyList<EmployeeTrustEntry> EmployeeTrusts { get; }
    public IReadOnlyList<AssignmentHistoryEntry> Assignments { get; }

    public WorkdayOpeningState(int credibility, List<EmployeeTrustEntry> employeeTrusts, List<AssignmentHistoryEntry> assignments)
    {
        if (credibility < 0)
            throw new ArgumentOutOfRangeException(nameof(credibility));

        if (employeeTrusts == null)
            throw new ArgumentNullException(nameof(employeeTrusts));

        if (assignments == null)
            throw new ArgumentNullException(nameof(assignments));

        var trustCopy = new List<EmployeeTrustEntry>();
        var employeeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in employeeTrusts)
        {
            if (entry == null)
                throw new ArgumentException("Trust entries cannot contain null.", nameof(employeeTrusts));

            if (!employeeIds.Add(entry.EmployeeId))
                throw new ArgumentException($"Duplicate employee ID: {entry.EmployeeId}", nameof(employeeTrusts));

            trustCopy.Add(entry);
        }

        var assignmentCopy = new List<AssignmentHistoryEntry>();
        var recordedByDay = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var entry in assignments)
        {
            if (entry == null)
                throw new ArgumentException("Assignments cannot contain null.", nameof(assignments));

            if (!employeeIds.Contains(entry.HelperId))
                throw new ArgumentException($"Unknown assignment helper: {entry.HelperId}", nameof(assignments));

            if (!recordedByDay.TryGetValue(entry.DayId, out var requestIds))
            {
                requestIds = new HashSet<string>(StringComparer.Ordinal);
                recordedByDay.Add(entry.DayId, requestIds);
            }

            if (!requestIds.Add(entry.RequestInstanceId))
                throw new ArgumentException("Duplicate assignment for the same day and request.", nameof(assignments));

            assignmentCopy.Add(entry);
        }

        Credibility = credibility;
        EmployeeTrusts = trustCopy.AsReadOnly();
        Assignments = assignmentCopy.AsReadOnly();
    }

    public static WorkdayOpeningState Capture(int credibility, EmployeeTrustState trust, EmployeeWorkloadHistory workload)
    {
        if (trust == null)
            throw new ArgumentNullException(nameof(trust));

        if (workload == null)
            throw new ArgumentNullException(nameof(workload));

        var entries = new List<EmployeeTrustEntry>();

        foreach (string employeeId in trust.EmployeeIds)
        {
            entries.Add(new EmployeeTrustEntry(employeeId, trust.GetTrust(employeeId)));
        }

        return new WorkdayOpeningState(credibility, entries, new List<AssignmentHistoryEntry>(workload.History));
    }
}