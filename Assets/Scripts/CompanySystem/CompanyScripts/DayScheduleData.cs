using System;
using System.Collections.Generic;

public sealed class DayScheduleData
{
    public WorkdaySettings Settings { get; }
    public IReadOnlyList<string> EmployeeIds { get; }
    public IReadOnlyList<ApprovedScheduleRecord> WorldApprovedRecords { get; }
    public HrKnowledge HrKnowledge { get; }

    public IReadOnlyList<EmployeeViewData> EmployeeRoster { get; }

    public IReadOnlyList<EmployeeAvailability> OrdinaryAvailability { get; }

    public DayScheduleData(WorkdaySettings settings, List<string> employeeIds,List<ApprovedScheduleRecord> worldApprovedRecords, List<ApprovedScheduleRecord> hrApprovedRecords, List<Claim> hrKnownClaims, List<EmployeeViewData> employeePresentation = null, List<EmployeeAvailability> ordinaryAvailability = null)
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

        OrdinaryAvailability = BuildOrdinaryAvailability(ordinaryAvailability, employeeSet, settings).AsReadOnly();

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

        ValidateDelegationCommitments(worldCopy, employeeCopy, settings.DayIndex);

        Settings = settings;
        EmployeeIds = employeeCopy.AsReadOnly();
        WorldApprovedRecords = worldCopy.AsReadOnly();
        EmployeeRoster = BuildEmployeeRoster(employeeCopy, employeePresentation).AsReadOnly();
        HrKnowledge = knowledge;
    }

    private static List<EmployeeAvailability> BuildOrdinaryAvailability(List<EmployeeAvailability> entries, HashSet<string> employeeIds, WorkdaySettings settings)
    {
        var copy = new List<EmployeeAvailability>();

        if (entries == null)
            return copy;

        foreach (var entry in entries)
        {
            if (entry == null)
                throw new ArgumentException("Availability entries cannot contain null.", nameof(entries));

            if (!employeeIds.Contains(entry.EmployeeId))
                throw new ArgumentException($"Unknown employee in availability: {entry.EmployeeId}", nameof(entries));

            TimeRange period = entry.Period;

            if (period.DayIndex != settings.DayIndex)
                throw new ArgumentException("Availability must belong to this day.", nameof(entries));

            if (period.StartMinute < settings.WorkHours.StartMinute || period.EndMinute > settings.WorkHours.EndMinute)
            {
                throw new ArgumentException("Availability must be within work hours.", nameof(entries));
            }

            copy.Add(entry);
        }

        return copy;
    }

    private static void ValidateDelegationCommitments(List<ApprovedScheduleRecord> records, List<string> employeeIds, int dayIndex)
    {
        var fullDay = new TimeRange(dayIndex, 0, 1440);

        foreach (string employeeId in employeeIds)
        {
            var segments = ScheduleResolver.ResolveSegments(employeeId, fullDay, records);

            foreach (var segment in segments)
            {
                bool? blocksDelegation = null;

                foreach (var record in segment.WinningRecords)
                {
                    if (blocksDelegation.HasValue &&
                        blocksDelegation.Value != record.BlocksDelegation)
                    {
                        throw new InvalidOperationException(
                            $"Conflicting delegation commitments for {employeeId} " +
                            $"on day {dayIndex}, " +
                            $"minute {segment.Period.StartMinute}.");
                    }

                    blocksDelegation = record.BlocksDelegation;
                }
            }
        }
    }

    private static void ValidateSchedule(List<ApprovedScheduleRecord> records, int dayIndex)
    {
        var fullDay = new TimeRange(dayIndex, 0, 1440);

        foreach (var record in records)
        {
            ScheduleResolver.GetPresence(record.EmployeeId,  record.LocationId, fullDay, records);
        }
    }

    private static List<EmployeeViewData> BuildEmployeeRoster(List<string> employeeIds, List<EmployeeViewData> employeePresentation)
    {
        var roster = new List<EmployeeViewData>();

        if (employeePresentation == null)
        {
            foreach (string id in employeeIds)
                roster.Add(new EmployeeViewData(id, id));

            return roster;
        }

        var byId = new Dictionary<string, EmployeeViewData>(
            StringComparer.Ordinal);

        foreach (EmployeeViewData employee in employeePresentation)
        {
            if (employee == null)
                throw new ArgumentException("Employee presentation cannot contain null.", nameof(employeePresentation));

            if (byId.ContainsKey(employee.EmployeeId))
                throw new ArgumentException("Employee presentation contains a duplicate ID.", nameof(employeePresentation));

            byId.Add(employee.EmployeeId, employee);
        }

        if (byId.Count != employeeIds.Count)
            throw new ArgumentException("Employee presentation must match the day roster.", nameof(employeePresentation));

        foreach (string id in employeeIds)
        {
            if (!byId.TryGetValue(id, out EmployeeViewData employee))
                throw new ArgumentException("Employee presentation must match the day roster.", nameof(employeePresentation));

            roster.Add(new EmployeeViewData(id, employee.DisplayName, employee.Role, employee.Bio, employee.InterestsAndExperience));
        }

        return roster;
    }
}