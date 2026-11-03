using System;
using System.Collections.Generic;
using NUnit.Framework;

public class PresenceTests
{
    [TestCase("bob", "printer_area", 635, 640, PresenceStatus.Impossible)]
    [TestCase("bob", "break_room", 575, 580, PresenceStatus.Unknown)]
    [TestCase("alice", "break_room", 575, 580, PresenceStatus.Impossible)]
    [TestCase("alice", "accounts_office", 575, 580, PresenceStatus.Possible)]
    [TestCase("dave", "printer_area", 635, 640, PresenceStatus.Possible)]
    [TestCase("dave", "it_office", 635, 640, PresenceStatus.Unknown)]
    [TestCase("karen", "supplies_area", 815, 820, PresenceStatus.Possible)]
    [TestCase("steve", "supplies_area", 815, 820, PresenceStatus.Unknown)]
    [TestCase("bob", "printer_area", 690, 700, PresenceStatus.Unknown)]
    [TestCase("bob", "printer_area", 599, 601, PresenceStatus.Unknown)]
    public void FirstDay_UsesOnlyApplicableApprovedRecords(string employeeId, string locationId, int start, int end, PresenceStatus expected)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord("dentist", "bob", "offsite", new TimeRange(0, 600, 690), 100, true),

            new ApprovedScheduleRecord( "meeting", "alice", "accounts_office", new TimeRange(0, 565, 590), 100, true),

            new ApprovedScheduleRecord("inspection", "dave", "printer_area",  new TimeRange(0, 630, 645), 100, false),

            new ApprovedScheduleRecord("collection", "karen", "supplies_area", new TimeRange(0, 810, 825), 100, false)
        };

        var result = ScheduleResolver.GetPresence(employeeId, locationId, new TimeRange(0, start, end), records);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(600, 630, PresenceStatus.Unknown)]
    [TestCase(630, 660, PresenceStatus.Unknown)]
    [TestCase(600, 660, PresenceStatus.Impossible)]
    public void Absence_RequiresWholeWindowCoverageToExclude(int start, int end, PresenceStatus expected)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord("absence", "bob", "offsite", new TimeRange(0, start, end), 100, true)
        };

        var result = ScheduleResolver.GetPresence("bob", "printer_area", new TimeRange(0, 600, 660), records);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("printer_area", PresenceStatus.Possible)]
    [TestCase("it_office", PresenceStatus.Unknown)]
    public void HigherPriorityAssignment_SupersedesLowerRestriction(string locationId, PresenceStatus expected)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord("office", "dave", "it_office", new TimeRange(0, 540, 720), 10, true),

            new ApprovedScheduleRecord("inspection", "dave", "printer_area", new TimeRange(0, 630, 645), 100, false)
        };

        var result = ScheduleResolver.GetPresence("dave", locationId, new TimeRange(0, 635, 640), records);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConflictingRequirements_ThrowEvenAfterPossibleSegment(bool reverse)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord("early", "bob", "break_room", new TimeRange(0, 540, 550), 100, false),

            new ApprovedScheduleRecord("required_a", "bob", "break_room", new TimeRange(0, 550, 560), 100, true),

            new ApprovedScheduleRecord("required_b", "bob", "offsite", new TimeRange(0, 550, 560), 100, true)
        };

        if (reverse)
            records.Reverse();

        Assert.Throws<InvalidOperationException>(() => ScheduleResolver.GetPresence("bob", "break_room", new TimeRange(0, 540, 560), records));
    }

    [Test]
    public void ApprovedOffsiteLunch_ExcludesOfficeDuringLunch()
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord("test_lunch", "bob", "offsite", new TimeRange(0, 720, 780), 100, true)
        };

        var result = ScheduleResolver.GetPresence("bob", "printer_area", new TimeRange(0, 730, 740), records);

        Assert.That(result, Is.EqualTo(PresenceStatus.Impossible));
    }
}