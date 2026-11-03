using System;
using System.Collections.Generic;

public static class ScheduleResolver
{
    public static IReadOnlyList<ResolvedScheduleSegment> ResolveSegments(string employeeId, TimeRange query, List<ApprovedScheduleRecord> records)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Employee ID is required.", nameof(employeeId));

        if (query.StartMinute >= query.EndMinute)
            throw new ArgumentException("A nonempty query is required.", nameof(query));

        if (records == null)
            throw new ArgumentNullException(nameof(records));

        var relevant = new List<ApprovedScheduleRecord>();
        var recordIds = new HashSet<string>(StringComparer.Ordinal);
        var boundaries = new SortedSet<int>
        {
            query.StartMinute,
            query.EndMinute
        };

        foreach (var record in records)
        {
            if (record == null)
                throw new ArgumentException("Records cannot contain null.", nameof(records));

            if (!recordIds.Add(record.RecordId))
                throw new ArgumentException("Record IDs must be unique.", nameof(records));

            if (record.EmployeeId != employeeId || !record.Period.Overlaps(query))
                continue;

            relevant.Add(record);

            boundaries.Add(Math.Max(query.StartMinute, record.Period.StartMinute));

            boundaries.Add(Math.Min(query.EndMinute, record.Period.EndMinute));
        }

        var points = new List<int>(boundaries);
        var segments = new List<ResolvedScheduleSegment>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            var period = new TimeRange(query.DayIndex, points[i], points[i + 1]);

            var winners = new List<ApprovedScheduleRecord>();
            int highestPriority = -1;

            foreach (var record in relevant)
            {
                if (!record.Period.Contains(query.DayIndex, period.StartMinute))
                    continue;

                if (record.Priority > highestPriority)
                {
                    highestPriority = record.Priority;
                    winners.Clear();
                    winners.Add(record);
                }
                else if (record.Priority == highestPriority)
                {
                    winners.Add(record);
                }
            }

            winners.Sort((left, right) =>StringComparer.Ordinal.Compare(left.RecordId, right.RecordId));

            segments.Add(new ResolvedScheduleSegment(period, winners));
        }

        return segments.AsReadOnly();
    }

    public static PresenceStatus GetPresence(string employeeId, string locationId, TimeRange query, List<ApprovedScheduleRecord> records)
    {
        if (string.IsNullOrWhiteSpace(locationId))
            throw new ArgumentException("Location ID is required.", nameof(locationId));

        var segments = ResolveSegments(employeeId, query, records);

        bool allSegmentsBlocked = true;
        bool hasSupportedOpportunity = false;

        foreach (var segment in segments)
        {
            string requiredLocation = null;
            bool hasMatchingAssignment = false;

            foreach (var record in segment.WinningRecords)
            {
                if (record.LocationId == locationId)
                    hasMatchingAssignment = true;

                if (!record.RequiresPresence)
                    continue;

                if (requiredLocation != null
                    && requiredLocation != record.LocationId)
                {
                    throw new InvalidOperationException($"Conflicting required locations for {employeeId} " + $"on day {query.DayIndex}, " + $"minute {segment.Period.StartMinute}.");
                }

                requiredLocation = record.LocationId;
            }

            if (requiredLocation != null)
            {
                if (requiredLocation == locationId)
                {
                    allSegmentsBlocked = false;
                    hasSupportedOpportunity = true;
                }
            }
            else
            {
                allSegmentsBlocked = false;

                if (hasMatchingAssignment)
                    hasSupportedOpportunity = true;
            }
        }

        if (allSegmentsBlocked)
            return PresenceStatus.Impossible;

        return hasSupportedOpportunity ? PresenceStatus.Possible : PresenceStatus.Unknown;
    }
}
