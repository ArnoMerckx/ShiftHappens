using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class WorkdaySessionTimingTests
{
    private static DayData LateDay()
    {
        var definition = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
        Assert.That(definition, Is.Not.Null);
        DayData monday = definition.GetData();
        var incident = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
        Assert.That(incident, Is.Not.Null);
        var plans = new List<DayIncidentPlan>();
        for (int i = 0; i < 2; i++)
        {
            plans.Add(new DayIncidentPlan(incident, new IncidentScheduleEntry(
                "closing_" + i, "I2", new TimeRange(0, 635, 640), 1000, 1019), "dave"));
        }
        return new DayData(monday.ScheduleData,
            new List<EvidenceDefinition>(monday.EvidenceDatabase.All), plans);
    }

    [Test]
    public void Closing_DrainsQueuedReviewsAtTheirActualTimesAndFreezesTimer()
    {
        var context = new WorkdaySessionTestContext(LateDay());
        WorkdaySession session = context.Session;
        var states = new List<WorkdayState>();
        session.Workday.StateChanged += () => states.Add(session.Workday.State);
        context.AdvanceTo(1018, 30);
        context.Submit("closing_0", "dave", "E_DAVE_PRINTER");
        session.Timer.TryStart(600);
        context.AdvanceTo(1018, 31);
        context.Submit("closing_1", "dave", "E_DAVE_PRINTER");
        context.AdvanceTo(1020);

        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Closing));
        Assert.That(session.Workday.Summary, Is.Null);
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(2));
        Assert.That(session.Timer.State, Is.EqualTo(DurationTimerState.Paused));
        Assert.That(session.Timer.RemainingMilliseconds, Is.EqualTo(510000L));
        Assert.That(session.TryDiscoverEvidence("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(session.TryReadEvidence("E_SCHEDULE"), Is.False);
        Assert.That(session.Timer.TryResume(), Is.False);
        Assert.That(session.Submissions.TrySubmit("closing_0", "dave",
            new List<string>()).IsValid, Is.False);

        context.AdvanceTo(1033, 30);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Closing));
        Assert.That(session.Progress.History.Count, Is.EqualTo(1));
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(1));
        session.AdvanceRealSeconds(10000);

        WorkdaySummary summary = session.Workday.Summary;
        CollectionAssert.AreEqual(new[] { WorkdayState.Closing, WorkdayState.Completed }, states);
        Assert.That(summary.EndedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 1033, 31).TotalMilliseconds));
        Assert.That(summary.History.Count, Is.EqualTo(2));
        Assert.That(summary.History[0].ReviewedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 1033, 30).TotalMilliseconds));
        Assert.That(summary.Credibility, Is.EqualTo(100));
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
        Assert.That(session.Timer.RemainingMilliseconds, Is.EqualTo(510000L));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(summary.EndedAt.TotalMilliseconds));
    }

    [Test]
    public void FiringDuringDrain_PreservesOtherPendingReportInFrozenSummary()
    {
        var context = new WorkdaySessionTestContext(LateDay(),
            new CredibilityRules(25, 25, 35, 0));
        WorkdaySession session = context.Session;
        context.AdvanceTo(1018, 30);
        context.Submit("closing_0", "dave");
        context.AdvanceTo(1018, 31);
        IncidentInstance pending = context.Submit("closing_1", "dave", "E_DAVE_PRINTER");
        context.AdvanceTo(1020);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Closing));
        session.AdvanceRealSeconds(10000);

        WorkdaySummary summary = session.Workday.Summary;
        Assert.That(summary.FinalState, Is.EqualTo(WorkdayState.Fired));
        Assert.That(summary.EndedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 1033, 30).TotalMilliseconds));
        Assert.That(summary.History.Count, Is.EqualTo(1));
        Assert.That(summary.Credibility, Is.Zero);
        Assert.That(summary.Incidents[0].Status, Is.EqualTo(IncidentStatus.Rejected));
        Assert.That(summary.Incidents[1].Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(summary.Incidents[1].Report, Is.SameAs(pending.SubmittedReport));
        Assert.That(summary.Incidents[1].Decision, Is.Null);
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(1));
        session.SetPaused(false);
        session.AdvanceRealSeconds(10000);
        Assert.That(pending.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(session.Workday.Summary, Is.SameAs(summary));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(summary.EndedAt.TotalMilliseconds));
    }

    [Test]
    public void GamePause_FreezesReviewTimerAndDeliveryOnTheSameClock()
    {
        var context = new WorkdaySessionTestContext();
        WorkdaySession session = context.Session;
        context.AdvanceTo(590);
        IncidentInstance incident = context.Submit("D0I1", "bob", "E_SCHEDULE");
        session.Timer.TryStart(600);
        var notifications = new List<string>();
        session.Reviews.ReviewCompleted += (job, decision) => notifications.Add("review");
        session.Timer.Completed += () => notifications.Add("timer");
        context.AdvanceTo(599);
        session.SetPaused(true);
        session.AdvanceRealSeconds(10000);
        Assert.That(session.IsPaused, Is.True);
        Assert.That(session.Now.MinuteOfDay, Is.EqualTo(599));
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(session.Timer.RemainingMilliseconds, Is.EqualTo(60000L));
        Assert.That(session.Deliveries.DeliveredIncidents.Count, Is.EqualTo(1));
        Assert.That(notifications, Is.Empty);
        session.SetPaused(false);
        context.AdvanceTo(600);
        CollectionAssert.AreEqual(new[] { "review", "timer" }, notifications);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Approved));
    }

    [Test]
    public void PausingDuringClosing_DefersRemainingHrDrain()
    {
        var context = new WorkdaySessionTestContext(LateDay());
        context.AdvanceTo(1018);
        context.Submit("closing_0", "dave", "E_DAVE_PRINTER");
        context.Submit("closing_1", "dave", "E_DAVE_PRINTER");
        context.AdvanceTo(1020);
        context.Session.SetPaused(true);
        context.Session.AdvanceRealSeconds(10000);
        Assert.That(context.Session.Workday.State, Is.EqualTo(WorkdayState.Closing));
        Assert.That(context.Session.Progress.History, Is.Empty);
        Assert.That(context.Session.Submissions.PendingReviews.Count, Is.EqualTo(2));
        context.Session.SetPaused(false);
        context.Session.AdvanceRealSeconds(10000);
        Assert.That(context.Session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(context.Session.Now.MinuteOfDay, Is.EqualTo(1033));
        Assert.That(context.Session.Progress.History.Count, Is.EqualTo(2));
    }

    [Test]
    public void DeliveryAtTimerExpiry_IsVisibleToTimerCompletionSubscriber()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        context.Submit("D0I1", "bob", "E_SCHEDULE");
        context.AdvanceTo(674);
        var order = new List<string>();
        context.Session.Deliveries.IncidentDelivered += incident => order.Add("delivery");
        context.Session.Timer.Completed += () =>
        {
            order.Add("timer");
            context.Submit("D0I2", "dave", "E_DAVE_PRINTER");
        };
        context.Session.Timer.TryStart(60);
        context.AdvanceTo(675);
        CollectionAssert.AreEqual(new[] { "delivery", "timer" }, order);
        Assert.That(context.Session.Submissions.PendingReviews.Count, Is.EqualTo(1));
        context.AdvanceTo(690);
        Assert.That(context.Session.Deliveries.GetDelivered("D0I2").Status,
            Is.EqualTo(IncidentStatus.Approved));
    }

    private static List<string> Observe(WorkdaySession session)
    {
        var trace = new List<string>();
        session.Deliveries.IncidentDelivered += incident =>
            trace.Add("delivery:" + incident.InstanceId + ":" + session.Now.TotalMilliseconds);
        session.Deadlines.IncidentOverdue += incident =>
            trace.Add("overdue:" + incident.InstanceId + ":" + session.Now.TotalMilliseconds);
        session.Workday.StateChanged += () =>
            trace.Add("state:" + session.Workday.State + ":" + session.Now.TotalMilliseconds);
        return trace;
    }

    [Test]
    public void LargeAndSplitElapsedInputs_ProduceTheSameChronologicalOutcomes()
    {
        var first = new WorkdaySessionTestContext();
        var second = new WorkdaySessionTestContext(first.Day);
        List<string> largeTrace = Observe(first.Session);
        List<string> splitTrace = Observe(second.Session);
        first.Session.AdvanceRealSeconds(10000);
        for (int i = 0; i < 1200; i++)
            second.Session.AdvanceRealSeconds(1);
        CollectionAssert.AreEqual(largeTrace, splitTrace);
        Assert.That(first.Session.Workday.State, Is.EqualTo(WorkdayState.Fired));
        Assert.That(second.Session.Now.TotalMilliseconds,
            Is.EqualTo(first.Session.Now.TotalMilliseconds));
        Assert.That(second.Session.Progress.Credibility,
            Is.EqualTo(first.Session.Progress.Credibility));
        Assert.That(second.Session.Progress.MissedDeadlines.Count, Is.EqualTo(3));
    }

    [Test]
    public void SessionsFromSameDay_HaveIndependentDiscoveryIncidentsClockAndTimer()
    {
        var first = new WorkdaySessionTestContext();
        var second = new WorkdaySessionTestContext(first.Day);
        first.AdvanceTo(590);
        IncidentInstance submitted = first.Submit("D0I1", "bob", "E_SCHEDULE");
        first.Session.Timer.TryStart(60);
        first.Session.SetPaused(true);

        Assert.That(second.Session.Now.MinuteOfDay, Is.EqualTo(540));
        Assert.That(second.Session.IsPaused, Is.False);
        Assert.That(second.Session.IsEvidenceDiscovered("E_SCHEDULE"), Is.False);
        Assert.That(second.Session.Deliveries.DeliveredIncidents, Is.Empty);
        Assert.That(second.Session.Submissions.PendingReviews, Is.Empty);
        Assert.That(second.Session.Timer.State, Is.EqualTo(DurationTimerState.Idle));
        second.AdvanceTo(600);
        IncidentInstance untouched = second.Session.Deliveries.GetDelivered("D0I1");
        Assert.That(untouched, Is.Not.SameAs(submitted));
        Assert.That(untouched.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(submitted.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(first.Session.Timer.RemainingMilliseconds, Is.EqualTo(60000L));
        first.Session.SetPaused(false);
        first.AdvanceTo(600);
        Assert.That(submitted.Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(untouched.Status, Is.EqualTo(IncidentStatus.Open));
    }
}
