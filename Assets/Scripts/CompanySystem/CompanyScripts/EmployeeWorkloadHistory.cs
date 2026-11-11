using System;
using System.Collections.Generic;

public sealed class EmployeeWorkloadHistory
{
    private readonly List<AssignmentHistoryEntry> history = new List<AssignmentHistoryEntry>();

    private readonly Dictionary<string, HashSet<string>> recordedByDay = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

    public IReadOnlyList<AssignmentHistoryEntry> History { get; }

    public EmployeeWorkloadHistory(List<AssignmentHistoryEntry> openingHistory = null)
    {
        History = history.AsReadOnly();

        if (openingHistory == null)
            return;

        foreach (var entry in openingHistory)
        {
            if (entry == null)
                throw new ArgumentException("Opening assignments cannot contain null.", nameof(openingHistory));

            if (!TryRecord(entry))
                throw new ArgumentException("Duplicate opening assignment for the same day and request.", nameof(openingHistory));
        }
    }

    internal bool TryRecord(AssignmentHistoryEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        if (!recordedByDay.TryGetValue(entry.DayId, out var requestIds))
        {
            requestIds = new HashSet<string>(StringComparer.Ordinal);
            recordedByDay.Add(entry.DayId, requestIds);
        }

        if (!requestIds.Add(entry.RequestInstanceId))
            return false;

        history.Add(entry);
        return true;
    }

    public int GetAttemptedCount(string employeeId)
    {
        return CountAssignments(employeeId, false);
    }

    public int GetSuitableArrangedCount(string employeeId)
    {
        return CountAssignments(employeeId, true);
    }

    private int CountAssignments(string employeeId, bool requireSuitable)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("An employee ID is required.", nameof(employeeId));

        int count = 0;

        foreach (var entry in history)
        {
            if (!string.Equals(
                entry.HelperId, employeeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!requireSuitable || entry.IsSuitableArranged)
                count++;
        }

        return count;
    }
}