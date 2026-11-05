using System.Collections.Generic;
using NUnit.Framework;

public class WorkdaySessionFailureTests
{
    private static void Finish(WorkdaySession session)
    {
        // Deliberately cross the whole day through its public elapsed-time entry.
        session.AdvanceRealSeconds(10000);
    }

    private static void AssertMissed(
        MissedDeadlineEntry entry, string id, int minute, int before, int after)
    {
        Assert.That(entry.InstanceId, Is.EqualTo(id));
        Assert.That(entry.DeadlineAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, minute).TotalMilliseconds));
        Assert.That(entry.RecordedAt.TotalMilliseconds,
            Is.EqualTo(entry.DeadlineAt.TotalMilliseconds + 1));
        Assert.That(entry.CredibilityBefore, Is.EqualTo(before));
        Assert.That(entry.CredibilityAfter, Is.EqualTo(after));
    }

    [Test]
    public void AllMissed_DefaultPenaltiesFireAtThirdDeadlineAndClampToZero()
    {
        var session = new WorkdaySessionTestContext().Session;
        int overdue = 0;
        session.Deadlines.IncidentOverdue += incident => overdue++;
        Finish(session);

        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Fired));
        WorkdaySummary summary = session.Workday.Summary;
        Assert.That(summary.Credibility, Is.Zero);
        Assert.That(summary.EndedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 900).TotalMilliseconds + 1));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(summary.EndedAt.TotalMilliseconds));
        Assert.That(summary.History, Is.Empty);
        Assert.That(summary.MissedDeadlines.Count, Is.EqualTo(3));
        AssertMissed(summary.MissedDeadlines[0], "D0I1", 630, 100, 65);
        AssertMissed(summary.MissedDeadlines[1], "D0I2", 720, 65, 30);
        AssertMissed(summary.MissedDeadlines[2], "D0I3", 900, 30, 0);
        Assert.That(overdue, Is.EqualTo(3));
        foreach (IncidentSummary incident in summary.Incidents)
        {
            Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Overdue));
            Assert.That(incident.Report, Is.Null);
            Assert.That(incident.Decision, Is.Null);
        }

        Finish(session);
        Assert.That(session.Workday.Summary, Is.SameAs(summary));
        Assert.That(overdue, Is.EqualTo(3));
        Assert.That(session.Progress.MissedDeadlines.Count, Is.EqualTo(3));
    }

    [Test]
    public void TwoMissesAndSupportedReceipt_CompleteWithThirtyCredibility()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(840);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(30));
        context.Submit("D0I3", "steve", "E_SUPPLIES_RECEIPT");
        Finish(context.Session);

        WorkdaySummary summary = context.Session.Workday.Summary;
        Assert.That(summary.FinalState, Is.EqualTo(WorkdayState.Completed));
        Assert.That(summary.Credibility, Is.EqualTo(30));
        Assert.That(summary.MissedDeadlines.Count, Is.EqualTo(2));
        Assert.That(summary.History.Count, Is.EqualTo(1));
        Assert.That(summary.History[0].Decision.Verdict, Is.EqualTo(HrVerdict.Approved));
        Assert.That(summary.History[0].CredibilityBefore, Is.EqualTo(30));
        Assert.That(summary.History[0].CredibilityAfter, Is.EqualTo(30));
        Assert.That(summary.Incidents[2].Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(context.Session.Submissions.PendingReviews, Is.Empty);
    }

    [TestCase("bob", "E_SCHEDULE")]
    [TestCase("dave", "E_OFFICE_SOCIAL")]
    public void ThreeRejectedReports_CompleteWithTwentyFiveCredibility(
        string printerSuspect, string printerEvidence)
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        context.Submit("D0I1", "alice", "E_SCHEDULE");
        context.AdvanceTo(675);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(75));
        context.Submit("D0I2", printerSuspect, printerEvidence);
        context.AdvanceTo(840);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(50));
        context.Submit("D0I3", "steve", "E_SCHEDULE");
        Finish(context.Session);

        WorkdaySummary summary = context.Session.Workday.Summary;
        Assert.That(summary.FinalState, Is.EqualTo(WorkdayState.Completed));
        Assert.That(summary.Credibility, Is.EqualTo(25));
        Assert.That(summary.History.Count, Is.EqualTo(3));
        Assert.That(summary.MissedDeadlines, Is.Empty);
        Assert.That(context.Session.Submissions.PendingReviews, Is.Empty);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(summary.Incidents[i].Status, Is.EqualTo(IncidentStatus.Rejected));
            Assert.That(summary.History[i].Decision.Verdict, Is.EqualTo(HrVerdict.Rejected));
            Assert.That(summary.History[i].CredibilityBefore, Is.EqualTo(100 - i * 25));
            Assert.That(summary.History[i].CredibilityAfter, Is.EqualTo(75 - i * 25));
        }
        // HR still uses the approved dentist record when its email was never discovered.
        Assert.That(context.Session.IsEvidenceDiscovered("E_BOB_DENTIST"), Is.False);
    }

    [Test]
    public void MixedMissAndRejectionPenalties_LeaveFiveCredibilityWithoutFiring()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(675);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(65));
        context.Submit("D0I2", "bob", "E_SCHEDULE");
        context.AdvanceTo(690);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(40));
        Finish(context.Session);

        WorkdaySummary summary = context.Session.Workday.Summary;
        Assert.That(summary.FinalState, Is.EqualTo(WorkdayState.Completed));
        Assert.That(summary.Credibility, Is.EqualTo(5));
        Assert.That(summary.History.Count, Is.EqualTo(1));
        Assert.That(summary.History[0].CredibilityBefore, Is.EqualTo(65));
        Assert.That(summary.History[0].CredibilityAfter, Is.EqualTo(40));
        Assert.That(summary.MissedDeadlines.Count, Is.EqualTo(2));
        AssertMissed(summary.MissedDeadlines[0], "D0I1", 630, 100, 65);
        AssertMissed(summary.MissedDeadlines[1], "D0I3", 900, 40, 5);
    }

    [Test]
    public void EarlyReviewFiring_HidesFutureIncidentsAndFreezesTimerImmediately()
    {
        var context = new WorkdaySessionTestContext(
            rules: new CredibilityRules(25, 25, 35, 0));
        WorkdaySession session = context.Session;
        context.AdvanceTo(590);
        context.Submit("D0I1", "alice", "E_SCHEDULE");
        int timerAlerts = 0;
        session.Timer.Completed += () => timerAlerts++;
        session.Timer.TryStart(3600);
        Finish(session);

        WorkdaySummary summary = session.Workday.Summary;
        Assert.That(summary.FinalState, Is.EqualTo(WorkdayState.Fired));
        Assert.That(summary.EndedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 600).TotalMilliseconds));
        Assert.That(summary.Credibility, Is.Zero);
        Assert.That(summary.Incidents.Count, Is.EqualTo(1));
        Assert.That(summary.Incidents[0].InstanceId, Is.EqualTo("D0I1"));
        Assert.That(summary.Incidents[0].Status, Is.EqualTo(IncidentStatus.Rejected));
        Assert.That(summary.History.Count, Is.EqualTo(1));
        Assert.That(summary.MissedDeadlines, Is.Empty);
        Assert.Throws<KeyNotFoundException>(() => session.Deliveries.GetDelivered("D0I2"));
        Assert.Throws<KeyNotFoundException>(() => session.Deliveries.GetDelivered("D0I3"));
        Assert.That(session.Timer.State, Is.EqualTo(DurationTimerState.Paused));
        Assert.That(session.Timer.RemainingMilliseconds, Is.EqualTo(3000000L));
        Assert.That(timerAlerts, Is.Zero);
        Assert.That(session.TryDiscoverEvidence("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(session.Submissions.TrySubmit("D0I2", "dave",
            new List<string>()).IsValid, Is.False);
        session.SetPaused(false);
        Finish(session);
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(summary.EndedAt.TotalMilliseconds));
        Assert.That(session.Workday.Summary, Is.SameAs(summary));
        Assert.That(timerAlerts, Is.Zero);
    }

    [Test]
    public void MixedPenalties_FireExactlyAtConfiguredNonzeroThreshold()
    {
        var context = new WorkdaySessionTestContext(
            rules: new CredibilityRules(70, 25, 35, 10));
        context.AdvanceTo(675);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(35));
        context.Submit("D0I2", "bob", "E_SCHEDULE");
        Finish(context.Session);

        WorkdaySummary summary = context.Session.Workday.Summary;
        Assert.That(summary.FinalState, Is.EqualTo(WorkdayState.Fired));
        Assert.That(summary.Credibility, Is.EqualTo(10));
        Assert.That(summary.EndedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 690).TotalMilliseconds));
        Assert.That(summary.Incidents.Count, Is.EqualTo(2));
        Assert.That(summary.History.Count, Is.EqualTo(1));
        Assert.That(summary.MissedDeadlines.Count, Is.EqualTo(1));
        AssertMissed(summary.MissedDeadlines[0], "D0I1", 630, 70, 35);
        Assert.That(summary.History[0].CredibilityBefore, Is.EqualTo(35));
        Assert.That(summary.History[0].CredibilityAfter, Is.EqualTo(10));
    }
}
