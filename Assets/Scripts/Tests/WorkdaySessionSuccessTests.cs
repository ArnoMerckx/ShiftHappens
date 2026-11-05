using System;
using System.Collections.Generic;
using NUnit.Framework;

public class WorkdaySessionSuccessTests
{
    [Test]
    public void Constructor_RejectsMissingDay()
    {
        Assert.Throws<ArgumentNullException>(() => new WorkdaySession(null));
    }

    [Test]
    public void Opening_SubscriptionsObserveAvailabilityWithoutDiscoveringEvidence()
    {
        var context = new WorkdaySessionTestContext();
        WorkdaySession session = context.Session;
        var available = new List<string>();
        session.Deliveries.EvidenceAvailable += available.Add;

        Assert.That(session.Now.TotalMilliseconds,
            Is.EqualTo(session.Settings.OpensAt.TotalMilliseconds));
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Working));
        Assert.That(session.Deliveries.DeliveredIncidents, Is.Empty);
        Assert.That(available, Is.Empty);

        session.AdvanceRealSeconds(0);
        Assert.That(available.Count, Is.EqualTo(7));
        Assert.That(new HashSet<string>(available).Count, Is.EqualTo(7));
        foreach (string id in available)
        {
            Assert.That(session.IsEvidenceAvailable(id), Is.True);
            Assert.That(session.IsEvidenceDiscovered(id), Is.False);
            Assert.That(session.IsEvidenceRead(id), Is.False);
            Assert.That(session.CanAttachEvidence(id), Is.False);
        }

        session.AdvanceRealSeconds(0);
        Assert.That(available.Count, Is.EqualTo(7));
    }

    [Test]
    public void EvidenceCommands_KeepDiscoveryReadingAndInitialHrKnowledgeSeparate()
    {
        var context = new WorkdaySessionTestContext();
        WorkdaySession session = context.Session;
        var hr = context.Day.ScheduleData.HrKnowledge;
        int knownBefore = hr.KnownClaims.Count;

        Assert.That(session.TryDiscoverEvidence("E_SUPPLIES_RECEIPT"), Is.True);
        Assert.That(session.TryDiscoverEvidence("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(session.IsEvidenceDiscovered("E_SUPPLIES_RECEIPT"), Is.True);
        Assert.That(session.IsEvidenceRead("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(session.CanAttachEvidence("E_SUPPLIES_RECEIPT"), Is.True);
        Assert.That(session.TryReadEvidence("E_SUPPLIES_RECEIPT"), Is.True);
        Assert.That(session.TryReadEvidence("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(session.IsEvidenceRead("E_SUPPLIES_RECEIPT"), Is.True);

        Assert.That(session.TryReadEvidence("E_SCHEDULE"), Is.True);
        Assert.That(session.IsEvidenceDiscovered("E_SCHEDULE"), Is.True);
        Assert.That(hr.KnownClaims.Count, Is.EqualTo(knownBefore));
        foreach (Claim claim in hr.KnownClaims)
            Assert.That(claim.ClaimId, Is.Not.EqualTo("C_SUPPLIES_HANDOVER"));
    }

    [Test]
    public void Session_UsesAuthoredRateAndCustomCredibilityConfiguration()
    {
        var schedules = new DayScheduleData(
            new WorkdaySettings("slow", "Slow day", 0, 540, 1020, 5),
            new List<string>(), new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>());
        var day = new DayData(schedules, new List<EvidenceDefinition>(),
            new List<DayIncidentPlan>());
        var session = new WorkdaySession(day, new CredibilityRules(80, 10, 20, 0));

        session.AdvanceRealSeconds(2);

        Assert.That(session.Now.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 540, 10).TotalMilliseconds));
        Assert.That(session.Progress.Credibility, Is.EqualTo(80));
    }

    [Test]
    public void Monday_CompletesWithPhysicalReceiptAndFortyMinuteTimer()
    {
        var context = new WorkdaySessionTestContext();
        WorkdaySession session = context.Session;
        var delivered = new List<string>();
        var reviewedAt = new List<long>();
        int overdue = 0;
        int timerAlerts = 0;
        long timerAlertAt = -1;
        session.Deliveries.IncidentDelivered += incident => delivered.Add(incident.InstanceId);
        session.Reviews.ReviewCompleted += (review, decision) =>
            reviewedAt.Add(session.Now.TotalMilliseconds);
        session.Deadlines.IncidentOverdue += incident => overdue++;
        session.Timer.Completed += () =>
        {
            timerAlerts++;
            timerAlertAt = session.Now.TotalMilliseconds;
        };

        context.AdvanceTo(590);
        IncidentInstance first = context.Submit("D0I1", "bob", "E_SCHEDULE");
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(1));
        context.AdvanceTo(599, 59);
        Assert.That(first.Status, Is.EqualTo(IncidentStatus.Pending));
        context.AdvanceTo(600);
        Assert.That(first.Status, Is.EqualTo(IncidentStatus.Approved));

        // 11:15 physical clock; 12:00 deadline with a five-minute buffer.
        context.AdvanceTo(675);
        Assert.That(session.Timer.TryStart(40 * 60), Is.True);
        Assert.That(session.Timer.RemainingMilliseconds, Is.EqualTo(2400000L));
        context.AdvanceTo(714, 59);
        Assert.That(timerAlerts, Is.Zero);
        Assert.That(session.Timer.RemainingMilliseconds, Is.EqualTo(1000L));
        context.AdvanceTo(715);
        Assert.That(timerAlerts, Is.EqualTo(1));
        Assert.That(timerAlertAt, Is.EqualTo(new GameTime(0, 715).TotalMilliseconds));
        IncidentInstance second = context.Submit("D0I2", "dave", "E_DAVE_PRINTER");
        context.AdvanceTo(720);
        Assert.That(second.Status, Is.EqualTo(IncidentStatus.Pending));
        context.AdvanceTo(730);
        Assert.That(second.Status, Is.EqualTo(IncidentStatus.Approved));

        context.AdvanceTo(840);
        Assert.That(session.IsEvidenceDiscovered("E_SUPPLIES_RECEIPT"), Is.False);
        IncidentInstance third = context.Submit("D0I3", "steve", "E_SUPPLIES_RECEIPT");
        Assert.That(session.IsEvidenceRead("E_SUPPLIES_RECEIPT"), Is.False);
        CollectionAssert.AreEqual(new[] { "E_SUPPLIES_RECEIPT" },
            third.SubmittedReport.EvidenceIds);
        context.AdvanceTo(855);
        Assert.That(third.Status, Is.EqualTo(IncidentStatus.Approved));
        context.AdvanceTo(1020);

        CollectionAssert.AreEqual(new[] { "D0I1", "D0I2", "D0I3" }, delivered);
        CollectionAssert.AreEqual(new[]
        {
            new GameTime(0, 600).TotalMilliseconds,
            new GameTime(0, 730).TotalMilliseconds,
            new GameTime(0, 855).TotalMilliseconds
        }, reviewedAt);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(session.Progress.Credibility, Is.EqualTo(100));
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
        Assert.That(session.Progress.MissedDeadlines, Is.Empty);
        Assert.That(overdue, Is.Zero);
        Assert.That(timerAlerts, Is.EqualTo(1));

        WorkdaySummary summary = session.Workday.Summary;
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.FinalState, Is.EqualTo(WorkdayState.Completed));
        Assert.That(summary.EndedAt.TotalMilliseconds,
            Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
        Assert.That(summary.Credibility, Is.EqualTo(100));
        Assert.That(summary.Incidents.Count, Is.EqualTo(3));
        Assert.That(summary.History.Count, Is.EqualTo(3));
        Assert.That(summary.MissedDeadlines, Is.Empty);
        foreach (IncidentSummary incident in summary.Incidents)
            Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Approved));
        foreach (ReviewHistoryEntry entry in summary.History)
        {
            Assert.That(entry.CredibilityBefore, Is.EqualTo(100));
            Assert.That(entry.CredibilityAfter, Is.EqualTo(100));
        }

        session.AdvanceRealSeconds(10000);
        Assert.That(session.Workday.Summary, Is.SameAs(summary));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(summary.EndedAt.TotalMilliseconds));
        Assert.That(session.TryReadEvidence("E_OFFICE_SOCIAL"), Is.False);
        Assert.That(session.TryDiscoverEvidence("E_OFFICE_SOCIAL"), Is.False);
        Assert.That(timerAlerts, Is.EqualTo(1));
    }
}
