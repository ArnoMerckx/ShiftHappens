using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ScheduleValidationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ValidRecords_StoreTheirValues(bool requiresPresence)
    {
        var period = new TimeRange(2, 540, 600);

        var published = new PublishedScheduleEntry("bob", "printer_area", period);

        var approved = new ApprovedScheduleRecord("record", "bob", "printer_area", period, 0, requiresPresence);

        Assert.That(published.EmployeeId, Is.EqualTo("bob"));
        Assert.That(published.LocationId, Is.EqualTo("printer_area"));
        Assert.That(published.Period, Is.EqualTo(period));

        Assert.That(approved.RecordId, Is.EqualTo("record"));
        Assert.That(approved.EmployeeId, Is.EqualTo("bob"));
        Assert.That(approved.LocationId, Is.EqualTo("printer_area"));
        Assert.That(approved.Period, Is.EqualTo(period));
        Assert.That(approved.Priority, Is.EqualTo(0));
        Assert.That(approved.RequiresPresence, Is.EqualTo(requiresPresence));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void MissingIds_AreRejected(string invalid)
    {
        var period = new TimeRange(0, 540, 600);
        var records = new List<ApprovedScheduleRecord>();

        Assert.Throws<ArgumentException>(() =>
            new PublishedScheduleEntry(invalid, "printer_area", period));

        Assert.Throws<ArgumentException>(() =>
            new PublishedScheduleEntry("bob", invalid, period));

        Assert.Throws<ArgumentException>(() =>
            new ApprovedScheduleRecord(
                invalid, "bob", "printer_area", period, 0, false));

        Assert.Throws<ArgumentException>(() =>
            new ApprovedScheduleRecord(
                "record", invalid, "printer_area", period, 0, false));

        Assert.Throws<ArgumentException>(() =>
            new ApprovedScheduleRecord(
                "record", "bob", invalid, period, 0, false));

        Assert.Throws<ArgumentException>(() =>
            ScheduleResolver.ResolveSegments(invalid, period, records));

        Assert.Throws<ArgumentException>(() =>
            ScheduleResolver.GetPresence(
                "bob", invalid, period, records));
    }

    [Test]
    public void DefaultPeriods_AreRejected()
    {
        var records = new List<ApprovedScheduleRecord>();

        Assert.Throws<ArgumentException>(() =>
            new PublishedScheduleEntry(
                "bob", "printer_area", default(TimeRange)));

        Assert.Throws<ArgumentException>(() =>
            new ApprovedScheduleRecord(
                "record", "bob", "printer_area",
                default(TimeRange), 0, false));

        Assert.Throws<ArgumentException>(() =>
            ScheduleResolver.ResolveSegments(
                "bob", default(TimeRange), records));

        Assert.Throws<ArgumentException>(() =>
            ScheduleResolver.GetPresence(
                "bob", "printer_area", default(TimeRange), records));
    }

    [Test]
    public void NegativePriority_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ApprovedScheduleRecord(
                "record", "bob", "printer_area",
                new TimeRange(0, 540, 600), -1, false));
    }

    [Test]
    public void NullRecordLists_AreRejected()
    {
        var period = new TimeRange(0, 540, 600);

        Assert.Throws<ArgumentNullException>(() =>
            ScheduleResolver.ResolveSegments("bob", period, null));

        Assert.Throws<ArgumentNullException>(() =>
            ScheduleResolver.GetPresence(
                "bob", "printer_area", period, null));
    }

    [Test]
    public void NullRecordElements_AreRejected()
    {
        var period = new TimeRange(0, 540, 600);
        var records = new List<ApprovedScheduleRecord> { null };

        Assert.Throws<ArgumentException>(() =>
            ScheduleResolver.ResolveSegments("bob", period, records));

        Assert.Throws<ArgumentException>(() =>
            ScheduleResolver.GetPresence(
                "bob", "printer_area", period, records));
    }

    [Test]
    public void DuplicateRecordIds_AreRejectedBeforeFiltering()
    {
        var period = new TimeRange(0, 540, 600);
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "duplicate", "bob", "printer_area", period, 0, false),

            new ApprovedScheduleRecord(
                "duplicate", "alice", "accounts_office", period, 0, false)
        };

        Assert.Throws<ArgumentException>(() =>
            ScheduleResolver.ResolveSegments("bob", period, records));

        Assert.Throws<ArgumentException>(() =>
            ScheduleResolver.GetPresence(
                "bob", "printer_area", period, records));
    }

    [Test]
    public void Result_SurvivesClearingInputList()
    {
        var period = new TimeRange(0, 540, 600);
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "record", "bob", "printer_area", period, 0, false)
        };

        var segments = ScheduleResolver.ResolveSegments(
            "bob", period, records);

        records.Clear();

        Assert.That(segments.Count, Is.EqualTo(1));
        Assert.That(segments[0].WinningRecords.Count, Is.EqualTo(1));
        Assert.That(segments[0].WinningRecords[0].RecordId, Is.EqualTo("record"));
    }
}