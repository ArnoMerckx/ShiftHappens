using System.Collections.Generic;
using NUnit.Framework;

public class WorkdayDocumentQueryTests : EvidencePresentationTestBase
{
    private static DayData Day(List<EvidenceDefinition> documents, DayScheduleData schedules = null)
    {
        if (schedules == null)
        {
            schedules = new DayScheduleData(
                new WorkdaySettings("query_day", "Query day", 0, 540, 1020, 20),
                new List<string> { "steve" }, new List<ApprovedScheduleRecord>(),
                new List<ApprovedScheduleRecord>(), new List<Claim>());
        }
        return new DayData(schedules, documents, new List<DayIncidentPlan>());
    }

    [Test]
    public void SessionQueries_UseCurrentTimeAndExistingSharedDiscoveryState()
    {
        var session = new WorkdaySession(Day(new List<EvidenceDefinition>
        {
            Document("CURRENT"), Document("LATER", EvidenceSource.InWatch, 0, 541)
        }));
        CollectionAssert.AreEqual(new[] { "CURRENT" },
            Ids(session.GetAvailableDigitalDocuments(EvidenceSource.InWatch)));
        Assert.That(session.GetAttachableDocuments(), Is.Empty);
        Assert.That(session.TryReadEvidence("CURRENT"), Is.True);
        CollectionAssert.AreEqual(new[] { "CURRENT" }, Ids(session.GetAttachableDocuments()));
        Assert.That(session.GetAvailableDigitalDocuments(EvidenceSource.InWatch)[0].IsRead, Is.True);
        session.AdvanceRealSeconds(3);
        CollectionAssert.AreEqual(new[] { "CURRENT", "LATER" },
            Ids(session.GetAvailableDigitalDocuments(EvidenceSource.InWatch)));
        Assert.That(session.IsEvidenceDiscovered("LATER") || session.IsEvidenceRead("LATER"), Is.False);
    }

    [Test]
    public void SessionReceiptRead_AppearsInSameAttachmentListWithoutEnteringDigitalInbox()
    {
        var session = new WorkdaySession(Day(new List<EvidenceDefinition>
        {
            Document("EMAIL"), Document("E_SUPPLIES_RECEIPT", EvidenceSource.Physical)
        }));
        Assert.That(session.GetAttachableDocuments(), Is.Empty);
        Assert.That(session.TryReadEvidence("E_SUPPLIES_RECEIPT"), Is.True);
        var row = session.GetAttachableDocuments()[0];
        Assert.That(row.EvidenceId, Is.EqualTo("E_SUPPLIES_RECEIPT"));
        Assert.That(row.Source, Is.EqualTo(EvidenceSource.Physical));
        Assert.That(row.IsRead && row.IsDiscovered, Is.True);
        CollectionAssert.AreEqual(new[] { "EMAIL" },
            Ids(session.GetAvailableDigitalDocuments(EvidenceSource.InWatch)));
    }

    [Test]
    public void RepeatedSessionQueries_DoNotAdvanceClockOrChangeDiscovery()
    {
        var session = new WorkdaySession(Day(new List<EvidenceDefinition> { Document("EMAIL") }));
        long before = session.Now.TotalMilliseconds;
        for (int i = 0; i < 3; i++)
        {
            session.GetAvailableDigitalDocuments(EvidenceSource.InWatch);
            session.GetAvailableDigitalDocuments(EvidenceSource.Excess);
            session.GetAvailableDigitalDocuments(EvidenceSource.ShareLess);
            session.GetAttachableDocuments();
        }
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(before));
        Assert.That(session.IsEvidenceDiscovered("EMAIL") || session.IsEvidenceRead("EMAIL"), Is.False);
    }

    [Test]
    public void SessionsSharingDayData_KeepIndependentQueryFlagsAndAttachmentLists()
    {
        var day = Day(new List<EvidenceDefinition> { Document("EMAIL") });
        var first = new WorkdaySession(day);
        var second = new WorkdaySession(day);
        first.TryReadEvidence("EMAIL");
        Assert.That(first.GetAttachableDocuments().Count, Is.EqualTo(1));
        Assert.That(second.GetAttachableDocuments(), Is.Empty);
        Assert.That(first.GetAvailableDigitalDocuments(EvidenceSource.InWatch)[0].IsRead, Is.True);
        var other = second.GetAvailableDigitalDocuments(EvidenceSource.InWatch)[0];
        Assert.That(other.IsRead || other.IsDiscovered, Is.False);
    }

    [Test]
    public void QueriesAfterClosing_RemainReadOnlyAndDoNotBypassWorkMutationGate()
    {
        var session = new WorkdaySession(Day(new List<EvidenceDefinition>
        {
            Document("READ"), Document("UNREAD", EvidenceSource.InWatch, 0, 541)
        }));
        session.TryReadEvidence("READ");
        session.AdvanceRealSeconds(1440);
        Assert.That(session.Workday.IsTerminal, Is.True);
        long closedAt = session.Now.TotalMilliseconds;
        CollectionAssert.AreEqual(new[] { "READ", "UNREAD" },
            Ids(session.GetAvailableDigitalDocuments(EvidenceSource.InWatch)));
        CollectionAssert.AreEqual(new[] { "READ" }, Ids(session.GetAttachableDocuments()));
        Assert.That(session.TryReadEvidence("UNREAD"), Is.False);
        Assert.That(session.IsEvidenceDiscovered("UNREAD") || session.IsEvidenceRead("UNREAD"), Is.False);
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(closedAt));
    }

    [Test]
    public void QueryingAndReadingClaims_DoNotPromoteDocumentClaimsIntoWorldOrHrKnowledge()
    {
        var email = Document("EMAIL");
        var receipt = Document("E_SUPPLIES_RECEIPT", EvidenceSource.Physical);
        AddClaim(email, "C_DOCUMENT_ONLY");
        AddClaim(receipt, "C_RECEIPT_ONLY");
        var worldOnly = new ApprovedScheduleRecord("world_only", "steve", "world_place",
            new TimeRange(0, 540, 570), 100, true);
        var hrRecord = new ApprovedScheduleRecord("known_record", "steve", "hr_record_place",
            new TimeRange(0, 570, 600), 100, true);
        var hrClaim = new WhereaboutsClaim("C_HR_ONLY", "steve", "hr_claim_place",
            new TimeRange(0, 600, 660), true);
        var schedules = new DayScheduleData(
            new WorkdaySettings("query_day", "Query day", 0, 540, 1020, 20),
            new List<string> { "steve" }, new List<ApprovedScheduleRecord> { worldOnly, hrRecord },
            new List<ApprovedScheduleRecord> { hrRecord }, new List<Claim> { hrClaim });
        var day = Day(new List<EvidenceDefinition> { email, receipt }, schedules);
        var session = new WorkdaySession(day);
        var rows = session.GetAvailableDigitalDocuments(EvidenceSource.InWatch);
        Assert.That(rows.Count, Is.EqualTo(1));
        Assert.That(rows[0].BodyText, Is.EqualTo(email.BodyText));
        Assert.That(session.IsEvidenceDiscovered("EMAIL"), Is.False);
        session.TryReadEvidence("EMAIL");
        session.TryReadEvidence("E_SUPPLIES_RECEIPT");
        CollectionAssert.AreEqual(new[] { "EMAIL", "E_SUPPLIES_RECEIPT" }, Ids(session.GetAttachableDocuments()));
        CollectionAssert.AreEqual(new[] { worldOnly, hrRecord }, schedules.WorldApprovedRecords);
        CollectionAssert.AreEqual(new[] { hrRecord }, schedules.HrKnowledge.ApprovedRecords);
        CollectionAssert.AreEqual(new Claim[] { hrClaim }, schedules.HrKnowledge.KnownClaims);
        Assert.That(email.GetClaims()[0].ClaimId, Is.EqualTo("C_DOCUMENT_ONLY"));
        Assert.That(receipt.GetClaims()[0].ClaimId, Is.EqualTo("C_RECEIPT_ONLY"));
    }
}
