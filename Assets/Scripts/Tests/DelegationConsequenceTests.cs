using System;
using System.Collections.Generic;
using NUnit.Framework;

public class DelegationConsequenceTests
{
    [TestCase("dave", false, DelegationOutcome.BestAvailable, 5, 5, 1)]
    [TestCase("steve", true, DelegationOutcome.BestAvailable, 5, 5, 1)]
    [TestCase("steve", false, DelegationOutcome.CapableAlternative, 2, 2, 1)]
    [TestCase("karen", false, DelegationOutcome.Unsuitable, -5, -5, 0)]
    [TestCase("bob", false, DelegationOutcome.Unavailable, -10, -5, 0)]
    public void AcceptedOutcome_ChangesSharedScoreDistinctTrustAndWorkload(
        string helper, bool daveBlocked, DelegationOutcome expected,
        int credibilityDelta, int trustDelta, int arranged)
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(daveBlocked));
        var session = context.Session;
        context.Advance(595);
        int notifications = 0;
        session.Consequences.ConsequenceCommitted += entry =>
        {
            notifications++;
            Assert.That(session.Consequences.IsApplying, Is.True);
            Assert.That(session.Progress.Credibility, Is.EqualTo(80 + credibilityDelta));
            Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(50 + trustDelta));
            Assert.That(session.Trust.GetTrust(helper), Is.EqualTo(50 + trustDelta));
            Assert.That(session.Workload.GetAttemptedCount(helper), Is.EqualTo(1));
            Assert.That(session.Consequences.History[0], Is.SameAs(entry));
        };
        var result = session.TryDelegate("R1", helper);
        Assert.That(result.IsAccepted, Is.True, result.Reason);
        Assert.That(result.Decision.Outcome, Is.EqualTo(expected));
        Assert.That(notifications, Is.EqualTo(1));
        var history = session.Consequences.History[0];
        Assert.That(history.Kind, Is.EqualTo(ConsequenceKind.RequestResolved));
        Assert.That(history.DayId, Is.EqualTo(session.Settings.DayId));
        Assert.That(history.DayIndex, Is.EqualTo(3));
        Assert.That(history.InstanceId, Is.EqualTo("R1"));
        Assert.That(history.CredibilityBefore, Is.EqualTo(80));
        Assert.That(history.CredibilityAfter, Is.EqualTo(80 + credibilityDelta));
        Assert.That(history.CredibilityDelta, Is.EqualTo(credibilityDelta));
        Assert.That(history.TrustChanges.Count, Is.EqualTo(2));
        Assert.That(session.Workload.GetSuitableArrangedCount(helper), Is.EqualTo(arranged));
        Assert.That(session.Workload.History[0].Decision, Is.SameAs(result.Decision));
        Assert.That(session.Trust.GetTrust(helper == "karen" ? "dave" : "karen"), Is.EqualTo(50));
        Assert.That(session.Progress.History, Is.Empty);
        Assert.That(session.Progress.MissedDeadlines, Is.Empty);
        Assert.That(session.Trust.EmployeeIds, Is.EqualTo(new[] { "alice", "bob", "dave", "steve", "karen" }));
        Assert.That(session.Consequences.IsApplying, Is.False);
        Assert.Throws<NotSupportedException>(() => ((IList<ConsequenceEntry>)session.Consequences.History).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<EmployeeTrustChange>)history.TrustChanges).Clear());
    }

    [Test]
    public void RequesterEqualsHelper_AppliesTrustOnceAndRecordsOneAssignment()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(selfHelp: true));
        context.Advance(595);
        Assert.That(context.Session.TryDelegate("R1", "alice").IsAccepted, Is.True);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(85));
        Assert.That(context.Session.Trust.GetTrust("alice"), Is.EqualTo(55));
        Assert.That(context.Session.Consequences.History[0].TrustChanges.Count, Is.EqualTo(1));
        Assert.That(context.Session.Workload.GetAttemptedCount("alice"), Is.EqualTo(1));
        Assert.That(context.Session.Workload.GetSuitableArrangedCount("alice"), Is.EqualTo(1));
    }

    [TestCase(98, 98, "dave", 100, 100)]
    [TestCase(100, 100, "dave", 100, 100)]
    [TestCase(80, 2, "bob", 70, 0)]
    [TestCase(5, 0, "bob", 0, 0)]
    public void History_RecordsActualClampedChangesEvenAtZeroOrCap(
        int credibility, int trust, string helper, int expectedCredibility, int expectedTrust)
    {
        var context = new ConsequenceTestContext(credibility: credibility, trust: trust);
        context.Advance(595);
        Assert.That(context.Session.TryDelegate("R1", helper).IsAccepted, Is.True);
        var entry = context.Session.Consequences.History[0];
        Assert.That(entry.CredibilityBefore, Is.EqualTo(credibility));
        Assert.That(entry.CredibilityAfter, Is.EqualTo(expectedCredibility));
        Assert.That(entry.CredibilityDelta, Is.EqualTo(expectedCredibility - credibility));
        foreach (var change in entry.TrustChanges)
        {
            Assert.That(change.Before, Is.EqualTo(trust));
            Assert.That(change.After, Is.EqualTo(expectedTrust));
            Assert.That(change.Delta, Is.EqualTo(expectedTrust - trust));
        }
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(expectedCredibility));
        Assert.That(context.Session.Workload.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void CustomRules_ReachTheSameScoreAndTrustOwners()
    {
        var rules = new ConsequenceRules(bestAvailableCredibilityDelta: 11, bestAvailableTrustDelta: 7);
        var context = new ConsequenceTestContext(rules: rules);
        context.Advance(595);
        context.Session.TryDelegate("R1", "dave");
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(91));
        Assert.That(context.Session.Trust.GetTrust("alice"), Is.EqualTo(57));
        Assert.That(context.Session.Trust.GetTrust("dave"), Is.EqualTo(57));
    }

    [Test]
    public void InvalidCommandsAndForgedCallbackHaveNoEffects_DuplicateDoesNotRewardAgain()
    {
        var context = new ConsequenceTestContext();
        var session = context.Session;
        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.False);
        context.Advance(595);
        Assert.That(session.TryDelegate("unknown", "dave").IsAccepted, Is.False);
        Assert.That(session.TryDelegate("R1", "unknown").IsAccepted, Is.False);
        Assert.That(session.TryDelegate("R1", "Dave").IsAccepted, Is.False);
        Assert.That(session.TryDelegate("R1", "alice").IsAccepted, Is.False);
        var forged = new DelegationDecision("R1", "dave", session.Now,
            DelegationOutcome.BestAvailable, "Help arranged.");
        ConsequenceTestContext.ReplayDecision(session.Consequences, forged);
        Assert.That(session.Progress.Credibility, Is.EqualTo(80));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(50));
        Assert.That(session.Consequences.History, Is.Empty);
        Assert.That(session.Workload.History, Is.Empty);

        var result = session.TryDelegate("R1", "dave");
        Assert.That(result.IsAccepted, Is.True);
        Assert.That(session.TryDelegate("R1", "steve").IsAccepted, Is.False);
        ConsequenceTestContext.ReplayDecision(session.Consequences, result.Decision);
        ConsequenceTestContext.ReplayDecision(session.Consequences, forged);
        Assert.That(session.Progress.Credibility, Is.EqualTo(85));
        Assert.That(session.Trust.GetTrust("dave"), Is.EqualTo(55));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Workload.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void FixedCutoff_AcceptsEqualityOrExpiresAtOneMillisecondLater_OnceOnly()
    {
        var accepted = new ConsequenceTestContext();
        accepted.Advance(630);
        Assert.That(accepted.Session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        accepted.Advance(631);
        Assert.That(accepted.Session.Progress.Credibility, Is.EqualTo(85));
        Assert.That(accepted.Session.Delegation.Expiries, Is.Empty);

        var expired = new ConsequenceTestContext();
        expired.Advance(630);
        Assert.That(expired.Session.Consequences.History, Is.Empty);
        expired.Advance(630, 0, 1);
        var session = expired.Session;
        var expiry = session.Delegation.Expiries[0];
        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.False);
        ConsequenceTestContext.ReplayExpiry(session.Consequences, expiry);
        Assert.That(session.Progress.Credibility, Is.EqualTo(70));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(45));
        Assert.That(session.Trust.GetTrust("dave"), Is.EqualTo(50));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Consequences.History[0].Kind, Is.EqualTo(ConsequenceKind.RequestExpired));
        Assert.That(session.Consequences.History[0].TrustChanges.Count, Is.EqualTo(1));
        Assert.That(session.Consequences.History[0].RecordedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(3, 630, 0, 1).TotalMilliseconds));
        Assert.That(session.Workload.History, Is.Empty);
        expired.Advance(700);
        ConsequenceTestContext.ReplayExpiry(session.Consequences, expiry);
        Assert.That(session.Progress.Credibility, Is.EqualTo(70));
    }

    [Test]
    public void ClosingExpiry_AppliesBeforeCompletedSummaryWithoutAssignmentOrHelper()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(fixedWindow: false));
        context.Advance(1020);
        var session = context.Session;
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(session.Workday.Summary.Credibility, Is.EqualTo(70));
        Assert.That(session.Progress.Credibility, Is.EqualTo(70));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(45));
        Assert.That(session.Workload.History, Is.Empty);
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Delegation.Expiries.Count, Is.EqualTo(1));
        context.Advance(1100);
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
    }

    [Test]
    public void RepeatedHelperForSameWindow_AccumulatesHistoryWithoutOccupancySimulation()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 2));
        context.Advance(595);
        Assert.That(context.Session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        Assert.That(context.Session.TryDelegate("R2", "dave").IsAccepted, Is.True);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(90));
        Assert.That(context.Session.Trust.GetTrust("dave"), Is.EqualTo(60));
        Assert.That(context.Session.Trust.GetTrust("alice"), Is.EqualTo(60));
        Assert.That(context.Session.Workload.GetAttemptedCount("dave"), Is.EqualTo(2));
        Assert.That(context.Session.Workload.GetSuitableArrangedCount("dave"), Is.EqualTo(2));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(2));
    }
}
