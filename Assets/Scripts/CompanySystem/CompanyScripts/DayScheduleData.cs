using System;
using System.Collections.Generic;

public sealed class DayScheduleData
{
    public WorkdaySettings Settings { get; }
    public IReadOnlyList<string> EmployeeIds { get; }
    public IReadOnlyList<ApprovedScheduleRecord> WorldApprovedRecords { get; }
    public HrKnowledge HrKnowledge { get; }

    public DayScheduleData(WorkdaySettings settings, List<string> employeeIds,List<ApprovedScheduleRecord> worldApprovedRecords, List<ApprovedScheduleRecord> hrApprovedRecords, List<Claim> hrKnownClaims)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (employeeIds == null)
            throw new ArgumentNullException(nameof(employeeIds));

        if (worldApprovedRecords == null)
        {
            throw new ArgumentNullException(nameof(worldApprovedRecords));
        }

        var employeeCopy = new List<string>();
        var employeeSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (string employeeId in employeeIds)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                throw new ArgumentException("Employee IDs cannot be blank.", nameof(employeeIds));
            }

            if (!employeeSet.Add(employeeId))
            {
                throw new ArgumentException($"Duplicate employee ID: {employeeId}", nameof(employeeIds));
            }

            employeeCopy.Add(employeeId);
        }

        var worldCopy = new List<ApprovedScheduleRecord>();
        var worldById = new Dictionary<string, ApprovedScheduleRecord>(StringComparer.Ordinal);

        foreach (var record in worldApprovedRecords)
        {
            if (record == null)
            {
                throw new ArgumentException("World records cannot contain null.", nameof(worldApprovedRecords));
            }

            if (!employeeSet.Contains(record.EmployeeId))
            {
                throw new ArgumentException($"Unknown employee in world record: {record.RecordId}", nameof(worldApprovedRecords));
            }

            if (record.Period.DayIndex != settings.DayIndex)
            {
                throw new ArgumentException($"World record belongs to another day: {record.RecordId}", nameof(worldApprovedRecords));
            }

            if (worldById.ContainsKey(record.RecordId))
            {
                throw new ArgumentException($"Duplicate world record ID: {record.RecordId}", nameof(worldApprovedRecords));
            }

            worldById.Add(record.RecordId, record);
            worldCopy.Add(record);
        }

        var knowledge = new HrKnowledge(
            hrApprovedRecords, hrKnownClaims);

        foreach (var record in knowledge.ApprovedRecords)
        {
            if (!worldById.TryGetValue(record.RecordId, out var worldRecord) || !ReferenceEquals(record, worldRecord))
            {
                throw new ArgumentException("HR-approved records must be selected from world records.", nameof(hrApprovedRecords));
            }
        }

        foreach (var claim in knowledge.KnownClaims)
        {
            if (!employeeSet.Contains(claim.EmployeeId))
            {
                throw new ArgumentException($"Unknown employee in HR claim: {claim.ClaimId}", nameof(hrKnownClaims));
            }
        }

        ValidateSchedule(worldCopy, settings.DayIndex);
        ValidateSchedule(new List<ApprovedScheduleRecord>(knowledge.ApprovedRecords), settings.DayIndex);

        Settings = settings;
        EmployeeIds = employeeCopy.AsReadOnly();
        WorldApprovedRecords = worldCopy.AsReadOnly();
        HrKnowledge = knowledge;
    }

    private static void ValidateSchedule(List<ApprovedScheduleRecord> records, int dayIndex)
    {
        var fullDay = new TimeRange(dayIndex, 0, 1440);

        foreach (var record in records)
        {
            ScheduleResolver.GetPresence(record.EmployeeId,  record.LocationId, fullDay, records);
        }
    }
}