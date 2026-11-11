using System;
using System.Collections.Generic;

public static class DelegationAvailability
{
    public static IReadOnlyList<TimeRange> ResolveAvailablePeriods(string employeeId, DayScheduleData day)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));

        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Employee ID is required.", nameof(employeeId));

        bool knownEmployee = false;

        foreach (string id in day.EmployeeIds)
        {
            if (string.Equals(id, employeeId, StringComparison.Ordinal))
            {
                knownEmployee = true;
                break;
            }
        }

        if (!knownEmployee)
            throw new ArgumentException("Employee must belong to this day's roster.", nameof(employeeId));

        var records = new List<ApprovedScheduleRecord>(day.WorldApprovedRecords);
        var pieces = new List<TimeRange>();

        foreach (var availability in day.OrdinaryAvailability)
        {
            if (availability.EmployeeId != employeeId)
                continue;

            var segments = ScheduleResolver.ResolveSegments(employeeId, availability.Period, records);

            foreach (var segment in segments)
            {
                bool blocked = false;

                foreach (var record in segment.WinningRecords)
                {
                    if (record.BlocksDelegation)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                    pieces.Add(segment.Period);
            }
        }

        pieces.Sort((left, right) =>
        {
            int startOrder = left.StartMinute.CompareTo(right.StartMinute);

            return startOrder != 0 ? startOrder : left.EndMinute.CompareTo(right.EndMinute);
        });

        var merged = new List<TimeRange>();

        foreach (var piece in pieces)
        {
            if (merged.Count == 0)
            {
                merged.Add(piece);
                continue;
            }

            int lastIndex = merged.Count - 1;
            TimeRange previous = merged[lastIndex];

            if (piece.StartMinute <= previous.EndMinute)
            {
                merged[lastIndex] = new TimeRange(previous.DayIndex, previous.StartMinute, Math.Max(previous.EndMinute, piece.EndMinute));
            }
            else
            {
                merged.Add(piece);
            }
        }

        return merged.AsReadOnly();
    }

    public static bool CoversWindow(string employeeId, TimeRange window, DayScheduleData day)
    {
        if (window.StartMinute >= window.EndMinute)
            throw new ArgumentException( "A nonempty help window is required.", nameof(window));

        var periods = ResolveAvailablePeriods(employeeId, day);

        if (window.DayIndex != day.Settings.DayIndex || window.StartMinute < day.Settings.WorkHours.StartMinute || window.EndMinute > day.Settings.WorkHours.EndMinute)
        {
            return false;
        }

        foreach (var period in periods)
        {
            if (period.StartMinute <= window.StartMinute && period.EndMinute >= window.EndMinute)
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasAvailabilityBeforeClosing(string employeeId, GameTime submittedAt, DayScheduleData day)
    {
        var periods = ResolveAvailablePeriods(employeeId, day);

        long submitted = submittedAt.TotalMilliseconds;
        long closing = day.Settings.ClosesAt.TotalMilliseconds;

        if (submitted < day.Settings.OpensAt.TotalMilliseconds || submitted >= closing)
        {
            return false;
        }

        foreach (var period in periods)
        {
            long dayStart = (long)period.DayIndex * GameTime.MillisecondsPerDay;

            long periodStart = dayStart + period.StartMinute * GameTime.MillisecondsPerMinute;

            long periodEnd = dayStart + period.EndMinute * GameTime.MillisecondsPerMinute;

            long overlapStart = Math.Max(submitted, periodStart);
            long overlapEnd = Math.Min(closing, periodEnd);

            if (overlapStart < overlapEnd)
                return true;
        }

        return false;
    }
}