using System;
using System.Collections.Generic;
using NUnit.Framework;

public class HrKnowledgeTests
{
    private static ApprovedScheduleRecord DentistRecord()
    {
        return new ApprovedScheduleRecord(
            "dentist", "bob", "offsite",
            new TimeRange(0, 600, 690), 100, true);
    }

    private static WhereaboutsClaim DentistClaim()
    {
        return new WhereaboutsClaim(
            "C_BOB_DENTIST", "bob", "offsite",
            new TimeRange(0, 600, 690), true);
    }

    [Test]
    public void WorldRecords_AreNotAutomaticallyKnown()
    {
        var worldRecords = new List<ApprovedScheduleRecord>
        {
            DentistRecord()
        };

        var hr = new HrKnowledge(
            new List<ApprovedScheduleRecord>(), new List<Claim>());

        var window = new TimeRange(0, 635, 640);

        Assert.That(
            ScheduleResolver.GetPresence(
                "bob", "printer_area", window, worldRecords),
            Is.EqualTo(PresenceStatus.Impossible));

        Assert.That(
            ScheduleResolver.GetPresence(
                "bob", "printer_area", window,
                new List<ApprovedScheduleRecord>(hr.ApprovedRecords)),
            Is.EqualTo(PresenceStatus.Unknown));
    }

    [Test]
    public void KnownApprovedAbsence_DoesNotRequireDocumentClaims()
    {
        var hr = new HrKnowledge(
            new List<ApprovedScheduleRecord> { DentistRecord() },
            new List<Claim>());

        Assert.That(hr.KnownClaims, Is.Empty);

        Assert.That(
            ScheduleResolver.GetPresence(
                "bob", "printer_area", new TimeRange(0, 635, 640),
                new List<ApprovedScheduleRecord>(hr.ApprovedRecords)),
            Is.EqualTo(PresenceStatus.Impossible));
    }

    [Test]
    public void KnownWhereaboutsClaim_IsNotAnApprovedConstraint()
    {
        var meeting = new WhereaboutsClaim(
            "C_ALICE_MEETING", "alice", "accounts_office",
            new TimeRange(0, 565, 590), true);

        var hr = new HrKnowledge(
            new List<ApprovedScheduleRecord>(),
            new List<Claim> { meeting });

        Assert.That(hr.KnownClaims.Count, Is.EqualTo(1));
        Assert.That(hr.ApprovedRecords, Is.Empty);

        Assert.That(
            ScheduleResolver.GetPresence(
                "alice", "break_room", new TimeRange(0, 575, 580),
                new List<ApprovedScheduleRecord>(hr.ApprovedRecords)),
            Is.EqualTo(PresenceStatus.Unknown));
    }

    [Test]
    public void SeparateReceiptBundle_DoesNotAlterHrKnowledge()
    {
        var hr = new HrKnowledge(
            new List<ApprovedScheduleRecord> { DentistRecord() },
            new List<Claim> { DentistClaim() });

        var receipt = new CustodyClaim(
            "C_SUPPLIES_HANDOVER", "steve", "supplies_carton_01",
            "supplies_area", 0, 530);

        var reportClaims = new List<Claim> { receipt };

        Assert.That(reportClaims[0], Is.SameAs(receipt));
        Assert.That(hr.KnownClaims.Count, Is.EqualTo(1));
        Assert.That(
            hr.KnownClaims[0].ClaimId,
            Is.EqualTo("C_BOB_DENTIST"));
    }

    [Test]
    public void Membership_IsCopiedAndReadOnly()
    {
        var records = new List<ApprovedScheduleRecord>
        {
            DentistRecord()
        };
        var claims = new List<Claim> { DentistClaim() };

        var hr = new HrKnowledge(records, claims);

        records.Clear();
        claims.Clear();

        Assert.That(hr.ApprovedRecords.Count, Is.EqualTo(1));
        Assert.That(hr.KnownClaims.Count, Is.EqualTo(1));

        Assert.Throws<NotSupportedException>(() =>
            ((IList<ApprovedScheduleRecord>)hr.ApprovedRecords).Clear());

        Assert.Throws<NotSupportedException>(() =>
            ((IList<Claim>)hr.KnownClaims).Clear());
    }

    [Test]
    public void NullCollections_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HrKnowledge(null, new List<Claim>()));

        Assert.Throws<ArgumentNullException>(() =>
            new HrKnowledge(new List<ApprovedScheduleRecord>(), null));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidMembers_AreRejected(bool useClaims)
    {
        if (useClaims)
        {
            var claim = DentistClaim();

            Assert.Throws<ArgumentException>(() =>
                new HrKnowledge(
                    new List<ApprovedScheduleRecord>(),
                    new List<Claim> { null }));

            Assert.Throws<ArgumentException>(() =>
                new HrKnowledge(
                    new List<ApprovedScheduleRecord>(),
                    new List<Claim> { claim, DentistClaim() }));
        }
        else
        {
            var record = DentistRecord();

            Assert.Throws<ArgumentException>(() =>
                new HrKnowledge(
                    new List<ApprovedScheduleRecord> { null },
                    new List<Claim>()));

            Assert.Throws<ArgumentException>(() =>
                new HrKnowledge(
                    new List<ApprovedScheduleRecord>
                    {
                        record, DentistRecord()
                    },
                    new List<Claim>()));
        }
    }
}