using System;
using System.Collections.Generic;
using NUnit.Framework;

public class DayScheduleDataTests
{
    private WorkdaySettings settings;
    private List<string> employees;
    private List<ApprovedScheduleRecord> world;
    private List<ApprovedScheduleRecord> hr;
    private List<Claim> claims;

    [SetUp]
    public void SetUp()
    {
        settings = new WorkdaySettings(
            "monday", "Monday", 0, 540, 1020, 20);

        employees = new List<string> { "bob" };
        world = new List<ApprovedScheduleRecord> { Record() };
        hr = new List<ApprovedScheduleRecord>();
        claims = new List<Claim>();
    }

    private static ApprovedScheduleRecord Record(
        string id = "absence",
        string location = "offsite",
        int priority = 100)
    {
        return new ApprovedScheduleRecord(
            id, "bob", location,
            new TimeRange(0, 540, 600), priority, true);
    }

    private DayScheduleData Create()
    {
        return new DayScheduleData(
            settings, employees, world, hr, claims);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void NullInput_IsRejected(int input)
    {
        switch (input)
        {
            case 0: settings = null; break;
            case 1: employees = null; break;
            case 2: world = null; break;
            case 3: hr = null; break;
            case 4: claims = null; break;
        }

        Assert.Throws<ArgumentNullException>(() => Create());
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void BlankEmployeeId_IsRejected(string id)
    {
        employees.Add(id);

        Assert.Throws<ArgumentException>(() => Create());
    }

    [Test]
    public void DuplicateEmployeeId_IsRejected()
    {
        employees.Add("bob");

        Assert.Throws<ArgumentException>(() => Create());
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void InvalidWorldEntry_IsRejected(int problem)
    {
        switch (problem)
        {
            case 0:
                world.Add(null);
                break;

            case 1:
                world.Add(Record());
                break;

            case 2:
                world.Add(new ApprovedScheduleRecord(
                    "unknown", "unknown_employee", "offsite",
                    new TimeRange(0, 540, 600), 100, true));
                break;

            case 3:
                world.Add(new ApprovedScheduleRecord(
                    "wrong_day", "bob", "offsite",
                    new TimeRange(1, 540, 600), 100, true));
                break;
        }

        Assert.Throws<ArgumentException>(() => Create());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HrRecord_MustBeCanonicalWorldObject(bool sameId)
    {
        hr.Add(sameId
            ? Record(location: "printer_area")
            : Record(id: "missing"));

        Assert.Throws<ArgumentException>(() => Create());
    }

    [Test]
    public void WorldRecords_DoNotAutomaticallyBecomeHrKnowledge()
    {
        var snapshot = Create();

        Assert.That(snapshot.WorldApprovedRecords.Count, Is.EqualTo(1));
        Assert.That(snapshot.HrKnowledge.ApprovedRecords, Is.Empty);
        Assert.That(snapshot.HrKnowledge.KnownClaims, Is.Empty);
    }

    [Test]
    public void KnownClaim_RemainsSeparateAndMayDescribeEarlierDay()
    {
        settings = new WorkdaySettings(
            "tuesday", "Tuesday", 1, 540, 1020, 20);

        world.Clear();

        var custody = new CustodyClaim(
            "handover", "bob", "carton", "supplies_area", 0, 530);

        claims.Add(custody);

        var snapshot = Create();

        Assert.That(snapshot.WorldApprovedRecords, Is.Empty);
        Assert.That(snapshot.HrKnowledge.ApprovedRecords, Is.Empty);
        Assert.That(
            snapshot.HrKnowledge.KnownClaims[0],
            Is.SameAs(custody));
    }

    [Test]
    public void Snapshot_CopiesInputCollectionMembership()
    {
        ApprovedScheduleRecord record = world[0];
        hr.Add(record);

        var claim = new WhereaboutsClaim(
            "claim", "bob", "printer_area",
            new TimeRange(0, 630, 645), false);

        claims.Add(claim);

        var snapshot = Create();

        employees.Clear();
        world.Clear();
        hr.Clear();
        claims.Clear();

        Assert.That(snapshot.Settings, Is.SameAs(settings));
        Assert.That(snapshot.EmployeeIds, Is.EqualTo(new[] { "bob" }));
        Assert.That(snapshot.WorldApprovedRecords.Count, Is.EqualTo(1));
        Assert.That(snapshot.WorldApprovedRecords[0], Is.SameAs(record));
        Assert.That(
            snapshot.HrKnowledge.ApprovedRecords[0],
            Is.SameAs(record));
        Assert.That(
            snapshot.HrKnowledge.KnownClaims[0],
            Is.SameAs(claim));
    }

    [Test]
    public void ExposedCollections_RejectMutation()
    {
        var snapshot = Create();

        Assert.Throws<NotSupportedException>(
            () => ((IList<string>)snapshot.EmployeeIds).Add("alice"));

        Assert.Throws<NotSupportedException>(
            () => ((IList<ApprovedScheduleRecord>)
                snapshot.WorldApprovedRecords).Clear());
    }

    [Test]
    public void ConflictingWorldRequirements_AreRejected()
    {
        world.Add(Record(
            id: "printer", location: "printer_area"));

        Assert.Throws<InvalidOperationException>(() => Create());
    }

    [Test]
    public void HrConflicts_AreCheckedWithoutUnknownWorldOverride()
    {
        world.Add(Record(
            id: "printer", location: "printer_area"));

        world.Add(Record(
            id: "override", location: "accounts_office", priority: 200));

        hr.Add(world[0]);
        hr.Add(world[1]);

        Assert.That(
            ScheduleResolver.GetPresence(
                "bob", "accounts_office",
                new TimeRange(0, 540, 600), world),
            Is.EqualTo(PresenceStatus.Possible));

        Assert.Throws<InvalidOperationException>(() => Create());
    }

    [Test]
    public void EmployeeIds_AreCaseSensitive()
    {
        world.Clear();
        world.Add(new ApprovedScheduleRecord(
            "wrong_case", "Bob", "offsite",
            new TimeRange(0, 540, 600), 100, true));

        Assert.Throws<ArgumentException>(() => Create());
    }

    [Test]
    public void SameDayRecord_MayPrecedeWorkHours()
    {
        world.Clear();
        world.Add(new ApprovedScheduleRecord(
            "early", "bob", "offsite",
            new TimeRange(0, 480, 510), 100, true));

        var snapshot = Create();

        Assert.That(
            snapshot.WorldApprovedRecords[0].Period.StartMinute,
            Is.EqualTo(480));
    }

    [Test]
    public void HrClaimForUnknownEmployee_IsRejected()
    {
        claims.Add(new WhereaboutsClaim(
            "unknown_claim", "unknown_employee", "printer_area",
            new TimeRange(0, 630, 645), false));

        Assert.Throws<ArgumentException>(() => Create());
    }
}