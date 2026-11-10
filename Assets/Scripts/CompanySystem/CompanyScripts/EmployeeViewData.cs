using System;

public sealed class EmployeeViewData
{
    public string EmployeeId { get; }
    public string DisplayName { get; }

    public EmployeeRole Role { get; }
    public string Bio { get; }
    public string InterestsAndExperience { get; }

    public EmployeeViewData(string employeeId, string displayName, EmployeeRole role = EmployeeRole.None, string bio = "", string interestsAndExperience = "")
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("An employee ID is required.", nameof(employeeId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("An employee name is required.", nameof(displayName));

        if (!Enum.IsDefined(typeof(EmployeeRole), role))
            throw new ArgumentOutOfRangeException(nameof(role));

        EmployeeId = employeeId;
        DisplayName = displayName;
        Role = role;
        Bio = bio ?? string.Empty;
        InterestsAndExperience = interestsAndExperience ?? string.Empty;
    }
}