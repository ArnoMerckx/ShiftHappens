using System;
using System.Collections.Generic;

public sealed class EmployeeTrustState
{
    private readonly Dictionary<string, int> trustByEmployee = new Dictionary<string, int>(StringComparer.Ordinal);

    private readonly int maximumTrust;

    public IReadOnlyList<string> EmployeeIds { get; }

    public EmployeeTrustState(List<string> employeeIds, ConsequenceRules rules, List<EmployeeTrustEntry> openingTrusts = null)
    {
        if (employeeIds == null)
            throw new ArgumentNullException(nameof(employeeIds));

        if (rules == null)
            throw new ArgumentNullException(nameof(rules));

        var employeeCopy = new List<string>();

        foreach (string employeeId in employeeIds)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                throw new ArgumentException( "Employee IDs cannot be blank.", nameof(employeeIds));
            }

            if (trustByEmployee.ContainsKey(employeeId))
            {
                throw new ArgumentException($"Duplicate employee ID: {employeeId}", nameof(employeeIds));
            }

            trustByEmployee.Add(employeeId, rules.StartingTrust);
            employeeCopy.Add(employeeId);
        }

        if (openingTrusts != null)
        {
            var restoredIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in openingTrusts)
            {
                if (entry == null)
                    throw new ArgumentException("Opening Trust entries cannot contain null.", nameof(openingTrusts));

                if (!trustByEmployee.ContainsKey(entry.EmployeeId))
                    throw new ArgumentException($"Unknown opening employee: {entry.EmployeeId}", nameof(openingTrusts));

                if (!restoredIds.Add(entry.EmployeeId))
                    throw new ArgumentException($"Duplicate opening employee: {entry.EmployeeId}", nameof(openingTrusts));

                if (entry.Trust < 0 || entry.Trust > rules.MaximumTrust)
                    throw new ArgumentOutOfRangeException(nameof(openingTrusts));

                trustByEmployee[entry.EmployeeId] = entry.Trust;
            }

            if (restoredIds.Count != trustByEmployee.Count)
                throw new ArgumentException("Opening Trust must cover exactly the current roster.", nameof(openingTrusts));
        }

        maximumTrust = rules.MaximumTrust;
        EmployeeIds = employeeCopy.AsReadOnly();
    }

    public int GetTrust(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("An employee ID is required.", nameof(employeeId));

        if (!trustByEmployee.TryGetValue(employeeId, out int trust))
            throw new KeyNotFoundException($"Unknown employee: {employeeId}");

        return trust;
    }

    internal int ApplyTrustDelta(string employeeId, int delta)
    {
        int before = GetTrust(employeeId);
        long changed = (long)before + delta;
        int after = (int)Math.Max(0L, Math.Min((long)maximumTrust, changed));

        trustByEmployee[employeeId] = after;
        return after;
    }
}