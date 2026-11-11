using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class ConsequenceOrderingTests
{
    private static void AssertCommandsBlocked(WorkdaySession session, string requestId = "R2")
    {
        Assert.That(session.Workday.CanAcceptWork, Is.False);
        Assert.That(session.TryDelegate(requestId, "dave").IsAccepted, Is.False);
        Assert.That(session.Submissions.TrySubmit("missing", "bob", new List<string>()).IsValid, Is.False);
        Assert.That(session.TryDiscoverEvidence("unknown"), Is.False);
        Assert.That(session.TryReadEvidence("unknown"), Is.False);
        Assert.That(session.Timer.TryStart(60), Is.False);
        Assert.That(session.Timer.TryReset(), Is.False);
    }

    [Test]
    public void DelegationFiring_CommitsAllStateBeforeTerminalAndScoreNotifications()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 2), credibility: 10);
        var session = context.Session;
        context.Advance(595);
        var order = new List<string>();
        Action assertCommitted = () =>
        {
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Fired));
            Assert.That(context.Scheduler.IsStopped, Is.True);
            Assert.That(session.Progress.Credibility, Is.Zero);
            Assert.That(session.Workday.Summary.Credibility, Is.Zero);
            Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(45));
            Assert.That(session.Trust.GetTrust("bob"), Is.EqualTo(45));
            Assert.That(session.Workload.GetAttemptedCount("bob"), Is.EqualTo(1));
            Assert.That(session.Workload.GetSuitableArrangedCount("bob"), Is.Zero);
            Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
            AssertCommandsBlocked(session);
        };
        session.Workday.StateChanged += () => { assertCommitted(); order.Add("terminal"); };
        session.Progress.Changed += () => { assertCommitted(); order.Add("score"); };
        session.Consequences.ConsequenceCommitted += entry => { assertCommitted(); order.Add("consequence"); };
        session.Delegation.DecisionResolved += decision => { assertCommitted(); order.Add("source"); };

        var result = session.TryDelegate("R1", "bob");
        Assert.That(result.IsAccepted, Is.True);
        Assert.That(order, Is.EqualTo(new[] { "terminal", "score", "consequence", "source" }));
        Assert.That(session.Delegation.TryGetDelivered("R2", out var other), Is.True);
        Assert.That(other.Status, Is.EqualTo(RequestStatus.Open));
        var summary = session.Workday.Summary;
        session.AdvanceRealSeconds(10000);
        ConsequenceTestContext.ReplayDecision(session.Consequences, result.Decision);
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(new GameTime(3, 595).TotalMilliseconds));
        Assert.That(session.Workday.Summary, Is.SameAs(summary));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Trust.GetTrust("dave"), Is.EqualTo(50));
    }

    [Test]
    public void ClosingExpiryThatFires_StopsLaterExpiryAndNeverPublishesCompleted()
    {
        var context = new ConsequenceTestContext(
            ConsequenceTestContext.CreateDay(fixedWindow: false, requestCount: 2), credibility: 10);
        var session = context.Session;
        int terminalEvents = 0;
        session.Workday.StateChanged += () =>
        {
            terminalEvents++;
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Fired));
            Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
            Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
            Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(45));
            Assert.That(session.Delegation.Expiries.Count, Is.EqualTo(1));
            Assert.That(session.Workload.History, Is.Empty);
            Assert.That(session.Workday.Summary.Credibility, Is.Zero);
            AssertCommandsBlocked(session);
        };
        context.Advance(1100);
        Assert.That(terminalEvents, Is.EqualTo(1));
        Assert.That(session.Delegation.TryGetDelivered("R2", out var other), Is.True);
        Assert.That(other.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Scheduler.PendingCount, Is.Zero);
        ConsequenceTestContext.ReplayExpiry(session.Consequences, session.Delegation.Expiries[0]);
        Assert.That(session.Progress.Credibility, Is.Zero);
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void MultipleClosingExpiries_SettleBeforeClosingRemovesNonHrEvents()
    {
        var context = new ConsequenceTestContext(
            ConsequenceTestContext.CreateDay(fixedWindow: false, requestCount: 2));
        var session = context.Session;
        var order = new List<string>();
        session.Delegation.RequestExpired += entry =>
        {
            order.Add(entry.RequestInstanceId);
            int count = order.Count;
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Working));
            Assert.That(session.Progress.Credibility, Is.EqualTo(80 - count * 10));
            Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(50 - count * 5));
            Assert.That(session.Consequences.History.Count, Is.EqualTo(count));
            Assert.That(session.Workload.History, Is.Empty);
            Assert.That(session.Workday.Summary, Is.Null);
            AssertCommandsBlocked(session);
        };
        session.Workday.StateChanged += () =>
        {
            order.Add("completed");
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
            Assert.That(session.Workday.Summary.Credibility, Is.EqualTo(60));
            Assert.That(session.Consequences.History.Count, Is.EqualTo(2));
            Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(40));
        };
        context.Advance(1100);
        Assert.That(order, Is.EqualTo(new[] { "R1", "R2", "completed" }));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
    }

    private static DayData LateHrDay()
    {
        var definition = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
        Assert.That(definition, Is.Not.Null);
        var monday = definition.GetData();
        var printer = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
        Assert.That(printer, Is.Not.Null);
        var incident = new DayIncidentPlan(printer, new IncidentScheduleEntry(
            "late_printer", printer.IncidentId, new TimeRange(0, 635, 640), 1000, 1019), "dave");
        return new DayData(monday.ScheduleData,
            new List<EvidenceDefinition>(monday.EvidenceDatabase.All),
            new List<DayIncidentPlan> { incident },
            new List<DayRequestPlan> { monday.Requests[1] });
    }

    private static QueuedHrReview SubmitUnsupportedLatePrinter(ConsequenceTestContext context)
    {
        var session = context.Session;
        Assert.That(session.TryDiscoverEvidence("E_OFFICE_SOCIAL"), Is.True);
        Assert.That(session.Submissions.TrySubmit("late_printer", "dave",
            new List<string> { "E_OFFICE_SOCIAL" }).IsValid, Is.True);
        return session.Submissions.PendingReviews[0];
    }

    [Test]
    public void HrReviewAtClosing_PrecedesExpiryAndSummaryUsesBothPenalties()
    {
        var context = new ConsequenceTestContext(LateHrDay());
        var session = context.Session;
        context.Advance(1005);
        var review = SubmitUnsupportedLatePrinter(context);
        Assert.That(review.ReviewAt.TotalMilliseconds, Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
        var order = new List<string>();
        session.Reviews.ReviewCompleted += (job, result) =>
        {
            order.Add("hr");
            Assert.That(session.Progress.Credibility, Is.EqualTo(55));
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Working));
        };
        session.Delegation.RequestExpired += entry =>
        {
            order.Add("expiry");
            Assert.That(session.Progress.Credibility, Is.EqualTo(45));
            Assert.That(session.Consequences.History[1].CredibilityBefore, Is.EqualTo(55));
            Assert.That(session.Consequences.History[1].CredibilityAfter, Is.EqualTo(45));
        };
        session.Workday.StateChanged += () => order.Add("completed");
        context.Advance(1020);
        Assert.That(order, Is.EqualTo(new[] { "hr", "expiry", "completed" }));
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(session.Workday.Summary.Credibility, Is.EqualTo(45));
        Assert.That(session.Progress.History[0].CredibilityBefore, Is.EqualTo(80));
        Assert.That(session.Progress.History[0].CredibilityAfter, Is.EqualTo(55));
        Assert.That(session.Trust.GetTrust("dave"), Is.EqualTo(50));
        Assert.That(session.Trust.GetTrust("karen"), Is.EqualTo(45));
    }

    [Test]
    public void PendingHrAfterClosing_UsesPostExpiryCredibilityAndCannotReapplyExpiry()
    {
        var context = new ConsequenceTestContext(LateHrDay());
        var session = context.Session;
        context.Advance(1018, 30);
        var review = SubmitUnsupportedLatePrinter(context);
        context.Advance(1020);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Closing));
        Assert.That(session.Progress.Credibility, Is.EqualTo(70));
        Assert.That(session.Trust.GetTrust("karen"), Is.EqualTo(45));
        Assert.That(session.Workday.Summary, Is.Null);
        AssertCommandsBlocked(session, "D0R2");
        ConsequenceTestContext.ReplayExpiry(session.Consequences, session.Delegation.Expiries[0]);
        context.Scheduler.AdvanceTo(review.ReviewAt);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(session.Workday.Summary.Credibility, Is.EqualTo(45));
        Assert.That(session.Progress.History[0].CredibilityBefore, Is.EqualTo(70));
        Assert.That(session.Progress.History[0].CredibilityAfter, Is.EqualTo(45));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(2));
        Assert.That(session.Delegation.Expiries.Count, Is.EqualTo(1));
        Assert.That(session.Trust.GetTrust("dave"), Is.EqualTo(50));
    }

    [Test]
    public void ReentrantCommandsAndTimeAdvance_AreBlockedUntilAllNotificationsReturn()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 2));
        var session = context.Session;
        context.Advance(595);
        int timeNotifications = 0;
        int scoreNotifications = 0;
        session.TimeAdvanced += () => timeNotifications++;
        Action verify = () =>
        {
            Assert.That(session.Consequences.IsApplying, Is.True);
            Assert.That(session.Progress.Credibility, Is.EqualTo(85));
            Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(55));
            Assert.That(session.Trust.GetTrust("dave"), Is.EqualTo(55));
            Assert.That(session.Workload.History.Count, Is.EqualTo(1));
            Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
            AssertCommandsBlocked(session);
            session.AdvanceRealSeconds(1000);
            Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(new GameTime(3, 595).TotalMilliseconds));
            Assert.That(timeNotifications, Is.Zero);
        };
        Action scoreObserver = () => { scoreNotifications++; verify(); };
        Action<ConsequenceEntry> consequenceObserver = entry => verify();
        session.Progress.Changed += scoreObserver;
        session.Consequences.ConsequenceCommitted += consequenceObserver;
        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        Assert.That(scoreNotifications, Is.EqualTo(1));
        Assert.That(session.Consequences.IsApplying, Is.False);
        Assert.That(session.Delegation.TryGetDelivered("R2", out var other), Is.True);
        Assert.That(other.Status, Is.EqualTo(RequestStatus.Open));
        session.Progress.Changed -= scoreObserver;
        session.Consequences.ConsequenceCommitted -= consequenceObserver;
        Assert.That(session.Workday.CanAcceptWork, Is.True);
        Assert.That(session.TryDelegate("R2", "dave").IsAccepted, Is.True);
        Assert.That(session.Progress.Credibility, Is.EqualTo(90));
    }

    [TestCase("score", false)]
    [TestCase("consequence", false)]
    [TestCase("score", true)]
    [TestCase("terminal", true)]
    public void ThrowingObserver_CannotRollbackDuplicateOrDelayFiring(string observer, bool firing)
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 2),
            credibility: firing ? 10 : 80);
        var session = context.Session;
        context.Advance(595);
        var failure = new InvalidOperationException("Intentional test observer failure.");
        Action throwSimple = () => throw failure;
        Action<ConsequenceEntry> throwEntry = entry => throw failure;
        if (observer == "score") session.Progress.Changed += throwSimple;
        if (observer == "consequence") session.Consequences.ConsequenceCommitted += throwEntry;
        if (observer == "terminal") session.Workday.StateChanged += throwSimple;

        var caught = Assert.Throws<InvalidOperationException>(() =>
            session.TryDelegate("R1", firing ? "bob" : "dave"));
        Assert.That(caught, Is.SameAs(failure));
        Assert.That(session.Consequences.IsApplying, Is.False);
        Assert.That(session.Progress.Credibility, Is.EqualTo(firing ? 0 : 85));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(firing ? 45 : 55));
        Assert.That(session.Workload.History.Count, Is.EqualTo(1));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Delegation.TryGetDelivered("R1", out var resolved), Is.True);
        Assert.That(resolved.Status, Is.EqualTo(RequestStatus.Resolved));
        ConsequenceTestContext.ReplayDecision(session.Consequences, resolved.Decision);
        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.False);

        session.Progress.Changed -= throwSimple;
        session.Consequences.ConsequenceCommitted -= throwEntry;
        session.Workday.StateChanged -= throwSimple;
        if (firing)
        {
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Fired));
            Assert.That(context.Scheduler.IsStopped, Is.True);
            Assert.That(session.Workday.Summary.Credibility, Is.Zero);
            AssertCommandsBlocked(session);
        }
        else
        {
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Working));
            Assert.That(session.TryDelegate("R2", "dave").IsAccepted, Is.True);
            Assert.That(session.Progress.Credibility, Is.EqualTo(90));
        }
    }
}
