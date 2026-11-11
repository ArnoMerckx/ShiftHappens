using System;
using System.Collections.Generic;
using NUnit.Framework;

public class DelegationAvailabilityQueryTests
{
    private static EmployeeAvailability Available(int start, int end, string employee = "dave")
    {
        return new EmployeeAvailability(employee, new TimeRange(3, start, end));
    }

    private static ApprovedScheduleRecord Record(string id, int start, int end,
        bool blocks = true, int priority = 100, bool requiresPresence = false,
        string employee = "dave")
    {
        return new ApprovedScheduleRecord(id, employee, "office",
            new TimeRange(3, start, end), priority, requiresPresence, blocks);
    }

    private static DayScheduleData Day(List<EmployeeAvailability> availability = null,
        List<ApprovedScheduleRecord> records = null, int closing = 1020)
    {
        return new DayScheduleData(
            new WorkdaySettings("query_day", "Query day", 3, 540, closing, 40),
            new List<string> { "dave", "steve" },
            records ?? new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>(),
            ordinaryAvailability: availability);
    }

    private static void AssertPeriods(IReadOnlyList<TimeRange> actual, params int[] bounds)
    {
        Assert.That(actual.Count * 2, Is.EqualTo(bounds.Length));
        for (int i = 0; i < actual.Count; i++)
        {
            Assert.That(actual[i].DayIndex, Is.EqualTo(3));
            Assert.That(actual[i].StartMinute, Is.EqualTo(bounds[i * 2]));
            Assert.That(actual[i].EndMinute, Is.EqualTo(bounds[i * 2 + 1]));
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("unknown")]
    [TestCase("Dave")]
    public void AllQueries_RejectInvalidEmployeeIdentity(string employee)
    {
        var day = Day();
        Assert.Throws<ArgumentException>(() =>
            DelegationAvailability.ResolveAvailablePeriods(employee, day));
        Assert.Throws<ArgumentException>(() =>
            DelegationAvailability.CoversWindow(employee, new TimeRange(3, 660, 720), day));
        Assert.Throws<ArgumentException>(() =>
            DelegationAvailability.HasAvailabilityBeforeClosing(employee, new GameTime(3, 900), day));
    }

    [Test]
    public void AllQueries_RejectMissingDay()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DelegationAvailability.ResolveAvailablePeriods("dave", null));
        Assert.Throws<ArgumentNullException>(() =>
            DelegationAvailability.CoversWindow("dave", new TimeRange(3, 660, 720), null));
        Assert.Throws<ArgumentNullException>(() =>
            DelegationAvailability.HasAvailabilityBeforeClosing("dave", new GameTime(3, 900), null));
    }

    [Test]
    public void MissingPositiveCoverage_IsUnknownEvenWithNonblockingAssignment()
    {
        var day = Day(records: new List<ApprovedScheduleRecord>
        {
            Record("ordinary_assignment", 540, 1020, false, requiresPresence: true)
        });
        Assert.That(DelegationAvailability.ResolveAvailablePeriods("dave", day), Is.Empty);
        Assert.That(DelegationAvailability.CoversWindow("dave", new TimeRange(3, 660, 720), day), Is.False);
        Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave", new GameTime(3, 900), day), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Union_MergesUnsortedOverlappingDuplicateAndAdjacentCoverage(bool reverse)
    {
        var entries = new List<EmployeeAvailability>
        {
            Available(660, 720), Available(540, 600), Available(590, 660),
            Available(540, 600), Available(800, 850), Available(540, 1020, "steve")
        };
        if (reverse) entries.Reverse();
        var day = Day(entries);
        AssertPeriods(DelegationAvailability.ResolveAvailablePeriods("dave", day), 540, 720, 800, 850);
        Assert.That(DelegationAvailability.CoversWindow("dave", new TimeRange(3, 540, 720), day), Is.True);
        Assert.That(DelegationAvailability.CoversWindow("dave", new TimeRange(3, 700, 810), day), Is.False);
    }

    [TestCase(600, 660, true)]
    [TestCase(720, 780, true)]
    [TestCase(659, 660, true)]
    [TestCase(660, 661, false)]
    [TestCase(660, 720, false)]
    [TestCase(690, 750, false)]
    [TestCase(719, 720, false)]
    public void FixedWindow_RequiresFullCoverageWithHalfOpenBlockerBoundaries(
        int blockStart, int blockEnd, bool expected)
    {
        var day = Day(new List<EmployeeAvailability> { Available(540, 1020) },
            new List<ApprovedScheduleRecord> { Record("commitment", blockStart, blockEnd) });
        Assert.That(DelegationAvailability.CoversWindow("dave", new TimeRange(3, 660, 720), day),
            Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HigherPriorityWinningFlag_OverridesLowerFlagInEitherInputOrder(bool higherBlocks)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            Record("lower", 600, 780, !higherBlocks, 10),
            Record("higher", 660, 720, higherBlocks, 20)
        };
        foreach (bool reverse in new[] { false, true })
        {
            var ordered = new List<ApprovedScheduleRecord>(records);
            if (reverse) ordered.Reverse();
            var day = Day(new List<EmployeeAvailability> { Available(540, 1020) }, ordered);
            Assert.That(DelegationAvailability.CoversWindow("dave", new TimeRange(3, 660, 720), day),
                Is.EqualTo(!higherBlocks));
            Assert.That(DelegationAvailability.CoversWindow("dave", new TimeRange(3, 600, 660), day),
                Is.EqualTo(higherBlocks));
        }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void RequiresPresence_DoesNotReplaceBlocksDelegation(bool requiresPresence, bool blocks)
    {
        var day = Day(new List<EmployeeAvailability> { Available(540, 1020) },
            new List<ApprovedScheduleRecord>
            {
                Record("a", 660, 720, blocks, requiresPresence: requiresPresence),
                Record("b", 660, 720, blocks, requiresPresence: requiresPresence)
            });
        Assert.That(DelegationAvailability.CoversWindow("dave", new TimeRange(3, 660, 720), day),
            Is.EqualTo(!blocks));
    }

    [Test]
    public void WorldBlockerAppliesWithoutHrKnowledge_AndOtherEmployeeDoesNotBlock()
    {
        var records = new List<ApprovedScheduleRecord>
        {
            Record("inspection", 630, 645),
            Record("steve_busy", 540, 1020, employee: "steve")
        };
        var day = Day(new List<EmployeeAvailability> { Available(540, 1020) }, records);
        AssertPeriods(DelegationAvailability.ResolveAvailablePeriods("dave", day), 540, 630, 645, 1020);
        Assert.That(DelegationAvailability.CoversWindow("dave", new TimeRange(3, 630, 645), day), Is.False);
        Assert.That(day.HrKnowledge.ApprovedRecords, Is.Empty);
        Assert.That(day.HrKnowledge.KnownClaims, Is.Empty);
        Assert.That(day.WorldApprovedRecords.Count, Is.EqualTo(2));
        Assert.That(day.OrdinaryAvailability.Count, Is.EqualTo(1));
    }

    [Test]
    public void FixedWindow_RejectsDefaultAndDoesNotBorrowOtherDayOrOutsideWorkHours()
    {
        var day = Day(new List<EmployeeAvailability> { Available(540, 1020) });
        Assert.Throws<ArgumentException>(() =>
            DelegationAvailability.CoversWindow("dave", default(TimeRange), day));
        foreach (var window in new[]
        {
            new TimeRange(2, 660, 720), new TimeRange(4, 660, 720),
            new TimeRange(3, 539, 600), new TimeRange(3, 1000, 1021)
        })
            Assert.That(DelegationAvailability.CoversWindow("dave", window, day), Is.False);
    }

    [TestCase(-1, true)]
    [TestCase(0, false)]
    [TestCase(1, false)]
    public void EndOfShift_OneMillisecondBeforeAvailableIntervalEndsStillQualifies(long offset, bool expected)
    {
        var day = Day(new List<EmployeeAvailability> { Available(900, 960) });
        var submitted = new GameTime(new GameTime(3, 960).TotalMilliseconds + offset);
        Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave", submitted, day),
            Is.EqualTo(expected));
    }

    [Test]
    public void EndOfShift_FutureAvailabilityQualifiesWhileEarlierCoverageDoesNot()
    {
        var day = Day(new List<EmployeeAvailability> { Available(900, 960) });
        Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave", new GameTime(3, 860), day), Is.True);
        Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave", new GameTime(3, 970), day), Is.False);
    }

    [TestCase(-1, true)]
    [TestCase(0, false)]
    [TestCase(1, false)]
    public void Closing_UsesExactTimestamp(long offset, bool expected)
    {
        var day = Day(new List<EmployeeAvailability> { Available(540, 1020) });
        var submitted = new GameTime(day.Settings.ClosesAt.TotalMilliseconds + offset);
        Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave", submitted, day), Is.EqualTo(expected));
    }

    [Test]
    public void SubmissionOutsideDayOrBeforeOpening_DoesNotBorrowCoverage()
    {
        var day = Day(new List<EmployeeAvailability> { Available(540, 1020) });
        foreach (var submitted in new[]
        {
            new GameTime(2, 900), new GameTime(4, 900),
            new GameTime(day.Settings.OpensAt.TotalMilliseconds - 1)
        })
            Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave", submitted, day), Is.False);
        Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave", day.Settings.OpensAt, day), Is.True);
    }

    [Test]
    public void MidnightClosing_SupportsEndMinute1440AndLastMillisecond()
    {
        var day = Day(new List<EmployeeAvailability> { Available(1439, 1440) }, closing: 1440);
        Assert.That(DelegationAvailability.CoversWindow("dave", new TimeRange(3, 1439, 1440), day), Is.True);
        Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave",
            new GameTime(day.Settings.ClosesAt.TotalMilliseconds - 1), day), Is.True);
        Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave", day.Settings.ClosesAt, day), Is.False);
    }

    [Test]
    public void EndOfShift_CannotUseCoverageRemovedByWinningCommitment()
    {
        var day = Day(new List<EmployeeAvailability> { Available(900, 960) },
            new List<ApprovedScheduleRecord> { Record("busy", 900, 960) });
        Assert.That(DelegationAvailability.HasAvailabilityBeforeClosing("dave", new GameTime(3, 860), day), Is.False);
    }
}
