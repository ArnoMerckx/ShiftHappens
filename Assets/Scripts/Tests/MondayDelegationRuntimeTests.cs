using System;
using System.Collections.Generic;
using NUnit.Framework;

public class MondayDelegationRuntimeTests
{
    private static RequestOverviewViewData Request(WorkdaySession session, string id)
    {
        foreach (var entry in session.GetRequestOverview())
            if (entry.InstanceId == id) return entry;
        Assert.Fail("Missing Monday request: " + id);
        return null;
    }

    private static WorkdaySessionTestContext ReachArchiveRequest()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(630);
        context.Submit("D0I1", "bob", "E_SCHEDULE");
        context.AdvanceTo(720);
        context.Submit("D0I2", "dave", "E_DAVE_PRINTER");
        context.AdvanceTo(860);
        return context;
    }

    [TestCase("steve", DelegationOutcome.BestAvailable)]
    [TestCase("dave", DelegationOutcome.Unavailable)]
    [TestCase("bob", DelegationOutcome.Unavailable)]
    [TestCase("karen", DelegationOutcome.Unsuitable)]
    public void PcRequest_UsesActualMondaySkillsAndCommitments(
        string helper, DelegationOutcome expected)
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(594, 59);
        Assert.That(context.Session.GetRequestOverview(), Is.Empty);
        var early = context.Session.TryDelegate("D0R1", helper);
        Assert.That(early.IsAccepted, Is.False);
        context.AdvanceTo(595);
        var request = Request(context.Session, "D0R1");
        Assert.That(request.Request.RequesterId, Is.EqualTo("alice"));
        Assert.That(request.Request.RequestId, Is.EqualTo("alice_pc_help"));
        Assert.That(request.HelpWindow.Value.StartMinute, Is.EqualTo(630));
        Assert.That(request.HelpWindow.Value.EndMinute, Is.EqualTo(645));
        var result = context.Session.TryDelegate("D0R1", helper);
        Assert.That(result.IsAccepted, Is.True, result.Reason);
        Assert.That(result.Decision.Outcome, Is.EqualTo(expected));
        Assert.That(result.Decision.RequestInstanceId, Is.EqualTo("D0R1"));
        Assert.That(result.Decision.HelperId, Is.EqualTo(helper));
        Assert.That(result.Decision.SubmittedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 595).TotalMilliseconds));
        Assert.That(Request(context.Session, "D0R1").Status, Is.EqualTo(RequestStatus.Resolved));
    }

    [TestCase(600)]
    [TestCase(630)]
    public void PcRequest_SteveRemainsBestAtDifferentValidTimesIncludingExactCutoff(int minute)
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(minute);
        Assert.That(context.Session.TryDelegate("D0R1", "steve").Decision.Outcome,
            Is.EqualTo(DelegationOutcome.BestAvailable));
    }

    [TestCase("bob", DelegationOutcome.BestAvailable)]
    [TestCase("steve", DelegationOutcome.CapableAlternative)]
    [TestCase("alice", DelegationOutcome.Unsuitable)]
    [TestCase("dave", DelegationOutcome.Unsuitable)]
    public void ArchiveRequest_UsesActualLateDayCoverageAndProfile(
        string helper, DelegationOutcome expected)
    {
        var context = ReachArchiveRequest();
        var session = context.Session;
        var request = Request(session, "D0R2");
        Assert.That(request.Request.RequesterId, Is.EqualTo("karen"));
        Assert.That(request.Request.RequestId, Is.EqualTo("karen_archive_boxes"));
        Assert.That(request.HelpWindow.HasValue, Is.False);
        Assert.That(request.SubmissionCutoff.TotalMilliseconds,
            Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
        var result = session.TryDelegate("D0R2", helper);
        Assert.That(result.IsAccepted, Is.True, result.Reason);
        Assert.That(result.Decision.Outcome, Is.EqualTo(expected));
        Assert.That(result.Decision.RequestInstanceId, Is.EqualTo("D0R2"));
        Assert.That(Request(session, "D0R2").Status, Is.EqualTo(RequestStatus.Resolved));
    }

    [Test]
    public void AuthoredSelfHelpPolicy_RejectsEachRequesterWithoutConsumingRequest()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(600);
        Assert.That(context.Session.TryDelegate("D0R1", "alice").IsAccepted, Is.False);
        Assert.That(Request(context.Session, "D0R1").Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Session.TryDelegate("D0R1", "steve").IsAccepted, Is.True);
        context.AdvanceTo(630);
        context.Submit("D0I1", "bob", "E_SCHEDULE");
        context.AdvanceTo(720);
        context.Submit("D0I2", "dave", "E_DAVE_PRINTER");
        context.AdvanceTo(860);
        Assert.That(context.Session.TryDelegate("D0R2", "karen").IsAccepted, Is.False);
        Assert.That(Request(context.Session, "D0R2").Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Session.TryDelegate("D0R2", "bob").IsAccepted, Is.True);
    }

    [TestCase("steve", "E_SUPPLIES_RECEIPT")]
    [TestCase("karen", "E_KAREN_COLLECTION")]
    public void FullMonday_ResolvesBothRequestsAndPreservesBothSupportedSuppliesAlternatives(
        string suspect, string evidence)
    {
        var context = new WorkdaySessionTestContext();
        var session = context.Session;
        int decisions = 0;
        session.Delegation.DecisionResolved += decision => decisions++;
        context.AdvanceTo(600);
        var pc = session.TryDelegate("D0R1", "steve");
        Assert.That(pc.Decision.Outcome, Is.EqualTo(DelegationOutcome.BestAvailable));
        context.AdvanceTo(630);
        context.Submit("D0I1", "bob", "E_SCHEDULE");
        context.AdvanceTo(720);
        context.Submit("D0I2", "dave", "E_DAVE_PRINTER");
        context.AdvanceTo(860);
        var boxes = session.TryDelegate("D0R2", "bob");
        Assert.That(boxes.Decision.Outcome, Is.EqualTo(DelegationOutcome.BestAvailable));
        context.AdvanceTo(900);
        context.Submit("D0I3", suspect, evidence);
        context.AdvanceTo(1020);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(session.Progress.Credibility, Is.EqualTo(100));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(55));
        Assert.That(session.Trust.GetTrust("bob"), Is.EqualTo(55));
        Assert.That(session.Trust.GetTrust("karen"), Is.EqualTo(55));
        Assert.That(session.Trust.GetTrust("steve"), Is.EqualTo(suspect == "steve" ? 45 : 55));
        Assert.That(session.Workload.GetSuitableArrangedCount("steve"), Is.EqualTo(1));
        Assert.That(session.Workload.GetSuitableArrangedCount("bob"), Is.EqualTo(1));
        Assert.That(session.Workload.History.Count, Is.EqualTo(2));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(5));
        Assert.That(session.Progress.History.Count, Is.EqualTo(3));
        foreach (var review in session.Progress.History)
            Assert.That(review.Decision.Verdict, Is.EqualTo(HrVerdict.Approved));
        Assert.That(session.Progress.MissedDeadlines, Is.Empty);
        Assert.That(session.Delegation.Expiries, Is.Empty);
        Assert.That(session.GetRequestOverview().Count, Is.EqualTo(2));
        foreach (var request in session.GetRequestOverview())
            Assert.That(request.Status, Is.EqualTo(RequestStatus.Resolved));
        Assert.That(session.TryDelegate("D0R1", "steve").IsAccepted, Is.False);
        Assert.That(session.TryDelegate("D0R2", "bob").IsAccepted, Is.False);
        Assert.That(decisions, Is.EqualTo(2));
    }

    [Test]
    public void UnansweredMondayRequests_ExpireAtTheirOwnTimesBeforeCompletedSummary()
    {
        var context = ReachArchiveRequest();
        var session = context.Session;
        Assert.That(Request(session, "D0R1").Status, Is.EqualTo(RequestStatus.Expired));
        Assert.That(session.Delegation.Expiries.Count, Is.EqualTo(1));
        Assert.That(session.Delegation.Expiries[0].RecordedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 630).TotalMilliseconds + 1));
        Assert.That(Request(session, "D0R2").Status, Is.EqualTo(RequestStatus.Open));
        context.AdvanceTo(900);
        context.Submit("D0I3", "karen", "E_KAREN_COLLECTION");
        context.AdvanceTo(1020);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(session.Delegation.Expiries.Count, Is.EqualTo(2));
        Assert.That(session.Delegation.Expiries[1].RequestInstanceId, Is.EqualTo("D0R2"));
        Assert.That(session.Delegation.Expiries[1].RecordedAt.TotalMilliseconds,
            Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
        Assert.That(Request(session, "D0R2").Status, Is.EqualTo(RequestStatus.Expired));
        Assert.That(session.Progress.Credibility, Is.EqualTo(80));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(45));
        Assert.That(session.Trust.GetTrust("karen"), Is.EqualTo(45));
        Assert.That(session.Workload.History, Is.Empty);
    }

    [Test]
    public void SharedMondayInput_CreatesIndependentRuntimeStateWithoutReorderingRoster()
    {
        var first = new WorkdaySessionTestContext();
        var second = new WorkdaySessionTestContext(first.Day);
        first.AdvanceTo(600);
        second.AdvanceTo(600);
        var old = Request(first.Session, "D0R1");
        var result = first.Session.TryDelegate("D0R1", "steve");
        Assert.That(result.IsAccepted, Is.True);
        Assert.That(old.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(Request(second.Session, "D0R1").Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(Request(second.Session, "D0R1").HelperId, Is.Empty);
        Assert.That(first.Day.Requests.Count, Is.EqualTo(2));
        Assert.That(first.Day.Requests[0].InstanceId, Is.EqualTo("D0R1"));
        Assert.That(first.Day.Requests[0].Profile.RankedEmployees[0].EmployeeId, Is.EqualTo("dave"));
        Assert.That(first.Day.Requests[0].Profile.RankedEmployees[0].Tier, Is.Zero);
        var firstRoster = first.Session.GetEmployeeRoster();
        var secondRoster = second.Session.GetEmployeeRoster();
        for (int i = 0; i < first.Day.ScheduleData.EmployeeIds.Count; i++)
        {
            Assert.That(firstRoster[i].EmployeeId, Is.EqualTo(first.Day.ScheduleData.EmployeeIds[i]));
            Assert.That(secondRoster[i].EmployeeId, Is.EqualTo(firstRoster[i].EmployeeId));
        }
    }
}
