using System;
using System.Collections.Generic;
using NUnit.Framework;

public class MissedDeadlineEntryTests
{
    private HrReviewTestContext context;
    private IncidentInstance incident;
    private GameTime recordedAt;

    [SetUp]
    public void SetUp()
    {
        context = new HrReviewTestContext();
        recordedAt = new GameTime(0, 720, 0, 1);
        context.Scheduler.AdvanceTo(recordedAt);
        incident = context.Deliveries.GetDelivered("D0I2");
        Assert.That(incident.TryMarkOverdue(recordedAt), Is.True);
    }

    [Test]
    public void NullIncident_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new MissedDeadlineEntry(null, recordedAt, 100, 65));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NonOverdueIncident_IsRejected(bool pending)
    {
        IncidentInstance fresh = context.Day.Incidents[1].CreateInstance();
        if (pending)
        {
            var validator = new ReportSubmissionValidator(new List<string> { "dave" },
                new EvidenceService(new EvidenceDatabase(new List<EvidenceDefinition>())));
            Assert.That(fresh.TrySubmit(new IncidentReport("D0I2", "dave",
                new List<string>(), 0, 675), validator, 0, 675).IsValid, Is.True);
        }

        Assert.Throws<ArgumentException>(
            () => new MissedDeadlineEntry(fresh, recordedAt, 100, 65));
    }

    [TestCase(-1)]
    [TestCase(0)]
    public void RecordedTime_MustBeStrictlyAfterDeadline(int offset)
    {
        var invalidTime = new GameTime(new GameTime(0, 720).TotalMilliseconds + offset);
        Assert.Throws<ArgumentException>(
            () => new MissedDeadlineEntry(incident, invalidTime, 100, 65));
    }

    [TestCase(-1, 0)]
    [TestCase(100, -1)]
    [TestCase(100, 101)]
    public void InvalidScores_AreRejected(int before, int after)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MissedDeadlineEntry(incident, recordedAt, before, after));
    }

    [Test]
    public void Entry_SnapshotsIdsPreciseTimesAndScoresWithoutReport()
    {
        var entry = new MissedDeadlineEntry(incident, recordedAt, 100, 65);
        Assert.That(entry.InstanceId, Is.EqualTo("D0I2"));
        Assert.That(entry.IncidentId, Is.EqualTo("I2"));
        Assert.That(entry.DeadlineAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 720).TotalMilliseconds));
        Assert.That(entry.RecordedAt.TotalMilliseconds, Is.EqualTo(recordedAt.TotalMilliseconds));
        Assert.That(entry.CredibilityBefore, Is.EqualTo(100));
        Assert.That(entry.CredibilityAfter, Is.EqualTo(65));
        Assert.That(incident.SubmittedReport, Is.Null);
        Assert.That(incident.Decision, Is.Null);
    }
}
