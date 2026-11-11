using System;

public sealed class EmployeeAvailability
{
    public string EmployeeId { get; }
    public TimeRange Period { get; }

    public EmployeeAvailability(string employeeId, TimeRange period)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Employee ID is required.", nameof(employeeId));

        if (period.StartMinute >= period.EndMinute)
            throw new ArgumentException("A nonempty availability period is required.", nameof(period));

        EmployeeId = employeeId;
        Period = period;
    }
}