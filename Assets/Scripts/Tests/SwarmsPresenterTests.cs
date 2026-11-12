using System;
using System.Collections.Generic;
using NUnit.Framework;

public class SwarmsPresenterTests
{
    [Test]
    public void NoticeKinds_KeepSameRawIdsAndColonContainingIdsDistinct()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string source in new[] { "SAME", "1:SAME", "0:1:SAME" })
        {
            foreach (SwarmsNoticeKind kind in Enum.GetValues(typeof(SwarmsNoticeKind)))
            {
                var notice = new SwarmsNoticeViewData(kind, source, "Title", "Body", "Status");
                Assert.That(ids.Add(notice.NoticeId), Is.True);
                Assert.That(notice.SourceId, Is.EqualTo(source));
                Assert.That(new SwarmsNoticeViewData(kind, source, "Other title", "Other body", "Other status")
                    .NoticeId, Is.EqualTo(notice.NoticeId));
            }
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void NoticeSnapshot_RejectsMissingIdentityOrDisplayContent(int missing)
    {
        Assert.Throws<ArgumentException>(() => new SwarmsNoticeViewData(
            SwarmsNoticeKind.HelpRequest, missing == 0 ? " " : "R1",
            missing == 1 ? null : "Title", missing == 2 ? "" : "Body",
            missing == 3 ? " " : "Open"));
    }

    [Test]
    public void MissingPresenterDependenciesAndUnknownNoticeKind_AreRejected()
    {
        var session = new ConsequenceTestContext().Session;
        Assert.Throws<ArgumentNullException>(() => new SwarmsPresenter(null, new AppReadingState()));
        Assert.Throws<ArgumentNullException>(() => new SwarmsPresenter(session, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SwarmsNoticeViewData(
            (SwarmsNoticeKind)99, "R1", "Title", "Body", "Open"));
    }

    [Test]
    public void BeforeDelivery_NoNoticesOrAutomaticSelection()
    {
        var presenter = new SwarmsPresenter(new WorkdaySessionTestContext().Session, new AppReadingState());
        Assert.That(presenter.GetNotices(), Is.Empty);
        Assert.That(presenter.GetSelectedNotice(), Is.Null);
        Assert.That(presenter.State.SelectedItemId, Is.Null);
    }

    [TestCase("D0I1", 590, "10:30", "09:35–09:40")]
    [TestCase("D0I2", 675, "12:00", "10:35–10:40")]
    [TestCase("D0I3", 840, "15:00", "13:35–13:40")]
    public void MondayHrNotices_PreserveExactLegacyReaderAndRowText(
        string sourceId, int delivery, string due, string occurrence)
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(delivery);
        IncidentInstance incident = context.Session.Deliveries.GetDelivered(sourceId);
        var presenter = new SwarmsPresenter(context.Session, new AppReadingState());
        SwarmsNoticeViewData notice = Find(presenter, SwarmsNoticeKind.HrIncident, sourceId);
        Assert.That(notice.DisplayName, Is.EqualTo(incident.DisplayName));
        Assert.That(notice.StatusText, Is.EqualTo("Due " + due));
        Assert.That(notice.BodyText, Is.EqualTo(
            $"Report due: {due}\n\nOccurred: {occurrence}\n\n{incident.Description}"));
    }

    [Test]
    public void SameHrAndRequestSourceId_HaveIndependentSelectionAndRetainedScroll()
    {
        var context = CreateCollisionContext();
        context.AdvanceTo(595);
        var apps = new ShiftOSAppState();
        var presenter = new SwarmsPresenter(context.Session, apps.Swarms);
        var hr = Find(presenter, SwarmsNoticeKind.HrIncident, "D0I1");
        var help = Find(presenter, SwarmsNoticeKind.HelpRequest, "D0I1");
        Assert.That(hr.NoticeId, Is.Not.EqualTo(help.NoticeId));
        Assert.That(presenter.TrySelectNotice(hr.NoticeId), Is.True);
        presenter.State.SetReaderScrollPosition(0.2f);
        presenter.State.SetListScrollPosition(0.3f);
        Assert.That(presenter.TrySelectNotice(help.NoticeId), Is.True);
        presenter.State.SetReaderScrollPosition(0.8f);
        Assert.That(presenter.GetSelectedNotice().Kind, Is.EqualTo(SwarmsNoticeKind.HelpRequest));

        var reopened = new SwarmsPresenter(context.Session, apps.Swarms);
        Assert.That(reopened.GetSelectedNotice().NoticeId, Is.EqualTo(help.NoticeId));
        Assert.That(reopened.State.ReaderScrollPosition, Is.EqualTo(0.8f));
        Assert.That(reopened.State.ListScrollPosition, Is.EqualTo(0.3f));
        Assert.That(reopened.TrySelectNotice(hr.NoticeId), Is.True);
        Assert.That(reopened.GetSelectedNotice().Kind, Is.EqualTo(SwarmsNoticeKind.HrIncident));
        Assert.That(reopened.State.ReaderScrollPosition, Is.EqualTo(0.2f));
        Assert.That(apps.ShareLess.SelectedItemId, Is.Null);
        Assert.That(new SwarmsPresenter(context.Session, new AppReadingState())
            .GetSelectedNotice(), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("D0I1")]
    [TestCase("0:d0i1")]
    [TestCase("0:D0I2")]
    [TestCase("2:D0I1")]
    public void InvalidOrUndeliveredNoticeSelection_PreservesCurrentState(string invalidId)
    {
        var context = CreateCollisionContext();
        context.AdvanceTo(595);
        var presenter = new SwarmsPresenter(context.Session, new AppReadingState());
        var help = Find(presenter, SwarmsNoticeKind.HelpRequest, "D0I1");
        presenter.TrySelectNotice(help.NoticeId);
        presenter.State.SetReaderScrollPosition(0.6f);
        Assert.That(presenter.TrySelectNotice(invalidId), Is.False);
        Assert.That(presenter.GetSelectedNotice().NoticeId, Is.EqualTo(help.NoticeId));
        Assert.That(presenter.State.ReaderScrollPosition, Is.EqualTo(0.6f));
    }

    [TestCase(0, 0, "10:20")]
    [TestCase(12, 0, "10:20:12")]
    [TestCase(0, 345, "10:20:00.345")]
    [TestCase(12, 345, "10:20:12.345")]
    public void FixedHelpNotice_UsesWholeWindowAndPreciseAuthoredCutoff(
        int second, int millisecond, string cutoffText)
    {
        DayData source = ConsequenceTestContext.CreateDay();
        DayRequestPlan original = source.Requests[0];
        var plan = new DayRequestPlan(original.InstanceId, original.Request, original.Profile,
            original.DeliveryAt, original.HelpWindow, new GameTime(3, 620, second, millisecond));
        var day = new DayData(source.ScheduleData, new List<EvidenceDefinition>(),
            new List<DayIncidentPlan>(), new List<DayRequestPlan> { plan });
        var context = new ConsequenceTestContext(day);
        context.Advance(595);
        var presenter = new SwarmsPresenter(context.Session, new AppReadingState());
        var notice = Find(presenter, SwarmsNoticeKind.HelpRequest, "R1");
        string expectedBody = "From: alice\nHelp window: 10:30–10:45\n" +
            $"Arrange by: {cutoffText}\nStatus: Open\n\nArrange help.\n\n" +
            "View this request in Deleg8.";
        Assert.That(notice.BodyText, Is.EqualTo(expectedBody));
        Assert.That(notice.StatusText, Is.EqualTo("Open · Arrange by " + cutoffText));

        context.Advance(600);
        Assert.That(Find(presenter, SwarmsNoticeKind.HelpRequest, "R1").BodyText,
            Is.EqualTo(expectedBody), "Reading later must not turn authored times into a live clock/countdown.");
    }

    [Test]
    public void MondayRequest_UsesPublicRequesterNameAndAuthoredDescription()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(595);
        var presenter = new SwarmsPresenter(context.Session, new AppReadingState());
        var notice = Find(presenter, SwarmsNoticeKind.HelpRequest, "D0R1");
        Assert.That(notice.BodyText, Does.StartWith("From: Alice\n"));
        Assert.That(notice.BodyText, Does.Contain(context.Session.GetRequestOverview()[0].Request.Description));
        Assert.That(notice.BodyText, Does.Contain("Help window: 10:30–10:45"));
        Assert.That(notice.BodyText, Does.Not.Contain("Trust"));
    }

    [Test]
    public void EndOfShiftNotice_StatesBeforeClosingAndRefreshesToExpiredAtClosing()
    {
        var context = new ConsequenceTestContext(ConsequenceTestContext.CreateDay(fixedWindow: false));
        context.Advance(595);
        var presenter = new SwarmsPresenter(context.Session, new AppReadingState());
        var open = Find(presenter, SwarmsNoticeKind.HelpRequest, "R1");
        presenter.TrySelectNotice(open.NoticeId);
        Assert.That(open.BodyText, Does.Contain("Arrange before shift end (17:00)."));
        Assert.That(open.BodyText, Does.Not.Contain("Help window:"));
        Assert.That(open.StatusText, Is.EqualTo("Open · Before shift end"));
        context.Advance(1020);
        var expired = presenter.GetSelectedNotice();
        Assert.That(expired.NoticeId, Is.EqualTo(open.NoticeId));
        Assert.That(expired.StatusText, Is.EqualTo("Expired · Before shift end"));
        Assert.That(expired.BodyText, Does.Contain("Status: Expired"));
        Assert.That(open.StatusText, Is.EqualTo("Open · Before shift end"));
        Assert.That(context.Session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
    }

    [TestCase("dave")]
    [TestCase("steve")]
    [TestCase("karen")]
    [TestCase("bob")]
    public void RealDecision_RefreshesSameNoticeWithoutChangingOldSnapshotOrRepeatingEffects(string helper)
    {
        var context = new ConsequenceTestContext();
        context.Advance(595);
        var state = new AppReadingState();
        var presenter = new SwarmsPresenter(context.Session, state);
        var open = Find(presenter, SwarmsNoticeKind.HelpRequest, "R1");
        presenter.TrySelectNotice(open.NoticeId);
        state.SetReaderScrollPosition(0.4f);
        Assert.That(context.Session.TryDelegate("R1", helper).IsAccepted, Is.True);
        var answered = presenter.GetSelectedNotice();
        Assert.That(answered.NoticeId, Is.EqualTo(open.NoticeId));
        Assert.That(answered.StatusText, Is.EqualTo("Submitted · Arrange by 10:30"));
        Assert.That(answered.BodyText, Does.Contain("Status: Submitted"));
        Assert.That(answered.BodyText, Does.Not.Contain("completed"));
        Assert.That(answered.BodyText, Does.Not.Contain("Trust"));
        Assert.That(open.StatusText, Is.EqualTo("Open · Arrange by 10:30"));
        Assert.That(open.BodyText, Does.Contain("Status: Open"));

        var reopened = new SwarmsPresenter(context.Session, state);
        Assert.That(reopened.GetSelectedNotice().BodyText, Is.EqualTo(answered.BodyText));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.4f));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(context.Session.Workload.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void FixedCutoff_NoticeRemainsOpenAtEqualityAndExpiresOneMillisecondLater()
    {
        var context = new ConsequenceTestContext();
        context.Advance(595);
        var presenter = new SwarmsPresenter(context.Session, new AppReadingState());
        var open = Find(presenter, SwarmsNoticeKind.HelpRequest, "R1");
        presenter.TrySelectNotice(open.NoticeId);
        context.Advance(630);
        Assert.That(presenter.GetSelectedNotice().StatusText, Does.StartWith("Open ·"));
        context.Advance(630, 0, 1);
        Assert.That(presenter.GetSelectedNotice().StatusText, Does.StartWith("Expired ·"));
        Assert.That(open.StatusText, Does.StartWith("Open ·"));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void ReadingMixedNotices_DoesNotMutateDomainEvidenceNotificationsOrTime()
    {
        var context = CreateCollisionContext();
        context.AdvanceTo(595);
        var session = context.Session;
        var presenter = new SwarmsPresenter(session, new AppReadingState());
        long now = session.Now.TotalMilliseconds;
        int pendingNotices = session.Notifications.PendingCount;
        int changes = 0;
        session.EvidenceChanged += id => changes++;
        session.Consequences.ConsequenceCommitted += entry => changes++;
        List<SwarmsNoticeViewData> first = presenter.GetNotices();
        first.Clear();
        Assert.That(presenter.GetNotices().Count, Is.EqualTo(2));
        for (int i = 0; i < 3; i++)
        {
            foreach (var notice in presenter.GetNotices())
            {
                Assert.That(presenter.TrySelectNotice(notice.NoticeId), Is.True);
                Assert.That(presenter.GetSelectedNotice().BodyText, Is.EqualTo(notice.BodyText));
            }
        }
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(now));
        Assert.That(session.Notifications.PendingCount, Is.EqualTo(pendingNotices));
        Assert.That(changes, Is.Zero);
        Assert.That(session.IsEvidenceRead("E_SCHEDULE"), Is.False);
        Assert.That(session.IsEvidenceDiscovered("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
        Assert.That(session.Deliveries.GetDelivered("D0I1").Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(session.GetRequestOverview()[0].Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(session.Consequences.History, Is.Empty);
        Assert.That(session.Workload.History, Is.Empty);
        Assert.That(session.Progress.Credibility, Is.EqualTo(100));
        foreach (string id in session.Trust.EmployeeIds)
            Assert.That(session.Trust.GetTrust(id), Is.EqualTo(50));
    }

    private static SwarmsNoticeViewData Find(
        SwarmsPresenter presenter, SwarmsNoticeKind kind, string sourceId)
    {
        foreach (var notice in presenter.GetNotices())
            if (notice.Kind == kind && string.Equals(notice.SourceId, sourceId, StringComparison.Ordinal))
                return notice;
        Assert.Fail($"Missing {kind} notice for {sourceId}.");
        return null;
    }

    private static WorkdaySessionTestContext CreateCollisionContext()
    {
        DayData monday = new WorkdaySessionTestContext().Day;
        DayRequestPlan original = monday.Requests[0];
        var collision = new DayRequestPlan("D0I1", original.Request, original.Profile,
            original.DeliveryAt, original.HelpWindow, original.CutoffAt);
        var day = new DayData(monday.ScheduleData,
            new List<EvidenceDefinition>(monday.EvidenceDatabase.All),
            new List<DayIncidentPlan>(monday.Incidents), new List<DayRequestPlan> { collision });
        return new WorkdaySessionTestContext(day);
    }
}
