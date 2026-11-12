using System;
using System.Collections.Generic;
using NUnit.Framework;

public class SwarmsReplyIntegrationTests
{
    [Test]
    public void FixedWindowResolution_DeliversOnceAtWindowEndWithTypedNoticeAndNotification()
    {
        var context = new ConsequenceTestContext();
        WorkdaySession session = context.Session;
        context.Advance(595);

        DelegationSubmissionResult result = session.TryDelegate("R1", "dave");
        Assert.That(result.IsAccepted, Is.True, result.Reason);
        Assert.That(result.Decision.Outcome, Is.EqualTo(DelegationOutcome.BestAvailable));
        Assert.That(session.SwarmsReplies.PendingCount, Is.EqualTo(2));
        Assert.That(session.SwarmsReplies.DeliveredReplies, Is.Empty);
        Assert.That(session.Notifications.PendingCount, Is.Zero);

        int credibility = session.Progress.Credibility;
        int aliceTrust = session.Trust.GetTrust("alice");
        int daveTrust = session.Trust.GetTrust("dave");
        ConsequenceTestContext.ReplayDecision(session.Consequences, result.Decision);
        Assert.That(session.SwarmsReplies.PendingCount, Is.EqualTo(2));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));

        context.Advance(644, 59, 999);
        Assert.That(session.SwarmsReplies.DeliveredReplies, Is.Empty);
        context.Advance(645);

        Assert.That(session.SwarmsReplies.PendingCount, Is.Zero);
        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(2));
        SwarmsReplyData reply = FindReply(session, "requester-help-arranged-v1", "R1");
        SwarmsReplyData reaction = FindReply(session, "coworker-assignment-thanks-v1", "R1");
        Assert.That(reply.SourceKind, Is.EqualTo(ConsequenceKind.RequestResolved));
        Assert.That(reply.SourceInstanceId, Is.EqualTo("R1"));
        Assert.That(reply.RuleId, Is.EqualTo("requester-help-arranged-v1"));
        Assert.That(reply.SenderEmployeeId, Is.EqualTo("alice"));
        Assert.That(reply.Subject, Is.EqualTo("PC help update"));
        Assert.That(reply.BodyText, Is.EqualTo("dave was arranged to help me with PC help."));
        Assert.That(reply.DueAt.TotalMilliseconds, Is.EqualTo(new GameTime(3, 645).TotalMilliseconds));
        Assert.That(reaction.SenderEmployeeId, Is.EqualTo("dave"));
        Assert.That(reaction.BodyText, Is.EqualTo(
            "Thanks for arranging for me to help alice with PC help."));
        Assert.That(reaction.DueAt.TotalMilliseconds, Is.EqualTo(reply.DueAt.TotalMilliseconds));
        Assert.That(reaction.ReplyId, Is.Not.EqualTo(reply.ReplyId));
        Assert.That(session.Progress.Credibility, Is.EqualTo(credibility));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(aliceTrust));
        Assert.That(session.Trust.GetTrust("dave"), Is.EqualTo(daveTrust));
        Assert.That(session.Workload.History.Count, Is.EqualTo(1));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));

        Assert.That(session.Notifications.PendingCount, Is.EqualTo(2));
        Assert.That(session.Notifications.TryTakeNext(out ShiftOSNotification notification), Is.True);
        Assert.That(notification.Kind, Is.EqualTo(ShiftOSNotificationKind.SwarmsReply));
        Assert.That(notification.Title, Is.EqualTo("Swarms — alice"));
        Assert.That(notification.Body, Is.EqualTo("New reply: PC help update"));
        Assert.That(session.Notifications.TryTakeNext(out ShiftOSNotification coworkerNotification), Is.True);
        Assert.That(coworkerNotification.Kind, Is.EqualTo(ShiftOSNotificationKind.SwarmsReply));
        Assert.That(coworkerNotification.Title, Is.EqualTo("Swarms — dave"));
        Assert.That(coworkerNotification.Body, Is.EqualTo("New reply: About PC help"));

        var state = new AppReadingState();
        var presenter = new SwarmsPresenter(session, state);
        SwarmsNoticeViewData notice = Find(presenter, SwarmsNoticeKind.AuthoredReply, reply.ReplyId);
        Assert.That(notice.DisplayName, Is.EqualTo(reply.Subject));
        Assert.That(notice.StatusText, Is.EqualTo("Reply"));
        Assert.That(notice.BodyText, Is.EqualTo("From: alice\n\ndave was arranged to help me with PC help."));
        SwarmsNoticeViewData reactionNotice = Find(
            presenter, SwarmsNoticeKind.AuthoredReply, reaction.ReplyId);
        Assert.That(reactionNotice.BodyText, Is.EqualTo(
            "From: dave\n\nThanks for arranging for me to help alice with PC help."));
        Assert.That(presenter.TrySelectNotice(notice.NoticeId), Is.True);
        state.SetReaderScrollPosition(0.7f);
        var reopened = new SwarmsPresenter(session, state);
        Assert.That(reopened.GetSelectedNotice().NoticeId, Is.EqualTo(notice.NoticeId));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.7f));

        context.Advance(700);
        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(2));
        Assert.That(session.Notifications.PendingCount, Is.Zero);
    }

    [Test]
    public void NonWindowResolution_UsesConfiguredDelayAndSurvivesLargeTimeJump()
    {
        var context = new ConsequenceTestContext(
            ConsequenceTestContext.CreateDay(fixedWindow: false),
            replyTimingRules: new ReplyTimingRules(7));
        WorkdaySession session = context.Session;
        context.Advance(595);
        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        Assert.That(session.SwarmsReplies.PendingCount, Is.EqualTo(2));

        context.Advance(601, 59, 999);
        Assert.That(session.SwarmsReplies.DeliveredReplies, Is.Empty);
        context.Advance(800);

        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(2));
        foreach (SwarmsReplyData reply in session.SwarmsReplies.DeliveredReplies)
            Assert.That(reply.DueAt.TotalMilliseconds,
                Is.EqualTo(new GameTime(3, 602).TotalMilliseconds));
        Assert.That(session.SwarmsReplies.PendingCount, Is.Zero);
        Assert.That(session.Notifications.PendingCount, Is.EqualTo(2));
        context.Advance(900);
        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(2));
        Assert.That(session.Notifications.PendingCount, Is.EqualTo(2));
    }

    [TestCase("bob", DelegationOutcome.Unavailable)]
    [TestCase("karen", DelegationOutcome.Unsuitable)]
    public void FailedAssignment_UsesNeutralNarrativeWithoutHiddenReasonOrStats(
        string helperId, DelegationOutcome expectedOutcome)
    {
        var context = new ConsequenceTestContext();
        WorkdaySession session = context.Session;
        context.Advance(595);
        DelegationSubmissionResult result = session.TryDelegate("R1", helperId);
        Assert.That(result.IsAccepted, Is.True, result.Reason);
        Assert.That(result.Decision.Outcome, Is.EqualTo(expectedOutcome));
        context.Advance(645);

        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(2));
        SwarmsReplyData reply = FindReply(session, "requester-not-helped-v1", "R1");
        SwarmsReplyData reaction = FindReply(session, "coworker-assignment-protest-v1", "R1");
        Assert.That(reply.RuleId, Is.EqualTo("requester-not-helped-v1"));
        Assert.That(reply.BodyText,
            Is.EqualTo("I wasn't helped with PC help after the assignment to " + helperId + "."));
        Assert.That(reply.BodyText, Does.Not.Contain(result.Reason));
        string lowerBody = reply.BodyText.ToLowerInvariant();
        Assert.That(lowerBody, Does.Not.Contain("credibility"));
        Assert.That(lowerBody, Does.Not.Contain("trust"));
        Assert.That(lowerBody, Does.Not.Contain("unavailable"));
        Assert.That(lowerBody, Does.Not.Contain("unsuitable"));
        Assert.That(reaction.SenderEmployeeId, Is.EqualTo(helperId));
        Assert.That(reaction.BodyText,
            Is.EqualTo("I couldn't help alice with PC help this time."));
        Assert.That(reaction.BodyText.ToLowerInvariant(), Does.Not.Contain("trust"));
        Assert.That(reaction.BodyText, Does.Not.Contain(result.Reason));
    }

    [Test]
    public void FixedWindowExpiry_WaitsForWindowEndAndDoesNotInventAssignment()
    {
        var context = new ConsequenceTestContext();
        WorkdaySession session = context.Session;
        context.Advance(595);
        context.Advance(630);
        Assert.That(session.GetRequestOverview()[0].Status, Is.EqualTo(RequestStatus.Open));
        context.Advance(630, 0, 1);

        Assert.That(session.GetRequestOverview()[0].Status, Is.EqualTo(RequestStatus.Expired));
        Assert.That(session.SwarmsReplies.PendingCount, Is.EqualTo(1));
        Assert.That(session.SwarmsReplies.DeliveredReplies, Is.Empty);
        Assert.That(session.Workload.History, Is.Empty);
        int credibility = session.Progress.Credibility;
        int trust = session.Trust.GetTrust("alice");

        context.Advance(645);
        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(1));
        SwarmsReplyData reply = session.SwarmsReplies.DeliveredReplies[0];
        Assert.That(reply.SourceKind, Is.EqualTo(ConsequenceKind.RequestExpired));
        Assert.That(reply.RuleId, Is.EqualTo("requester-expired-v1"));
        Assert.That(reply.BodyText, Is.EqualTo("I wasn't helped with PC help before the request closed."));
        Assert.That(session.Progress.Credibility, Is.EqualTo(credibility));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(trust));
        Assert.That(session.Workload.History, Is.Empty);
    }

    [Test]
    public void ClosingExpiry_DeliversBeforeCompletionAndLeavesNothingPending()
    {
        var context = new ConsequenceTestContext(
            ConsequenceTestContext.CreateDay(fixedWindow: false));
        WorkdaySession session = context.Session;
        context.Advance(595);
        context.Advance(1020);

        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(session.SwarmsReplies.PendingCount, Is.Zero);
        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(1));
        Assert.That(session.SwarmsReplies.DeliveredReplies[0].DueAt.TotalMilliseconds,
            Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
        Assert.That(session.SwarmsReplies.DeliveredReplies[0].SourceKind,
            Is.EqualTo(ConsequenceKind.RequestExpired));
        Assert.That(session.Notifications.PendingCount, Is.EqualTo(1));
        Assert.That(new SwarmsPresenter(session, new AppReadingState()).GetNotices().Count,
            Is.EqualTo(2));
    }

    [Test]
    public void Firing_CancelsEarlierPendingRepliesAndSuppressesTriggeringReply()
    {
        var context = new ConsequenceTestContext(
            ConsequenceTestContext.CreateDay(requestCount: 3), credibility: 10);
        WorkdaySession session = context.Session;
        context.Advance(595);

        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        Assert.That(session.SwarmsReplies.PendingCount, Is.EqualTo(2));
        Assert.That(session.TryDelegate("R2", "bob").IsAccepted, Is.True);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Working));
        Assert.That(session.SwarmsReplies.PendingCount, Is.EqualTo(4));
        Assert.That(session.TryDelegate("R3", "bob").IsAccepted, Is.True);

        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Fired));
        Assert.That(session.Progress.Credibility, Is.Zero);
        Assert.That(session.SwarmsReplies.PendingCount, Is.Zero);
        Assert.That(session.SwarmsReplies.DeliveredReplies, Is.Empty);
        Assert.That(session.Notifications.PendingCount, Is.Zero);
        Assert.That(context.Scheduler.PendingCount, Is.Zero);
        session.AdvanceRealSeconds(10000);
        Assert.That(session.SwarmsReplies.DeliveredReplies, Is.Empty);
    }

    [Test]
    public void ThrowingDeliveryObserver_CannotRollbackOrDuplicateDeliveredReply()
    {
        var context = new ConsequenceTestContext();
        WorkdaySession session = context.Session;
        context.Advance(595);
        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        var failure = new InvalidOperationException("Intentional reply observer failure.");
        bool reentrantAccepted = true;
        Action<SwarmsReplyData> observer = reply =>
        {
            reentrantAccepted = session.SwarmsReplies.TryQueue(reply);
            throw failure;
        };
        session.SwarmsReplies.ReplyDelivered += observer;

        InvalidOperationException caught = Assert.Throws<InvalidOperationException>(
            () => context.Advance(645));
        Assert.That(caught, Is.SameAs(failure));
        Assert.That(reentrantAccepted, Is.False);
        Assert.That(session.SwarmsReplies.PendingCount, Is.EqualTo(1));
        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(1));
        Assert.That(session.Notifications.PendingCount, Is.EqualTo(1));

        session.SwarmsReplies.ReplyDelivered -= observer;
        context.Advance(646);
        Assert.That(session.SwarmsReplies.PendingCount, Is.Zero);
        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(2));
        Assert.That(session.Notifications.PendingCount, Is.EqualTo(2));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Workload.History.Count, Is.EqualTo(1));
    }

    private static SwarmsNoticeViewData Find(
        SwarmsPresenter presenter, SwarmsNoticeKind kind, string sourceId)
    {
        foreach (SwarmsNoticeViewData notice in presenter.GetNotices())
        {
            if (notice.Kind == kind &&
                string.Equals(notice.SourceId, sourceId, StringComparison.Ordinal))
            {
                return notice;
            }
        }

        Assert.Fail("Missing " + kind + " notice for " + sourceId + ".");
        return null;
    }

    private static SwarmsReplyData FindReply(
        WorkdaySession session, string ruleId, string sourceInstanceId)
    {
        foreach (SwarmsReplyData reply in session.SwarmsReplies.DeliveredReplies)
        {
            if (string.Equals(reply.RuleId, ruleId, StringComparison.Ordinal) &&
                string.Equals(reply.SourceInstanceId, sourceInstanceId, StringComparison.Ordinal))
            {
                return reply;
            }
        }

        Assert.Fail("Missing reply rule " + ruleId + " for " + sourceInstanceId + ".");
        return null;
    }
}
