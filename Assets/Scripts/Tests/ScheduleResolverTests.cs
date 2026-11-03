using System.Collections.Generic;
using NUnit.Framework;

public class ScheduleResolverTests
{
    [Test]
    public void NoRecords_PreservesWholeQueryAsGap()
    {
        var segments = ScheduleResolver.ResolveSegments("bob", new TimeRange(0, 570, 705), new List<ApprovedScheduleRecord>());

        Assert.That(segments.Count, Is.EqualTo(1));
        AssertSegment(segments[0], 570, 705);
    }

    [Test]
    public void HigherPriority_OverridesOnlyItsOwnInterval()
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord("ordinary", "bob", "printer_area", new TimeRange(0, 540, 720), 10, false),

            new ApprovedScheduleRecord("dentist", "bob", "offsite", new TimeRange(0, 600, 690), 100, true)
        };

        var segments = ScheduleResolver.ResolveSegments("bob", new TimeRange(0, 570, 705), records);

        Assert.That(segments.Count, Is.EqualTo(3));
        AssertSegment(segments[0], 570, 600, "ordinary");
        AssertSegment(segments[1], 600, 690, "dentist");
        AssertSegment(segments[2], 690, 705, "ordinary");
    }

    [TestCase("alice", 0, 550, 560)]
    [TestCase("bob", 1, 550, 560)]
    [TestCase("bob", 0, 480, 540)]
    [TestCase("bob", 0, 600, 660)]
    public void IrrelevantRecord_DoesNotAffectQuery(
        string employeeId, int day, int start, int end)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord("irrelevant", employeeId, "offsite", new TimeRange(day, start, end), 100, true)
        };

        var segments = ScheduleResolver.ResolveSegments("bob", new TimeRange(0, 540, 600), records);

        Assert.That(segments.Count, Is.EqualTo(1));
        AssertSegment(segments[0], 540, 600);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EqualPriority_KeepsTiesInStableOrder(bool reverse)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord("z_record", "bob", "printer_area", new TimeRange(0, 540, 600), 10, false),

            new ApprovedScheduleRecord("a_record", "bob", "supplies_area", new TimeRange(0, 540, 600), 10, false)
        };

        if (reverse)
            records.Reverse();

        var segments = ScheduleResolver.ResolveSegments("bob", new TimeRange(0, 540, 600), records);

        Assert.That(segments.Count, Is.EqualTo(1));
        AssertSegment(segments[0], 540, 600, "a_record", "z_record");
    }

    private static void AssertSegment(ResolvedScheduleSegment segment, int start, int end, params string[] recordIds)
    {
        Assert.That(segment.Period.DayIndex, Is.EqualTo(0));
        Assert.That(segment.Period.StartMinute, Is.EqualTo(start));
        Assert.That(segment.Period.EndMinute, Is.EqualTo(end));
        Assert.That(segment.WinningRecords.Count,Is.EqualTo(recordIds.Length));

        for (int i = 0; i < recordIds.Length; i++)
        {
            Assert.That(segment.WinningRecords[i].RecordId,Is.EqualTo(recordIds[i]));
        }
    }
}