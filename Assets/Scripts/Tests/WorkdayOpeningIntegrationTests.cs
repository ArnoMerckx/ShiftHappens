using System;
using System.Collections.Generic;
using NUnit.Framework;

public class WorkdayOpeningIntegrationTests
{
    private static List<EmployeeTrustEntry> TrustEntries(DayData day, int value = 50)
    {
        var entries = new List<EmployeeTrustEntry>();
        foreach (string id in day.ScheduleData.EmployeeIds)
            entries.Add(new EmployeeTrustEntry(id, value));
        return entries;
    }

    [Test]
    public void FreshSession_UsesConfiguredDefaultsAndEmptyHistories()
    {
        var day = ConsequenceTestContext.CreateDay();
        var defaults = new WorkdaySession(day);
        Assert.That(defaults.Progress.Credibility, Is.EqualTo(100));
        Assert.That(defaults.Trust.GetTrust("alice"), Is.EqualTo(50));
        var custom = new WorkdaySession(day,
            new CredibilityRules(startingCredibility: 80),
            new ConsequenceRules(startingTrust: 17));
        Assert.That(custom.Progress.Credibility, Is.EqualTo(80));
        foreach (string id in day.ScheduleData.EmployeeIds)
            Assert.That(custom.Trust.GetTrust(id), Is.EqualTo(17));
        Assert.That(custom.Workload.History, Is.Empty);
        Assert.That(custom.Consequences.History, Is.Empty);
        Assert.That(custom.Progress.History, Is.Empty);
        Assert.That(custom.Progress.MissedDeadlines, Is.Empty);
        Assert.That(custom.Delegation.DeliveredRequests, Is.Empty);
        Assert.That(custom.Submissions.PendingReviews, Is.Empty);
        Assert.That(custom.Now.TotalMilliseconds, Is.EqualTo(custom.Settings.OpensAt.TotalMilliseconds));
    }

    [Test]
    public void OpeningValuesAndPriorWork_AreCopiedIntoIndependentComposedSessions()
    {
        var day = ConsequenceTestContext.CreateDay();
        var trusts = new List<EmployeeTrustEntry> {
            new EmployeeTrustEntry("karen", 100), new EmployeeTrustEntry("steve", 35),
            new EmployeeTrustEntry("dave", 98), new EmployeeTrustEntry("bob", 0),
            new EmployeeTrustEntry("alice", 61) };
        var assignments = new List<AssignmentHistoryEntry> {
            EmployeeStateTestAccess.Assignment(day: "prior_day", request: "R1", helper: "dave", dayIndex: 2),
            EmployeeStateTestAccess.Assignment(day: "prior_day", request: "R2", helper: "steve",
                outcome: DelegationOutcome.Unavailable, dayIndex: 2) };
        var opening = new WorkdayOpeningState(98, trusts, assignments);
        var first = new ConsequenceTestContext(day, opening: opening);
        var second = new ConsequenceTestContext(day, opening: opening);
        trusts.Clear();
        assignments.Clear();
        Assert.That(first.Session.Trust.EmployeeIds, Is.EqualTo(day.ScheduleData.EmployeeIds));
        Assert.That(first.Session.Progress.Credibility, Is.EqualTo(98));
        Assert.That(first.Session.Workload.GetAttemptedCount("dave"), Is.EqualTo(1));
        Assert.That(first.Session.Workload.GetSuitableArrangedCount("dave"), Is.EqualTo(1));
        Assert.That(first.Session.Workload.GetAttemptedCount("steve"), Is.EqualTo(1));
        Assert.That(first.Session.Workload.GetSuitableArrangedCount("steve"), Is.Zero);
        Assert.That(first.Session.Consequences.History, Is.Empty);

        first.Advance(595);
        Assert.That(first.Session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        Assert.That(first.Session.Progress.Credibility, Is.EqualTo(100));
        Assert.That(first.Session.Trust.GetTrust("dave"), Is.EqualTo(100));
        Assert.That(first.Session.Trust.GetTrust("alice"), Is.EqualTo(66));
        Assert.That(first.Session.Trust.GetTrust("bob"), Is.Zero);
        Assert.That(first.Session.Workload.GetAttemptedCount("dave"), Is.EqualTo(2));
        Assert.That(first.Session.Workload.GetSuitableArrangedCount("dave"), Is.EqualTo(2));
        Assert.That(first.Session.Consequences.History[0].CredibilityDelta, Is.EqualTo(2));
        foreach (var change in first.Session.Consequences.History[0].TrustChanges)
            Assert.That(change.Delta, Is.EqualTo(change.EmployeeId == "dave" ? 2 : 5));

        Assert.That(second.Session.Progress.Credibility, Is.EqualTo(98));
        Assert.That(second.Session.Trust.GetTrust("dave"), Is.EqualTo(98));
        Assert.That(second.Session.Trust.GetTrust("alice"), Is.EqualTo(61));
        Assert.That(second.Session.Workload.History.Count, Is.EqualTo(2));
        Assert.That(second.Session.Consequences.History, Is.Empty);
        Assert.That(second.Session.Delegation.DeliveredRequests, Is.Empty);
        Assert.That(second.Session.Now.TotalMilliseconds, Is.EqualTo(second.Session.Settings.OpensAt.TotalMilliseconds));
        Assert.That(opening.Credibility, Is.EqualTo(98));
        Assert.That(opening.EmployeeTrusts[2].Trust, Is.EqualTo(98));
        Assert.That(opening.Assignments.Count, Is.EqualTo(2));
    }

    private static DayData NextDay(DayData previous)
    {
        int index = previous.ScheduleData.Settings.DayIndex + 1;
        var ids = new List<string>(previous.ScheduleData.EmployeeIds);
        var availability = new List<EmployeeAvailability>();
        foreach (string id in ids)
            availability.Add(new EmployeeAvailability(id, new TimeRange(index, 540, 1020)));
        var schedule = new DayScheduleData(
            new WorkdaySettings("next_test_day", "Next test day", index, 540, 1020, 40),
            ids, new List<ApprovedScheduleRecord>(), new List<ApprovedScheduleRecord>(),
            new List<Claim>(), ordinaryAvailability: availability);
        var source = previous.Requests[0];
        var request = new DayRequestPlan(source.InstanceId, source.Request, source.Profile,
            new GameTime(index, 595), new TimeRange(index, 630, 645));
        return new DayData(schedule, new List<EvidenceDefinition>(),
            new List<DayIncidentPlan>(), new List<DayRequestPlan> { request });
    }

    [Test]
    public void CompletedDayState_CanSeedNextDayWithoutSharingUnfinishedDayChanges()
    {
        var first = new ConsequenceTestContext();
        first.Advance(595);
        Assert.That(first.Session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        first.Advance(1020);
        Assert.That(first.Session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        var opening = WorkdayOpeningState.Capture(first.Session.Progress.Credibility,
            first.Session.Trust, first.Session.Workload);
        var nextDay = NextDay(first.Day);
        var next = new ConsequenceTestContext(nextDay, opening: opening);
        Assert.That(next.Session.Progress.Credibility, Is.EqualTo(85));
        Assert.That(next.Session.Trust.GetTrust("alice"), Is.EqualTo(55));
        Assert.That(next.Session.Trust.GetTrust("dave"), Is.EqualTo(55));
        Assert.That(next.Session.Workload.GetAttemptedCount("dave"), Is.EqualTo(1));
        Assert.That(next.Session.Consequences.History, Is.Empty);
        next.Advance(595);
        Assert.That(next.Session.TryDelegate("R1", "bob").IsAccepted, Is.True);
        Assert.That(next.Session.Progress.Credibility, Is.EqualTo(80));
        Assert.That(next.Session.Trust.GetTrust("bob"), Is.EqualTo(45));
        Assert.That(next.Session.Trust.GetTrust("alice"), Is.EqualTo(50));
        Assert.That(next.Session.Workload.History.Count, Is.EqualTo(2));
        Assert.That(next.Session.Workload.GetSuitableArrangedCount("bob"), Is.Zero);

        var independentOpening = new ConsequenceTestContext(nextDay, opening: opening);
        Assert.That(independentOpening.Session.Progress.Credibility, Is.EqualTo(85));
        Assert.That(independentOpening.Session.Trust.GetTrust("bob"), Is.EqualTo(50));
        Assert.That(independentOpening.Session.Trust.GetTrust("alice"), Is.EqualTo(55));
        Assert.That(independentOpening.Session.Workload.History.Count, Is.EqualTo(1));
        Assert.That(independentOpening.Session.Consequences.History, Is.Empty);
        Assert.That(first.Session.Progress.Credibility, Is.EqualTo(85));
        Assert.That(first.Session.Workload.History.Count, Is.EqualTo(1));
        Assert.That(opening.Assignments.Count, Is.EqualTo(1));
    }

    [TestCase(0, 0)]
    [TestCase(10, 10)]
    [TestCase(101, 0)]
    public void Session_RejectsOpeningCredibilityOutsidePlayableRules(int credibility, int threshold)
    {
        var day = ConsequenceTestContext.CreateDay();
        var opening = new WorkdayOpeningState(credibility, TrustEntries(day), new List<AssignmentHistoryEntry>());
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkdaySession(day,
            new CredibilityRules(firingThreshold: threshold), openingState: opening));
    }

    [Test]
    public void Session_RejectsMissingExtraOrOverCapOpeningTrust()
    {
        var day = ConsequenceTestContext.CreateDay();
        var missing = TrustEntries(day);
        missing.RemoveAt(0);
        var extra = TrustEntries(day);
        extra.Add(new EmployeeTrustEntry("outside_roster", 50));
        foreach (var entries in new[] { missing, extra })
        {
            var opening = new WorkdayOpeningState(80, entries, new List<AssignmentHistoryEntry>());
            Assert.Throws<ArgumentException>(() => new WorkdaySession(day, openingState: opening));
        }
        var aboveCap = new WorkdayOpeningState(80, TrustEntries(day, 101), new List<AssignmentHistoryEntry>());
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkdaySession(day, openingState: aboveCap));
    }

    [TestCase("current_index", 3)]
    [TestCase("future_index", 4)]
    [TestCase("consequence_day", 2)]
    public void Session_RejectsCurrentFutureOrSameDayIdentityAssignments(string dayId, int dayIndex)
    {
        var day = ConsequenceTestContext.CreateDay();
        var assignment = EmployeeStateTestAccess.Assignment(day: dayId, helper: "dave", dayIndex: dayIndex);
        var opening = new WorkdayOpeningState(80, TrustEntries(day),
            new List<AssignmentHistoryEntry> { assignment });
        Assert.Throws<ArgumentException>(() => new WorkdaySession(day, openingState: opening));
    }

    [Test]
    public void ConfiguredCaps_AreSharedByOpeningValidationMutationAndHistory()
    {
        var day = ConsequenceTestContext.CreateDay();
        var opening = new WorkdayOpeningState(119, TrustEntries(day, 129), new List<AssignmentHistoryEntry>());
        var context = new ConsequenceTestContext(day,
            scoreRules: new CredibilityRules(maximumCredibility: 120),
            rules: new ConsequenceRules(maximumTrust: 130), opening: opening);
        context.Advance(595);
        Assert.That(context.Session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        var entry = context.Session.Consequences.History[0];
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(120));
        Assert.That(entry.CredibilityBefore, Is.EqualTo(119));
        Assert.That(entry.CredibilityAfter, Is.EqualTo(120));
        foreach (var change in entry.TrustChanges)
        {
            Assert.That(change.Before, Is.EqualTo(129));
            Assert.That(change.After, Is.EqualTo(130));
            Assert.That(change.Delta, Is.EqualTo(1));
        }
        Assert.That(opening.Credibility, Is.EqualTo(119));
        Assert.That(opening.EmployeeTrusts[0].Trust, Is.EqualTo(129));
    }
}
