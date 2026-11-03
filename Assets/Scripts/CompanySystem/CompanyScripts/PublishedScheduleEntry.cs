using System;

public sealed class PublishedScheduleEntry
{
    public string EmployeeId { get; }
    public string LocationId { get; }
    public TimeRange Period { get; }

    public PublishedScheduleEntry(string employeeId, string locationId, TimeRange period)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Employee ID is required.", nameof(employeeId));

        if (string.IsNullOrWhiteSpace(locationId))
            throw new ArgumentException("Location ID is required.", nameof(locationId));

        if (period.StartMinute >= period.EndMinute)
            throw new ArgumentException("A nonempty time range is required.", nameof(period));

        EmployeeId = employeeId;
        LocationId = locationId;
        Period = period;
    }
}