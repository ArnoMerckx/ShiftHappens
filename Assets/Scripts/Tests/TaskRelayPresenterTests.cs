using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public class TaskRelayPresenterTests
{
    [Test]
    public void MissingDependencies_AreRejectedAndNoRequestIsSelectedAutomatically()
    {
        var session = new ConsequenceTestContext().Session;
        Assert.Throws<ArgumentNullException>(() => new TaskRelayPresenter(null, new TaskRelayAppState()));
        Assert.Throws<ArgumentNullException>(() => new TaskRelayPresenter(session, null));
        var presenter = new TaskRelayPresenter(session, new TaskRelayAppState());
        Assert.That(presenter.GetRequests(), Is.Empty);
        Assert.That(presenter.TrySelectRequest("R1"), Is.False);
        Assert.That(presenter.TrySelectEmployee("dave"), Is.False);
        Assert.That(presenter.GetSelectedRequest(), Is.Null);
        Assert.That(presenter.GetSelectedEmployee(), Is.Null);
        Assert.That(presenter.GetSelectedResult(), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("UNKNOWN")]
    [TestCase("r1")]
    public void InvalidRequestSelection_PreservesDraft(string invalid)
    {
        var context = new ConsequenceTestContext();
        context.Advance(595);
        var presenter = Selected(context, "dave");
        presenter.State.SetFeedback("Keep feedback");
        Assert.That(presenter.TrySelectRequest(invalid), Is.False);
        Assert.That(presenter.State.Requests.SelectedItemId, Is.EqualTo("R1"));
        Assert.That(presenter.State.Employees.SelectedItemId, Is.EqualTo("dave"));
        Assert.That(presenter.State.Feedback, Is.EqualTo("Keep feedback"));
    }

    [Test]
    public void Browsing_IsNeutralCopiedAndCannotChangeDomainState()
    {
        var context = new ConsequenceTestContext();
        context.Advance(595);
        var session = context.Session;
        var presenter = new TaskRelayPresenter(session, new TaskRelayAppState());
        CollectionAssert.AreEqual(new[] { "alice", "bob", "dave", "steve", "karen" },
            presenter.GetEmployees().ConvertAll(employee => employee.EmployeeId));
        var employees = presenter.GetEmployees();
        var requests = presenter.GetRequests();
        employees.Clear();
        requests.Clear();
        Assert.That(presenter.GetEmployees().Count, Is.EqualTo(5));
        Assert.That(presenter.GetRequests().Count, Is.EqualTo(1));
        Assert.That(presenter.TrySelectRequest("R1"), Is.True);
        long now = session.Now.TotalMilliseconds;
        foreach (var employee in presenter.GetEmployees())
            Assert.That(presenter.TrySelectEmployee(employee.EmployeeId), Is.True);
        Assert.That(presenter.TrySelectEmployee("DAVE"), Is.False);
        Assert.That(presenter.TrySelectEmployee("unknown"), Is.False);
        Assert.That(presenter.GetSelectedEmployee().EmployeeId, Is.EqualTo("karen"));
        Assert.That(presenter.GetSelectedResult(), Is.Null);
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(now));
        Assert.That(session.Progress.Credibility, Is.EqualTo(80));
        Assert.That(session.Consequences.History, Is.Empty);
        Assert.That(session.Workload.History, Is.Empty);
        Assert.That(session.Notifications.PendingCount, Is.Zero);
        Assert.That(session.GetRequestOverview()[0].Status, Is.EqualTo(RequestStatus.Open));
        foreach (string id in session.Trust.EmployeeIds)
            Assert.That(session.Trust.GetTrust(id), Is.EqualTo(50));
    }

    [TestCase("dave", DelegationOutcome.BestAvailable, 5, true)]
    [TestCase("steve", DelegationOutcome.CapableAlternative, 2, true)]
    [TestCase("karen", DelegationOutcome.Unsuitable, -5, false)]
    [TestCase("bob", DelegationOutcome.Unavailable, -10, false)]
    public void ExplicitConfirm_AcceptsAllValidChoicesAndShowsActualRecordedResult(
        string helper, DelegationOutcome outcome, int delta, bool suitable)
    {
        var context = new ConsequenceTestContext();
        context.Advance(595, 12, 345);
        var presenter = Selected(context, helper);
        Assert.That(context.Session.Consequences.History, Is.Empty);
        var result = presenter.TryConfirm("R1", helper);
        Assert.That(result.IsAccepted, Is.True, result.Reason);
        Assert.That(result.Decision.Outcome, Is.EqualTo(outcome));
        Assert.That(presenter.State.Feedback,
            Is.EqualTo("Assignment submitted. Watch Swarms for a reply."));
        Assert.That(presenter.State.Requests.SelectedItemId, Is.EqualTo("R1"));
        Assert.That(presenter.State.Employees.SelectedItemId, Is.EqualTo(helper));
        var view = presenter.GetSelectedResult();
        Assert.That(view.RequestInstanceId, Is.EqualTo("R1"));
        Assert.That(view.Status, Is.EqualTo(RequestStatus.Resolved));
        Assert.That(view.HelperId, Is.EqualTo(helper));
        Assert.That(view.HelperName, Is.EqualTo(helper));
        Assert.That(view.HasAssignment, Is.True);
        Assert.That(view.IsSuitableArranged, Is.EqualTo(suitable));
        Assert.That(view.Explanation, Is.EqualTo(result.Decision.Explanation));
        Assert.That(view.Explanation, Does.Not.Contain("Trust"));
        Assert.That(view.Explanation, Does.Not.Contain("completed"));
        Assert.That(view.CredibilityBefore, Is.EqualTo(80));
        Assert.That(view.CredibilityAfter, Is.EqualTo(80 + delta));
        Assert.That(view.CredibilityDelta, Is.EqualTo(delta));
        Assert.That(view.RecordedAt.TotalMilliseconds, Is.EqualTo(context.Session.Now.TotalMilliseconds));
        Assert.That(presenter.TrySelectEmployee("dave"), Is.False);
        Assert.That(context.Session.Workload.History.Count, Is.EqualTo(1));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
    }

    [TestCase(98, "dave", 100)]
    [TestCase(100, "dave", 100)]
    [TestCase(5, "bob", 0)]
    public void Result_UsesActualClampedHistoryAndRemainsReadableAfterFiring(
        int opening, string helper, int after)
    {
        var context = new ConsequenceTestContext(credibility: opening);
        context.Advance(595);
        var presenter = Selected(context, helper);
        Assert.That(presenter.TryConfirm("R1", helper).IsAccepted, Is.True);
        var view = presenter.GetSelectedResult();
        Assert.That(view.CredibilityBefore, Is.EqualTo(opening));
        Assert.That(view.CredibilityAfter, Is.EqualTo(after));
        Assert.That(view.CredibilityDelta, Is.EqualTo(after - opening));
        if (after == 0)
            Assert.That(context.Session.Workday.State, Is.EqualTo(WorkdayState.Fired));
        Assert.That(presenter.TryConfirm("R1", helper).IsAccepted, Is.False);
        Assert.That(presenter.GetSelectedResult().CredibilityAfter, Is.EqualTo(after));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
    }

    [TestCase(0, "Select a delivered request.")]
    [TestCase(1, "Select an employee.")]
    [TestCase(2, "The selection changed.")]
    [TestCase(3, "The selection changed.")]
    public void MissingOrStaleDisplayedPair_RejectsWithoutDomainEffects(int scenario, string message)
    {
        var context = new ConsequenceTestContext();
        context.Advance(595);
        var presenter = new TaskRelayPresenter(context.Session, new TaskRelayAppState());
        if (scenario >= 1) presenter.TrySelectRequest("R1");
        if (scenario >= 2) presenter.TrySelectEmployee("dave");
        var result = presenter.TryConfirm(scenario == 2 ? "old_request" : "R1",
            scenario == 3 ? "steve" : "dave");
        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Reason, Does.StartWith(message));
        Assert.That(presenter.State.Feedback, Is.EqualTo(result.Reason));
        Assert.That(context.Session.GetRequestOverview()[0].Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Session.Consequences.History, Is.Empty);
        Assert.That(context.Session.Workload.History, Is.Empty);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(80));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SelfHelpPolicy_IsDecidedByDomainAtConfirmation(bool allowed)
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(selfHelp: allowed));
        context.Advance(595);
        var presenter = Selected(context, "alice");
        var result = presenter.TryConfirm("R1", "alice");
        Assert.That(result.IsAccepted, Is.EqualTo(allowed));
        if (allowed)
        {
            Assert.That(context.Session.Trust.GetTrust("alice"), Is.EqualTo(55));
            Assert.That(context.Session.Consequences.History[0].TrustChanges.Count, Is.EqualTo(1));
            Assert.That(presenter.GetSelectedResult().HelperId, Is.EqualTo("alice"));
        }
        else
        {
            Assert.That(result.Reason, Does.Contain("self-help"));
            Assert.That(context.Session.Trust.GetTrust("alice"), Is.EqualTo(50));
            Assert.That(context.Session.Consequences.History, Is.Empty);
        }
    }

    [Test]
    public void ReopenAndDuplicateConfirm_DoNotReplayAndHistoryIgnoresLaterDraftChoiceOrScore()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 2));
        context.Advance(595);
        var presenter = Selected(context, "dave");
        presenter.State.Requests.SetReaderScrollPosition(0.25f);
        presenter.State.Employees.SetListScrollPosition(0.75f);
        Assert.That(presenter.TryConfirm("R1", "dave").IsAccepted, Is.True);
        var original = presenter.GetSelectedResult();
        var reopened = new TaskRelayPresenter(context.Session, presenter.State);
        Assert.That(reopened.State.Requests.ReaderScrollPosition, Is.EqualTo(0.25f));
        Assert.That(reopened.State.Employees.ListScrollPosition, Is.EqualTo(0.75f));
        Assert.That(reopened.TryConfirm("R1", "dave").IsAccepted, Is.False);
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(reopened.TrySelectRequest("R2"), Is.True);
        Assert.That(reopened.GetSelectedEmployee(), Is.Null);
        Assert.That(reopened.TrySelectEmployee("bob"), Is.True);
        Assert.That(reopened.TryConfirm("R2", "bob").IsAccepted, Is.True);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(75));
        Assert.That(reopened.TrySelectRequest("R1"), Is.False);
        // Internal archive projection remains readable; closed requests are not queue choices.
        reopened.State.Requests.SelectItem("R1");
        // Simulate a retained draft helper different from the recorded choice.
        reopened.State.Employees.SelectItem("karen");
        var historical = reopened.GetSelectedResult();
        Assert.That(historical.HelperId, Is.EqualTo("dave"));
        Assert.That(historical.CredibilityBefore, Is.EqualTo(80));
        Assert.That(historical.CredibilityAfter, Is.EqualTo(85));
        Assert.That(original.CredibilityAfter, Is.EqualTo(85));
        Assert.That(context.Session.Workload.History.Count, Is.EqualTo(2));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(2));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Expiry_RejectsStaleConfirmAndRecordsNoHelperOrAssignment(bool fixedWindow)
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(fixedWindow: fixedWindow));
        context.Advance(595);
        var presenter = Selected(context, "dave");
        if (fixedWindow) context.Advance(630, 0, 1);
        else context.Advance(1020);
        Assert.That(presenter.TryConfirm("R1", "dave").IsAccepted, Is.False);
        Assert.That(presenter.TrySelectEmployee("bob"), Is.False);
        var view = presenter.GetSelectedResult();
        Assert.That(view.Status, Is.EqualTo(RequestStatus.Expired));
        Assert.That(view.HasAssignment, Is.False);
        Assert.That(view.IsSuitableArranged, Is.False);
        Assert.That(view.HelperId, Is.Empty);
        Assert.That(view.HelperName, Is.Empty);
        Assert.That(view.CredibilityBefore, Is.EqualTo(80));
        Assert.That(view.CredibilityAfter, Is.EqualTo(70));
        Assert.That(view.Explanation, Does.Contain("No employee was assigned"));
        Assert.That(context.Session.Workload.History, Is.Empty);
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(presenter.TrySelectRequest("R1"), Is.False);
        Assert.That(new TaskRelayPresenter(context.Session, presenter.State)
            .GetSelectedResult().HelperId, Is.Empty);
    }

    [Test]
    public void ConfirmationAtExactCutoff_IsAcceptedAndBusyCallbackCannotSubmitAnotherRequest()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 2));
        context.Advance(595);
        var presenter = Selected(context, "dave");
        DelegationSubmissionResult nested = null;
        TaskRelayResultViewData duringCommit = null;
        context.Session.Consequences.ConsequenceCommitted += entry =>
        {
            duringCommit = presenter.GetSelectedResult();
            presenter.TrySelectRequest("R2");
            presenter.TrySelectEmployee("steve");
            nested = presenter.TryConfirm("R2", "steve");
        };
        context.Advance(630);
        var result = presenter.TryConfirm("R1", "dave");
        Assert.That(result.IsAccepted, Is.True);
        Assert.That(duringCommit.HelperId, Is.EqualTo("dave"));
        Assert.That(duringCommit.CredibilityAfter, Is.EqualTo(85));
        Assert.That(nested, Is.Not.Null);
        Assert.That(nested.IsAccepted, Is.False);
        Assert.That(presenter.State.Requests.SelectedItemId, Is.EqualTo("R2"));
        Assert.That(presenter.State.Feedback, Is.EqualTo(nested.Reason),
            "The outer result must not overwrite feedback for the newly selected pair.");
        Assert.That(context.Session.GetRequestOverview()[1].Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void PriorDaySameRequestId_DoesNotSupplyCurrentAssignment()
    {
        DayData day = ConsequenceTestContext.CreateDay();
        var trusts = new List<EmployeeTrustEntry>();
        foreach (string id in day.ScheduleData.EmployeeIds) trusts.Add(new EmployeeTrustEntry(id, 50));
        var prior = new AssignmentHistoryEntry("yesterday",
            new DelegationDecision("R1", "karen", new GameTime(2, 600),
                DelegationOutcome.Unsuitable, "Prior day only."));
        var opening = new WorkdayOpeningState(80, trusts, new List<AssignmentHistoryEntry> { prior });
        var context = new ConsequenceTestContext(day, opening: opening);
        context.Advance(595);
        var presenter = Selected(context, "dave");
        Assert.That(presenter.TryConfirm("R1", "dave").IsAccepted, Is.True);
        Assert.That(presenter.GetSelectedResult().HelperId, Is.EqualTo("dave"));
        Assert.That(presenter.GetSelectedResult().Explanation, Is.Not.EqualTo("Prior day only."));
    }

    [Test]
    public void SameIdHrConsequence_DoesNotSupplyRequestResultAndPublicNameComesFromRoster()
    {
        DayData monday = new WorkdaySessionTestContext().Day;
        var source = monday.Requests[0];
        var request = new DayRequestPlan("D0I1", source.Request, source.Profile,
            source.DeliveryAt, source.HelpWindow, source.CutoffAt);
        var day = new DayData(monday.ScheduleData, new List<EvidenceDefinition>(monday.EvidenceDatabase.All),
            new List<DayIncidentPlan> { monday.Incidents[0] }, new List<DayRequestPlan> { request });
        var context = new WorkdaySessionTestContext(day);
        context.AdvanceTo(595);
        context.Submit("D0I1", "bob", "E_SCHEDULE");
        Assert.That(context.Session.Consequences.History[0].Kind, Is.EqualTo(ConsequenceKind.ReportAccepted));
        var presenter = new TaskRelayPresenter(context.Session, new TaskRelayAppState());
        presenter.TrySelectRequest("D0I1");
        presenter.TrySelectEmployee("steve");
        Assert.That(presenter.GetSelectedResult(), Is.Null);
        Assert.That(presenter.TryConfirm("D0I1", "steve").IsAccepted, Is.True);
        var view = presenter.GetSelectedResult();
        Assert.That(view.HelperName, Is.EqualTo("Steve"));
        Assert.That(view.CredibilityBefore, Is.EqualTo(100));
        Assert.That(view.CredibilityAfter, Is.EqualTo(100));
        Assert.That(view.Explanation, Does.Contain("Help is arranged"));
        context.AdvanceTo(605);
        Assert.That(context.Session.Deliveries.GetDelivered("D0I1").Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(presenter.GetSelectedResult().Explanation, Is.EqualTo(view.Explanation));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(2));
    }

    [Test]
    public void ResultContract_ExposesOnlyImmutablePermittedValuesWithoutDomainHistoryObjects()
    {
        var allowed = new HashSet<Type> { typeof(string), typeof(bool), typeof(int),
            typeof(GameTime), typeof(RequestStatus) };
        foreach (var property in typeof(TaskRelayResultViewData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.That(allowed.Contains(property.PropertyType), Is.True, property.Name);
            Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
            Assert.That(property.Name.IndexOf("Trust", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1));
        }
        Assert.That(typeof(TaskRelayResultViewData).GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
    }

    [Test]
    public void ResultConstructor_RejectsMismatchedOrMissingRecords()
    {
        var context = new ConsequenceTestContext();
        context.Advance(595);
        var presenter = Selected(context, "dave");
        var open = presenter.GetSelectedRequest();
        presenter.TryConfirm("R1", "dave");
        var request = presenter.GetSelectedRequest();
        var assignment = context.Session.Workload.History[0];
        var consequence = context.Session.Consequences.History[0];
        Assert.Throws<ArgumentNullException>(() => new TaskRelayResultViewData(null, assignment, consequence, "Dave"));
        Assert.Throws<ArgumentNullException>(() => new TaskRelayResultViewData(request, assignment, null, "Dave"));
        Assert.Throws<ArgumentException>(() => new TaskRelayResultViewData(open, assignment, consequence, "Dave"));
        Assert.Throws<ArgumentException>(() => new TaskRelayResultViewData(request, null, consequence, "Dave"));
        Assert.Throws<ArgumentException>(() => new TaskRelayResultViewData(request, assignment, consequence, " "));
        var wrongKind = new ConsequenceEntry(consequence.DayId, "R1", ConsequenceKind.ReportAccepted,
            consequence.RecordedAt, 80, 80, new List<EmployeeTrustChange>());
        Assert.Throws<ArgumentException>(() => new TaskRelayResultViewData(request, assignment, wrongKind, "Dave"));
        var wrongRequest = new ConsequenceEntry(consequence.DayId, "OTHER", consequence.Kind,
            consequence.RecordedAt, 80, 85, new List<EmployeeTrustChange>());
        Assert.Throws<ArgumentException>(() => new TaskRelayResultViewData(request, assignment, wrongRequest, "Dave"));
        var wrongDay = new AssignmentHistoryEntry("other_day", assignment.Decision);
        Assert.Throws<ArgumentException>(() => new TaskRelayResultViewData(request, wrongDay, consequence, "Dave"));
        var wrongHelper = new AssignmentHistoryEntry(assignment.DayId,
            new DelegationDecision("R1", "bob", assignment.SubmittedAt,
                DelegationOutcome.Unavailable, "Other helper."));
        Assert.Throws<ArgumentException>(() => new TaskRelayResultViewData(request, wrongHelper, consequence, "Bob"));
    }

    private static TaskRelayPresenter Selected(ConsequenceTestContext context, string helper)
    {
        var presenter = new TaskRelayPresenter(context.Session, new TaskRelayAppState());
        Assert.That(presenter.TrySelectRequest("R1"), Is.True);
        Assert.That(presenter.TrySelectEmployee(helper), Is.True);
        return presenter;
    }
}
