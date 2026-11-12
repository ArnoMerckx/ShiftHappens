using System.Collections.Generic;
using NUnit.Framework;

public class TaskRelayOpenQueueTests
{
    private const string Submitted = "Assignment submitted. Watch Swarms for a reply.";

    [Test]
    public void Queue_ContainsOnlyDeliveredOpenRequestsAndReturnsIndependentLists()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 3));
        var presenter = new TaskRelayPresenter(context.Session, new TaskRelayAppState());
        Assert.That(presenter.GetOpenRequests(), Is.Empty);
        Assert.That(presenter.TrySelectRequest("R1"), Is.False);
        context.Advance(595);
        var before = presenter.GetOpenRequests();
        CollectionAssert.AreEqual(new[] { "R1", "R2", "R3" }, Ids(before));
        Assert.That(context.Session.TryDelegate("R2", "dave").IsAccepted, Is.True);
        CollectionAssert.AreEqual(new[] { "R1", "R3" }, Ids(presenter.GetOpenRequests()));
        Assert.That(presenter.TrySelectRequest("R2"), Is.False);
        Assert.That(before[1].Status, Is.EqualTo(RequestStatus.Open));
        before.Clear();
        var queue = presenter.GetOpenRequests();
        queue.Reverse();
        queue.Clear();
        CollectionAssert.AreEqual(new[] { "R1", "R3" }, Ids(presenter.GetOpenRequests()));
        Assert.That(presenter.GetRequests().Count, Is.EqualTo(3));
        Assert.That(presenter.GetRequests()[1].Status, Is.EqualTo(RequestStatus.Resolved));
    }

    [TestCase("dave")]
    [TestCase("steve")]
    [TestCase("karen")]
    [TestCase("bob")]
    public void ReconciliationDuringConfirmation_RemovesChosenRequestAndKeepsOnlyNeutralFeedback(string helper)
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 3));
        context.Advance(595);
        var presenter = new TaskRelayPresenter(context.Session, new TaskRelayAppState());
        presenter.TrySelectRequest("R2");
        Assert.That(presenter.TrySelectEmployee(helper), Is.True);
        // Match the view's synchronous refresh during the existing service callback.
        context.Session.Delegation.DecisionResolved += decision => presenter.ReconcileSelection();
        var result = presenter.TryConfirm("R2", helper);
        Assert.That(result.IsAccepted, Is.True, result.Reason);
        CollectionAssert.AreEqual(new[] { "R1", "R3" }, Ids(presenter.GetOpenRequests()));
        Assert.That(presenter.GetSelectedRequest(), Is.Null);
        Assert.That(presenter.GetSelectedEmployee(), Is.Null);
        Assert.That(presenter.GetSelectedResult(), Is.Null);
        Assert.That(presenter.State.Feedback, Is.EqualTo(Submitted));
        Assert.That(presenter.State.Feedback, Is.Not.EqualTo(result.Reason));
        Assert.That(presenter.State.Requests.SelectedItemId, Is.Null);
        Assert.That(presenter.State.Employees.SelectedItemId, Is.Null);
        Assert.That(presenter.GetRequests()[1].HelperId, Is.EqualTo(helper));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(context.Session.Workload.History.Count, Is.EqualTo(1));

        var reopened = new TaskRelayPresenter(context.Session, presenter.State);
        reopened.ReconcileSelection();
        reopened.ReconcileSelection();
        CollectionAssert.AreEqual(new[] { "R1", "R3" }, Ids(reopened.GetOpenRequests()));
        Assert.That(reopened.State.Feedback, Is.EqualTo(Submitted));
        Assert.That(reopened.TryConfirm("R2", helper).IsAccepted, Is.False);
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(context.Session.Workload.History.Count, Is.EqualTo(1));

        var swarms = new SwarmsPresenter(context.Session, new AppReadingState());
        SwarmsNoticeViewData notice = swarms.GetNotices().Find(entry => entry.SourceId == "R2");
        Assert.That(notice, Is.Not.Null);
        Assert.That(notice.Kind, Is.EqualTo(SwarmsNoticeKind.HelpRequest));
        Assert.That(notice.StatusText, Does.StartWith("Submitted ·"));
        Assert.That(notice.BodyText, Does.Contain("View this request in Deleg8."));
        Assert.That(notice.BodyText, Does.Not.Contain(result.Reason));
        Assert.That(notice.BodyText, Does.Not.Contain("Credibility"));
        Assert.That(notice.BodyText, Does.Not.Contain("Trust"));
        Assert.That(swarms.GetNotices().Count, Is.EqualTo(3), "No authored reply exists before its scheduled due time.");
    }

    [Test]
    public void ClosingAnotherRequest_PreservesOpenSelectionDraftFeedbackAndScroll()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 3));
        context.Advance(595);
        var presenter = new TaskRelayPresenter(context.Session, new TaskRelayAppState());
        presenter.TrySelectRequest("R2");
        presenter.TrySelectEmployee("bob");
        presenter.State.Requests.SetListScrollPosition(0.4f);
        presenter.State.Requests.SetReaderScrollPosition(0.6f);
        presenter.State.Employees.SetListScrollPosition(0.7f);
        presenter.State.SetFeedback("Current draft feedback");
        Assert.That(context.Session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        long now = context.Session.Now.TotalMilliseconds;
        presenter.ReconcileSelection();
        presenter.ReconcileSelection();
        Assert.That(presenter.GetSelectedRequest().InstanceId, Is.EqualTo("R2"));
        Assert.That(presenter.GetSelectedEmployee().EmployeeId, Is.EqualTo("bob"));
        Assert.That(presenter.State.Feedback, Is.EqualTo("Current draft feedback"));
        Assert.That(presenter.State.Requests.ListScrollPosition, Is.EqualTo(0.4f));
        Assert.That(presenter.State.Requests.ReaderScrollPosition, Is.EqualTo(0.6f));
        Assert.That(presenter.State.Employees.ListScrollPosition, Is.EqualTo(0.7f));
        CollectionAssert.AreEqual(new[] { "R2", "R3" }, Ids(presenter.GetOpenRequests()));
        Assert.That(context.Session.Now.TotalMilliseconds, Is.EqualTo(now));
        Assert.That(context.Session.Workload.History.Count, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Expiry_LeavesQueueButKeepsHistoryAndSwarmsNoticeWithoutInventingAnAssignment(bool fixedWindow)
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(fixedWindow: fixedWindow));
        context.Advance(595);
        var presenter = new TaskRelayPresenter(context.Session, new TaskRelayAppState());
        presenter.TrySelectRequest("R1");
        presenter.TrySelectEmployee("dave");
        context.Session.Delegation.RequestExpired += entry => presenter.ReconcileSelection();
        if (fixedWindow)
        {
            context.Advance(630);
            Assert.That(presenter.GetOpenRequests().Count, Is.EqualTo(1));
            Assert.That(presenter.GetSelectedRequest().InstanceId, Is.EqualTo("R1"));
            context.Advance(630, 0, 1);
        }
        else
        {
            context.Advance(1020);
        }
        Assert.That(presenter.GetOpenRequests(), Is.Empty);
        Assert.That(presenter.GetSelectedRequest(), Is.Null);
        Assert.That(presenter.GetSelectedEmployee(), Is.Null);
        Assert.That(presenter.State.Feedback, Is.EqualTo("This request is closed. Check Swarms for updates."));
        Assert.That(presenter.TrySelectRequest("R1"), Is.False);
        Assert.That(presenter.GetRequests()[0].Status, Is.EqualTo(RequestStatus.Expired));
        Assert.That(context.Session.Workload.History, Is.Empty);
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(70));
        var notices = new SwarmsPresenter(context.Session, new AppReadingState()).GetNotices();
        SwarmsNoticeViewData requestNotice = notices.Find(
            entry => entry.Kind == SwarmsNoticeKind.HelpRequest && entry.SourceId == "R1");
        Assert.That(requestNotice, Is.Not.Null);
        Assert.That(requestNotice.StatusText, Does.StartWith("Expired ·"));

        if (fixedWindow)
        {
            Assert.That(notices.Count, Is.EqualTo(1));
            Assert.That(context.Session.SwarmsReplies.PendingCount, Is.EqualTo(1));
            Assert.That(context.Session.SwarmsReplies.DeliveredReplies, Is.Empty);
        }
        else
        {
            Assert.That(notices.Count, Is.EqualTo(2));
            Assert.That(context.Session.SwarmsReplies.PendingCount, Is.Zero);
            Assert.That(context.Session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(1));
            SwarmsReplyData reply = context.Session.SwarmsReplies.DeliveredReplies[0];
            SwarmsNoticeViewData replyNotice = notices.Find(
                entry => entry.Kind == SwarmsNoticeKind.AuthoredReply &&
                    entry.SourceId == reply.ReplyId);
            Assert.That(replyNotice, Is.Not.Null);
            Assert.That(reply.SourceKind, Is.EqualTo(ConsequenceKind.RequestExpired));
            Assert.That(reply.BodyText, Does.Contain("wasn't helped"));
        }

        Assert.That(context.Session.Workload.History, Is.Empty);
        presenter.ReconcileSelection();
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void ReopeningAfterAnUnobservedResolution_ClearsClosedDraftAndRetainsOtherReaderPositions()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 2));
        context.Advance(595);
        var state = new TaskRelayAppState();
        var first = new TaskRelayPresenter(context.Session, state);
        first.TrySelectRequest("R2");
        state.Requests.SetReaderScrollPosition(0.8f);
        first.TrySelectRequest("R1");
        first.TrySelectEmployee("dave");
        state.Requests.SetListScrollPosition(0.3f);
        state.Employees.SetListScrollPosition(0.5f);
        // No view subscribes while the app is closed.
        Assert.That(context.Session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        var reopened = new TaskRelayPresenter(context.Session, state);
        reopened.ReconcileSelection();
        Assert.That(reopened.GetSelectedRequest(), Is.Null);
        Assert.That(reopened.GetSelectedEmployee(), Is.Null);
        Assert.That(state.Feedback, Is.EqualTo(Submitted));
        CollectionAssert.AreEqual(new[] { "R2" }, Ids(reopened.GetOpenRequests()));
        Assert.That(state.Requests.ListScrollPosition, Is.EqualTo(0.3f));
        Assert.That(state.Employees.ListScrollPosition, Is.EqualTo(0.5f));
        Assert.That(reopened.TrySelectRequest("R2"), Is.True);
        Assert.That(state.Requests.ReaderScrollPosition, Is.EqualTo(0.8f));
        Assert.That(reopened.GetSelectedEmployee(), Is.Null);
        Assert.That(state.Feedback, Is.Empty);
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void InvalidCommand_LeavesOpenChoiceAndUsefulErrorWithoutRemovingRequest()
    {
        var context = new ConsequenceTestContext();
        context.Advance(595);
        var presenter = new TaskRelayPresenter(context.Session, new TaskRelayAppState());
        presenter.TrySelectRequest("R1");
        presenter.TrySelectEmployee("alice");
        var rejected = presenter.TryConfirm("R1", "alice");
        Assert.That(rejected.IsAccepted, Is.False);
        presenter.ReconcileSelection();
        Assert.That(presenter.GetOpenRequests().Count, Is.EqualTo(1));
        Assert.That(presenter.GetSelectedRequest().InstanceId, Is.EqualTo("R1"));
        Assert.That(presenter.GetSelectedEmployee().EmployeeId, Is.EqualTo("alice"));
        Assert.That(presenter.State.Feedback, Is.EqualTo(rejected.Reason));
        Assert.That(context.Session.Consequences.History, Is.Empty);
        Assert.That(context.Session.Workload.History, Is.Empty);
    }

    [Test]
    public void UnknownSavedRequest_IsClearedWithoutFabricatedSuccessOrDomainEffects()
    {
        var context = new ConsequenceTestContext();
        context.Advance(595);
        var state = new TaskRelayAppState();
        state.SelectRequest("unknown");
        state.SelectEmployee("dave");
        state.SetFeedback("Old message");
        var presenter = new TaskRelayPresenter(context.Session, state);
        presenter.ReconcileSelection();
        Assert.That(state.Requests.SelectedItemId, Is.Null);
        Assert.That(state.Employees.SelectedItemId, Is.Null);
        Assert.That(state.Feedback, Is.Empty);
        Assert.That(presenter.GetOpenRequests().Count, Is.EqualTo(1));
        Assert.That(context.Session.Consequences.History, Is.Empty);
        Assert.That(context.Session.Progress.Credibility, Is.EqualTo(80));
    }

    [Test]
    public void Firing_DoesNotRewriteFrozenOpenRequestsAsExpiredOrAllowFurtherWork()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(requestCount: 2), credibility: 5);
        context.Advance(595);
        var presenter = new TaskRelayPresenter(context.Session, new TaskRelayAppState());
        presenter.TrySelectRequest("R1");
        presenter.TrySelectEmployee("bob");
        Assert.That(presenter.TryConfirm("R1", "bob").IsAccepted, Is.True);
        presenter.ReconcileSelection();
        Assert.That(context.Session.Workday.State, Is.EqualTo(WorkdayState.Fired));
        CollectionAssert.AreEqual(new[] { "R2" }, Ids(presenter.GetOpenRequests()));
        Assert.That(presenter.TrySelectRequest("R2"), Is.True);
        Assert.That(presenter.TrySelectEmployee("dave"), Is.True);
        Assert.That(presenter.TryConfirm("R2", "dave").IsAccepted, Is.False);
        presenter.ReconcileSelection();
        Assert.That(context.Session.GetRequestOverview()[1].Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(context.Session.Workload.History.Count, Is.EqualTo(1));
        Assert.That(context.Session.Progress.Credibility, Is.Zero);
    }

    private static List<string> Ids(List<RequestOverviewViewData> requests)
    {
        return requests.ConvertAll(request => request.InstanceId);
    }
}
