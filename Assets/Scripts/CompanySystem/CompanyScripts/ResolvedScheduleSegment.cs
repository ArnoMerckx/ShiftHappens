using System;
using System.Collections.Generic;

public sealed class ResolvedScheduleSegment
{
    public TimeRange Period { get; }

    public IReadOnlyList<ApprovedScheduleRecord> WinningRecords { get; }

    internal ResolvedScheduleSegment(TimeRange period, List<ApprovedScheduleRecord> winningRecords)
    {
        if (period.StartMinute >= period.EndMinute)
            throw new ArgumentException("A nonempty time range is required.", nameof(period));

        if (winningRecords == null)
            throw new ArgumentNullException(nameof(winningRecords));

        var records = new List<ApprovedScheduleRecord>();

        foreach (var record in winningRecords)
        {
            if (record == null)
                throw new ArgumentException("Records cannot contain null.", nameof(winningRecords));

            records.Add(record);
        }

        Period = period;
        WinningRecords = records.AsReadOnly();
    }
}
