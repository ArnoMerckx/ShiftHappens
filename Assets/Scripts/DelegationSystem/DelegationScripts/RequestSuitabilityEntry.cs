using System;

public sealed class RequestSuitabilityEntry
{
    public string EmployeeId { get; }
    public int Tier { get; }

    public RequestSuitabilityEntry(string employeeId, int tier)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Employee ID is required.", nameof(employeeId));

        if (tier < 0)
            throw new ArgumentOutOfRangeException(nameof(tier));

        EmployeeId = employeeId;
        Tier = tier;
    }
}