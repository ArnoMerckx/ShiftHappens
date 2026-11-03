using System;

public sealed class ApprovedScheduleRecord
{
    public string RecordId { get; }
    public string EmployeeId { get; }
    public string LocationId { get; }
    public TimeRange Period { get; }
    public int Priority { get; }
    public bool RequiresPresence { get; }

    public ApprovedScheduleRecord(string recordId, string employeeId, string locationId, TimeRange period, int priority, bool requiresPresence)
    {
        if (string.IsNullOrWhiteSpace(recordId))
            throw new ArgumentException("Record ID is required.", nameof(recordId));

        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Employee ID is required.", nameof(employeeId));

        if (string.IsNullOrWhiteSpace(locationId))
            throw new ArgumentException( "Location ID is required.", nameof(locationId));

        if (period.StartMinute >= period.EndMinute)
            throw new ArgumentException("A nonempty time range is required.", nameof(period));

        if (priority < 0)
            throw new ArgumentOutOfRangeException(nameof(priority));

        RecordId = recordId;
        EmployeeId = employeeId;
        LocationId = locationId;
        Period = period;
        Priority = priority;
        RequiresPresence = requiresPresence;
    }
}