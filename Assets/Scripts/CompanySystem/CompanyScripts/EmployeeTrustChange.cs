using System;

public sealed class EmployeeTrustChange
{
    public string EmployeeId { get; }
    public int Before { get; }
    public int After { get; }
    public int Delta => After - Before;

    public EmployeeTrustChange(string employeeId, int before, int after)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("An employee ID is required.", nameof(employeeId));

        if (before < 0)
            throw new ArgumentOutOfRangeException(nameof(before));

        if (after < 0)
            throw new ArgumentOutOfRangeException(nameof(after));

        EmployeeId = employeeId;
        Before = before;
        After = after;
    }
}