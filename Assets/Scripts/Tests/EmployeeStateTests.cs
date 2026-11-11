using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

// Test-only access to internal mutations; no runtime visibility/API changes.
internal static class EmployeeStateTestAccess
{
    public static int ApplyTrust(EmployeeTrustState state, string id, int delta)
    {
        var method = typeof(EmployeeTrustState).GetMethod(
            "ApplyTrustDelta", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var apply = (Func<string, int, int>)Delegate.CreateDelegate(
            typeof(Func<string, int, int>), state, method);
        return apply(id, delta);
    }

    public static bool Record(EmployeeWorkloadHistory state, AssignmentHistoryEntry entry)
    {
        var method = typeof(EmployeeWorkloadHistory).GetMethod(
            "TryRecord", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var record = (Func<AssignmentHistoryEntry, bool>)Delegate.CreateDelegate(
            typeof(Func<AssignmentHistoryEntry, bool>), state, method);
        return record(entry);
    }

    public static AssignmentHistoryEntry Assignment(
        string day = "monday", string request = "R1", string helper = "steve",
        DelegationOutcome outcome = DelegationOutcome.BestAvailable, int dayIndex = 0)
    {
        return new AssignmentHistoryEntry(day, new DelegationDecision(
            request, helper, new GameTime(dayIndex, 600, 12, 345), outcome,
            "Help arranged for the requested window."));
    }
}

public class EmployeeStateTests
{
    [TestCase(DelegationOutcome.BestAvailable, 5, 5)]
    [TestCase(DelegationOutcome.CapableAlternative, 2, 2)]
    [TestCase(DelegationOutcome.Unsuitable, -5, -5)]
    [TestCase(DelegationOutcome.Unavailable, -10, -5)]
    public void Rules_DefaultOutcomeDeltas(DelegationOutcome outcome, int credibility, int trust)
    {
        var rules = new ConsequenceRules();
        Assert.That(rules.GetCredibilityDelta(outcome), Is.EqualTo(credibility));
        Assert.That(rules.GetTrustDelta(outcome), Is.EqualTo(trust));
        Assert.That(rules.StartingTrust, Is.EqualTo(50));
        Assert.That(rules.MaximumTrust, Is.EqualTo(100));
        Assert.That(rules.UnansweredCredibilityDelta, Is.EqualTo(-10));
        Assert.That(rules.UnansweredTrustDelta, Is.EqualTo(-5));
        Assert.That(rules.WrongfulBlameTrustDelta, Is.EqualTo(-10));
    }

    [Test]
    public void Rules_CustomDeltasAreUsedAndUndefinedOutcomeRejected()
    {
        var rules = new ConsequenceRules(0, 120, 11, 12, -13, -14,
            21, 22, -23, -24, -31, -32, -33);
        var outcomes = new[] { DelegationOutcome.BestAvailable,
            DelegationOutcome.CapableAlternative, DelegationOutcome.Unsuitable,
            DelegationOutcome.Unavailable };
        for (int i = 0; i < outcomes.Length; i++)
        {
            Assert.That(rules.GetCredibilityDelta(outcomes[i]),
                Is.EqualTo(new[] { 11, 12, -13, -14 }[i]));
            Assert.That(rules.GetTrustDelta(outcomes[i]),
                Is.EqualTo(new[] { 21, 22, -23, -24 }[i]));
        }
        Assert.That(rules.UnansweredCredibilityDelta, Is.EqualTo(-31));
        Assert.That(rules.UnansweredTrustDelta, Is.EqualTo(-32));
        Assert.That(rules.WrongfulBlameTrustDelta, Is.EqualTo(-33));
        Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetCredibilityDelta((DelegationOutcome)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetTrustDelta((DelegationOutcome)99));
    }

    [TestCase(-1, 100)]
    [TestCase(101, 100)]
    [TestCase(0, 0)]
    [TestCase(0, -1)]
    public void Rules_InvalidTrustBoundsRejected(int starting, int maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsequenceRules(starting, maximum));
    }

    [Test]
    public void Trust_CopiesRosterPreservesOrderAndIsolatesEmployeesAndSessions()
    {
        var ids = new List<string> { "steve", "alice" };
        var rules = new ConsequenceRules();
        var first = new EmployeeTrustState(ids, rules);
        var second = new EmployeeTrustState(ids, rules);
        ids.Clear();
        Assert.That(first.EmployeeIds, Is.EqualTo(new[] { "steve", "alice" }));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)first.EmployeeIds).Clear());
        Assert.That(EmployeeStateTestAccess.ApplyTrust(first, "steve", 5), Is.EqualTo(55));
        Assert.That(first.GetTrust("alice"), Is.EqualTo(50));
        Assert.That(second.GetTrust("steve"), Is.EqualTo(50));
    }

    [TestCase(50, 5, 55)]
    [TestCase(98, 5, 100)]
    [TestCase(100, 5, 100)]
    [TestCase(2, -5, 0)]
    [TestCase(0, -5, 0)]
    [TestCase(0, 5, 5)]
    [TestCase(50, 0, 50)]
    [TestCase(100, int.MaxValue, 100)]
    [TestCase(100, int.MinValue, 0)]
    public void Trust_MutationReturnsActualClampedValue(int opening, int delta, int expected)
    {
        var state = new EmployeeTrustState(new List<string> { "steve" },
            new ConsequenceRules(), new List<EmployeeTrustEntry> { new EmployeeTrustEntry("steve", opening) });
        Assert.That(EmployeeStateTestAccess.ApplyTrust(state, "steve", delta), Is.EqualTo(expected));
        Assert.That(state.GetTrust("steve"), Is.EqualTo(expected));
    }

    [Test]
    public void Trust_ConfiguredMaximumAndExtremeDeltasDoNotOverflow()
    {
        var state = new EmployeeTrustState(new List<string> { "steve" },
            new ConsequenceRules(maximumTrust: int.MaxValue));
        Assert.That(EmployeeStateTestAccess.ApplyTrust(state, "steve", int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(EmployeeStateTestAccess.ApplyTrust(state, "steve", int.MinValue), Is.Zero);
    }

    [Test]
    public void Trust_InvalidRosterOrLookupCannotCreateEmployees()
    {
        Assert.Throws<ArgumentNullException>(() => new EmployeeTrustState(null, new ConsequenceRules()));
        Assert.Throws<ArgumentNullException>(() => new EmployeeTrustState(new List<string>(), null));
        foreach (string id in new string[] { null, "", " " })
            Assert.Throws<ArgumentException>(() => new EmployeeTrustState(new List<string> { id }, new ConsequenceRules()));
        Assert.Throws<ArgumentException>(() => new EmployeeTrustState(new List<string> { "steve", "steve" }, new ConsequenceRules()));
        var state = new EmployeeTrustState(new List<string> { "steve" }, new ConsequenceRules());
        Assert.Throws<ArgumentException>(() => state.GetTrust(" "));
        Assert.Throws<KeyNotFoundException>(() => state.GetTrust("Steve"));
        Assert.Throws<KeyNotFoundException>(() => EmployeeStateTestAccess.ApplyTrust(state, "unknown", 5));
        Assert.That(state.EmployeeIds, Is.EqualTo(new[] { "steve" }));
        Assert.That(state.GetTrust("steve"), Is.EqualTo(50));
    }

    [TestCase(DelegationOutcome.BestAvailable, true)]
    [TestCase(DelegationOutcome.CapableAlternative, true)]
    [TestCase(DelegationOutcome.Unsuitable, false)]
    [TestCase(DelegationOutcome.Unavailable, false)]
    public void Workload_EveryAcceptedChoiceIsAnAttemptOnlySuitableChoicesAreArranged(
        DelegationOutcome outcome, bool suitable)
    {
        var entry = EmployeeStateTestAccess.Assignment(outcome: outcome);
        var state = new EmployeeWorkloadHistory();
        Assert.That(EmployeeStateTestAccess.Record(state, entry), Is.True);
        Assert.That(entry.IsSuitableArranged, Is.EqualTo(suitable));
        Assert.That(entry.DayIndex, Is.Zero);
        Assert.That(entry.SubmittedAt.TotalMilliseconds, Is.EqualTo(new GameTime(0, 600, 12, 345).TotalMilliseconds));
        Assert.That(state.GetAttemptedCount("steve"), Is.EqualTo(1));
        Assert.That(state.GetSuitableArrangedCount("steve"), Is.EqualTo(suitable ? 1 : 0));
        Assert.That(state.GetAttemptedCount("alice"), Is.Zero);
        Assert.That(state.GetAttemptedCount("Steve"), Is.Zero);
    }

    [Test]
    public void Workload_DuplicateRequestCannotCountAgainWithDifferentHelperOrOutcome()
    {
        var state = new EmployeeWorkloadHistory();
        var view = state.History;
        var first = EmployeeStateTestAccess.Assignment(outcome: DelegationOutcome.Unavailable);
        Assert.That(EmployeeStateTestAccess.Record(state, first), Is.True);
        Assert.That(EmployeeStateTestAccess.Record(state, first), Is.False);
        Assert.That(EmployeeStateTestAccess.Record(state, EmployeeStateTestAccess.Assignment(helper: "bob")), Is.False);
        Assert.That(view.Count, Is.EqualTo(1));
        Assert.That(view[0], Is.SameAs(first));
        Assert.That(state.GetAttemptedCount("bob"), Is.Zero);
        Assert.That(state.GetSuitableArrangedCount("steve"), Is.Zero);
        Assert.Throws<NotSupportedException>(() => ((IList<AssignmentHistoryEntry>)view).Clear());
        Assert.That(EmployeeStateTestAccess.Record(state,
            EmployeeStateTestAccess.Assignment(day: "tuesday", dayIndex: 1)), Is.True);
        Assert.That(EmployeeStateTestAccess.Record(state,
            EmployeeStateTestAccess.Assignment(request: "R2", outcome: DelegationOutcome.CapableAlternative)), Is.True);
        Assert.That(state.GetAttemptedCount("steve"), Is.EqualTo(3));
        Assert.That(state.GetSuitableArrangedCount("steve"), Is.EqualTo(2));
    }

    [Test]
    public void Workload_InvalidEntriesAndBlankQueriesRejected()
    {
        var state = new EmployeeWorkloadHistory();
        Assert.Throws<ArgumentNullException>(() => EmployeeStateTestAccess.Record(state, null));
        Assert.Throws<ArgumentException>(() => state.GetAttemptedCount(" "));
        Assert.Throws<ArgumentException>(() => state.GetSuitableArrangedCount(null));
        Assert.Throws<ArgumentException>(() => new AssignmentHistoryEntry(" ", EmployeeStateTestAccess.Assignment().Decision));
        Assert.Throws<ArgumentNullException>(() => new AssignmentHistoryEntry("monday", null));
        Assert.That(state.History, Is.Empty);
    }
}
