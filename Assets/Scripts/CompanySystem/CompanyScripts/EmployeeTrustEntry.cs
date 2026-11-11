using System;

public sealed class EmployeeTrustEntry
{
    public string EmployeeId { get; }
    public int Trust { get; }

    public EmployeeTrustEntry(string employeeId, int trust)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("An employee ID is required.", nameof(employeeId));

        if (trust < 0)
            throw new ArgumentOutOfRangeException(nameof(trust));

        EmployeeId = employeeId;
        Trust = trust;
    }
}