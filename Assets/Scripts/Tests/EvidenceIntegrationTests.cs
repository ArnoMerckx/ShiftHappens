using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class EvidenceIntegrationTests
{
    private EvidenceDatabase database;
    private EvidenceService service;
    private HrKnowledge hr;

    [SetUp]
    public void SetUp()
    {
        const string folder =
            "Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents";

        var documents = new List<EvidenceDefinition>();

        foreach (var guid in AssetDatabase.FindAssets(
            "t:EvidenceDefinition", new[] { folder }))
        {
            documents.Add(
                AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)));
        }

        database = new EvidenceDatabase(documents);
        service = new EvidenceService(database);

        var approved = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "dentist", "bob", "offsite",
                new TimeRange(0, 600, 690), 100, true),

            new ApprovedScheduleRecord(
                "meeting", "alice", "accounts_office",
                new TimeRange(0, 565, 590), 100, true),

            new ApprovedScheduleRecord(
                "inspection", "dave", "printer_area",
                new TimeRange(0, 630, 645), 100, false),

            new ApprovedScheduleRecord(
                "collection", "karen", "supplies_area",
                new TimeRange(0, 810, 825), 100, false)
        };

        var knownClaims = new List<Claim>();

        foreach (var id in new[]
        {
            "E_BOB_DENTIST",
            "E_ALICE_MEETING",
            "E_DAVE_PRINTER",
            "E_KAREN_COLLECTION"
        })
        {
            knownClaims.AddRange(database.GetById(id).GetClaims());
        }

        hr = new HrKnowledge(approved, knownClaims);
    }

    [TestCase(575, 580, "break_room", PresenceStatus.Unknown)]
    [TestCase(635, 640, "printer_area", PresenceStatus.Impossible)]
    public void PublishedAssignment_StaysSeparateFromHrConstraints(
        int start, int end, string location, PresenceStatus expected)
    {
        var schedule = database.GetById("E_SCHEDULE");
        WhereaboutsClaim published = null;

        foreach (var claim in schedule.GetClaims())
        {
            if (claim is WhereaboutsClaim whereabouts
                && whereabouts.EmployeeId == "bob"
                && whereabouts.Period.Contains(0, start))
            {
                published = whereabouts;
                break;
            }
        }

        Assert.That(published, Is.Not.Null);
        Assert.That(published.LocationId, Is.EqualTo(location));
        Assert.That(published.RequiresPresence, Is.False);

        service.MarkRead("E_SCHEDULE", 0, 540);

        Assert.That(service.CanAttach("E_SCHEDULE", 0, 540), Is.True);
        Assert.That(service.IsDiscovered("E_BOB_DENTIST"), Is.False);

        Assert.That(
            ScheduleResolver.GetPresence(
                "bob", location, new TimeRange(0, start, end),
                new List<ApprovedScheduleRecord>(hr.ApprovedRecords)),
            Is.EqualTo(expected));

        Assert.That(schedule.GetClaims().Count, Is.EqualTo(7));
        Assert.That(published.LocationId, Is.EqualTo(location));
    }

    [TestCase("E_SUPPLIES_RECEIPT", EvidenceSource.Physical)]
    [TestCase("E_DAVE_PRINTER", EvidenceSource.InWatch)]
    public void AuthoredSources_UseSameDiscoveryAndAttachmentRoute(
        string id, EvidenceSource source)
    {
        var document = database.GetById(id);

        Assert.That(document.Source, Is.EqualTo(source));
        Assert.That(service.IsAvailable(id, 0, 540), Is.True);
        Assert.That(service.CanAttach(id, 0, 540), Is.False);

        Assert.That(service.MarkRead(id, 0, 540), Is.True);
        Assert.That(service.MarkRead(id, 0, 540), Is.False);

        Assert.That(service.IsDiscovered(id), Is.True);
        Assert.That(service.IsRead(id), Is.True);
        Assert.That(service.CanAttach(id, 0, 540), Is.True);
        Assert.That(database.GetById(id), Is.SameAs(document));
    }

    [Test]
    public void Receipt_RecordsEarlierCustodyWithoutInformingHr()
    {
        var document = database.GetById("E_SUPPLIES_RECEIPT");
        var claims = document.GetClaims();

        Assert.That(claims.Count, Is.EqualTo(1));
        Assert.That(claims[0], Is.TypeOf<CustodyClaim>());

        var custody = (CustodyClaim)claims[0];

        Assert.That(custody.EmployeeId, Is.EqualTo("steve"));
        Assert.That(custody.ItemId, Is.EqualTo("supplies_carton_01"));
        Assert.That(custody.LocationId, Is.EqualTo("supplies_area"));
        Assert.That(custody.DayIndex, Is.EqualTo(0));
        Assert.That(custody.MinuteOfDay, Is.EqualTo(530));
        Assert.That(document.AvailableMinute, Is.EqualTo(540));

        service.MarkRead(document.EvidenceId, 0, 540);

        foreach (var known in hr.KnownClaims)
            Assert.That(known.ClaimId, Is.Not.EqualTo(custody.ClaimId));

        Assert.That(service.CanAttach(document.EvidenceId, 0, 540), Is.True);

        Assert.That(
            ScheduleResolver.GetPresence(
                "steve", "supplies_area", new TimeRange(0, 815, 820),
                new List<ApprovedScheduleRecord>(hr.ApprovedRecords)),
            Is.EqualTo(PresenceStatus.Unknown));
    }
}