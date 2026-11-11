using System;
using System.Collections.Generic;
using NUnit.Framework;

public class WorkdayOpeningStateTests
{
    private static List<EmployeeTrustEntry> Trusts()
    {
        return new List<EmployeeTrustEntry> {
            new EmployeeTrustEntry("steve", 65), new EmployeeTrustEntry("alice", 0) };
    }

    [Test]
    public void CaptureAndRestore_IsolateSnapshotInputsAndTwoRuntimeCopies()
    {
        var rules = new ConsequenceRules();
        var ids = new List<string> { "steve", "alice" };
        var original = new EmployeeTrustState(ids, rules, Trusts());
        var source = new List<AssignmentHistoryEntry> { EmployeeStateTestAccess.Assignment() };
        var originalWorkload = new EmployeeWorkloadHistory(source);
        var opening = WorkdayOpeningState.Capture(80, original, originalWorkload);
        source.Clear();
        var trustInput = new List<EmployeeTrustEntry>(opening.EmployeeTrusts);
        var historyInput = new List<AssignmentHistoryEntry>(opening.Assignments);
        var first = new EmployeeTrustState(ids, rules, trustInput);
        var second = new EmployeeTrustState(ids, rules, new List<EmployeeTrustEntry>(opening.EmployeeTrusts));
        var firstWorkload = new EmployeeWorkloadHistory(historyInput);
        var secondWorkload = new EmployeeWorkloadHistory(new List<AssignmentHistoryEntry>(opening.Assignments));
        trustInput.Clear();
        historyInput.Clear();
        ids.Reverse();
        EmployeeStateTestAccess.ApplyTrust(original, "steve", -10);
        EmployeeStateTestAccess.ApplyTrust(first, "steve", 5);
        Assert.That(EmployeeStateTestAccess.Record(firstWorkload,
            EmployeeStateTestAccess.Assignment(day: "tuesday", request: "R2", dayIndex: 1)), Is.True);
        EmployeeStateTestAccess.Record(originalWorkload, EmployeeStateTestAccess.Assignment(request: "R3"));

        Assert.That(opening.Credibility, Is.EqualTo(80));
        Assert.That(opening.EmployeeTrusts[0].Trust, Is.EqualTo(65));
        Assert.That(opening.EmployeeTrusts[1].Trust, Is.Zero);
        Assert.That(opening.Assignments.Count, Is.EqualTo(1));
        Assert.That(first.GetTrust("steve"), Is.EqualTo(70));
        Assert.That(second.GetTrust("steve"), Is.EqualTo(65));
        Assert.That(first.EmployeeIds, Is.EqualTo(new[] { "steve", "alice" }));
        Assert.That(firstWorkload.GetAttemptedCount("steve"), Is.EqualTo(2));
        Assert.That(secondWorkload.GetAttemptedCount("steve"), Is.EqualTo(1));
        Assert.That(EmployeeStateTestAccess.Record(secondWorkload, opening.Assignments[0]), Is.False);
        Assert.Throws<NotSupportedException>(() => ((IList<EmployeeTrustEntry>)opening.EmployeeTrusts).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<AssignmentHistoryEntry>)opening.Assignments).Clear());
    }

    [Test]
    public void Snapshot_CopiesConstructorListsAndRestorationUsesCurrentRosterOrder()
    {
        var trusts = Trusts();
        var history = new List<AssignmentHistoryEntry> { EmployeeStateTestAccess.Assignment() };
        var opening = new WorkdayOpeningState(80, trusts, history);
        trusts.Clear();
        history.Clear();
        var restored = new EmployeeTrustState(new List<string> { "alice", "steve" },
            new ConsequenceRules(), new List<EmployeeTrustEntry>(opening.EmployeeTrusts));
        Assert.That(restored.EmployeeIds, Is.EqualTo(new[] { "alice", "steve" }));
        Assert.That(restored.GetTrust("steve"), Is.EqualTo(65));
        Assert.That(restored.GetTrust("alice"), Is.Zero);
        Assert.That(opening.Assignments.Count, Is.EqualTo(1));
    }

    [Test]
    public void TrustRestoration_RejectsMissingUnknownDuplicateNullAndOverCapValues()
    {
        var ids = new List<string> { "steve", "alice" };
        var rules = new ConsequenceRules();
        var invalid = new[] {
            new List<EmployeeTrustEntry>(),
            new List<EmployeeTrustEntry> { new EmployeeTrustEntry("steve", 65) },
            new List<EmployeeTrustEntry> { new EmployeeTrustEntry("steve", 65), new EmployeeTrustEntry("bob", 50) },
            new List<EmployeeTrustEntry> { new EmployeeTrustEntry("steve", 65), new EmployeeTrustEntry("steve", 50) },
            new List<EmployeeTrustEntry> { null, new EmployeeTrustEntry("alice", 50) } };
        foreach (var entries in invalid)
            Assert.Throws<ArgumentException>(() => new EmployeeTrustState(ids, rules, entries));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmployeeTrustState(ids, rules,
            new List<EmployeeTrustEntry> { new EmployeeTrustEntry("steve", 101), new EmployeeTrustEntry("alice", 50) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmployeeTrustEntry("steve", -1));
        Assert.Throws<ArgumentException>(() => new EmployeeTrustEntry(" ", 50));
        var custom = new EmployeeTrustState(new List<string> { "steve" },
            new ConsequenceRules(maximumTrust: 120),
            new List<EmployeeTrustEntry> { new EmployeeTrustEntry("steve", 120) });
        Assert.That(custom.GetTrust("steve"), Is.EqualTo(120));
    }

    [Test]
    public void WorkloadImport_RejectsCorruptDuplicatesInsteadOfDroppingThem()
    {
        Assert.Throws<ArgumentException>(() => new EmployeeWorkloadHistory(new List<AssignmentHistoryEntry> { null }));
        Assert.Throws<ArgumentException>(() => new EmployeeWorkloadHistory(new List<AssignmentHistoryEntry> {
            EmployeeStateTestAccess.Assignment(), EmployeeStateTestAccess.Assignment(helper: "bob") }));
        var empty = new EmployeeWorkloadHistory();
        Assert.That(empty.History, Is.Empty);
        Assert.That(empty.GetAttemptedCount("steve"), Is.Zero);
    }

    [Test]
    public void Snapshot_RejectsInvalidStructureAndAssignmentsOutsideRoster()
    {
        var noHistory = new List<AssignmentHistoryEntry>();
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkdayOpeningState(-1, Trusts(), noHistory));
        Assert.Throws<ArgumentNullException>(() => new WorkdayOpeningState(80, null, noHistory));
        Assert.Throws<ArgumentNullException>(() => new WorkdayOpeningState(80, Trusts(), null));
        Assert.Throws<ArgumentException>(() => new WorkdayOpeningState(80,
            new List<EmployeeTrustEntry> { null }, noHistory));
        Assert.Throws<ArgumentException>(() => new WorkdayOpeningState(80,
            new List<EmployeeTrustEntry> { new EmployeeTrustEntry("steve", 50), new EmployeeTrustEntry("steve", 60) }, noHistory));
        Assert.Throws<ArgumentException>(() => new WorkdayOpeningState(80, Trusts(),
            new List<AssignmentHistoryEntry> { null }));
        Assert.Throws<ArgumentException>(() => new WorkdayOpeningState(80, Trusts(),
            new List<AssignmentHistoryEntry> { EmployeeStateTestAccess.Assignment(helper: "bob") }));
        Assert.Throws<ArgumentException>(() => new WorkdayOpeningState(80, Trusts(),
            new List<AssignmentHistoryEntry> { EmployeeStateTestAccess.Assignment(), EmployeeStateTestAccess.Assignment() }));
    }

    [Test]
    public void Capture_RejectsMissingLiveStateAndEmptyRosterCanRoundTrip()
    {
        var trust = new EmployeeTrustState(new List<string>(), new ConsequenceRules());
        var workload = new EmployeeWorkloadHistory();
        Assert.Throws<ArgumentNullException>(() => WorkdayOpeningState.Capture(80, null, workload));
        Assert.Throws<ArgumentNullException>(() => WorkdayOpeningState.Capture(80, trust, null));
        var snapshot = WorkdayOpeningState.Capture(0, trust, workload);
        Assert.That(snapshot.Credibility, Is.Zero);
        Assert.That(snapshot.EmployeeTrusts, Is.Empty);
        Assert.That(snapshot.Assignments, Is.Empty);
        var restored = new EmployeeTrustState(new List<string>(), new ConsequenceRules(),
            new List<EmployeeTrustEntry>(snapshot.EmployeeTrusts));
        Assert.That(restored.EmployeeIds, Is.Empty);
    }
}
