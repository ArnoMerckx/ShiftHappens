using System;

public sealed class EmployeeViewData
{
    public string EmployeeId { get; }
    public string DisplayName { get; }

    public EmployeeViewData(string employeeId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("An employee ID is required.", nameof(employeeId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("An employee name is required.", nameof(displayName));

        EmployeeId = employeeId;
        DisplayName = displayName;
    }
}