using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ReportOverviewPresenterTests
{
    [TestCase(0)]
    [TestCase(1)]
    public void Constructor_RejectsMissingDependency(int dependency)
    {
        WorkdaySession session = new WorkdaySessionTestContext().Session;
        Assert.Throws<ArgumentNullException>(() => new ReportOverviewPresenter(
            dependency == 0 ? null : session,
            dependency == 1 ? null : new AppReadingState()));
    }

    [Test]
    public void Snapshot_RejectsNullIncident()
    {
        Assert.Throws<ArgumentNullException>(() => new ReportOverviewViewData(null));
    }

    [Test]
    public void BeforeDelivery_EntriesAreEmptyAndNothingIsSelected()
    {
        var session = new WorkdaySessionTestContext().Session;
        var state = new AppReadingState();
        var presenter = new ReportOverviewPresenter(session, state);
        Assert.That(presenter.GetEntries(), Is.Empty);
        Assert.That(presenter.GetSelectedEntry(), Is.Null);
        Assert.That(state.SelectedItemId, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("UNKNOWN")]
    [TestCase("d0i1")]
    [TestCase("D0I2")]
    public void InvalidOrUndeliveredSelection_PreservesExistingSelection(string id)
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        var state = new AppReadingState();
        var presenter = new ReportOverviewPresenter(context.Session, state);
        Assert.That(presenter.TrySelectEntry("D0I1"), Is.True);
        Assert.That(presenter.TrySelectEntry(id), Is.False);
        Assert.That(state.SelectedItemId, Is.EqualTo("D0I1"));
    }

    [Test]
    public void ReadingOverview_DoesNotDiscoverEvidenceSubmitOrAdvanceTime()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        var presenter = new ReportOverviewPresenter(context.Session, new AppReadingState());
        long now = context.Session.Now.TotalMilliseconds;
        Assert.That(presenter.TrySelectEntry("D0I1"), Is.True);
        ReportOverviewViewData entry = presenter.GetSelectedEntry();
        Assert.That(entry.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(entry.SuspectId, Is.Null);
        Assert.That(entry.EvidenceIds, Is.Empty);
        Assert.That(entry.DecisionSummary, Is.Empty);
        Assert.That(entry.DecisionReasons, Is.Empty);
        Assert.That(entry.DueTimeText, Is.EqualTo("10:30"));
        Assert.That(context.Session.Now.TotalMilliseconds, Is.EqualTo(now));
        Assert.That(context.Session.IsEvidenceDiscovered("E_SCHEDULE"), Is.False);
        Assert.That(context.Session.IsEvidenceDiscovered("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(context.Session.Submissions.PendingReviews, Is.Empty);
    }

    [Test]
    public void SnapshotsAndCollections_RemainIsolatedWhenDomainStateChanges()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        var presenter = new ReportOverviewPresenter(context.Session, new AppReadingState());
        Assert.That(presenter.TrySelectEntry("D0I1"), Is.True);
        List<ReportOverviewViewData> initialList = presenter.GetEntries();
        ReportOverviewViewData open = initialList[0];
        initialList.Clear();
        Assert.That(presenter.GetEntries().Count, Is.EqualTo(1));

        context.Submit("D0I1", "bob", "E_SCHEDULE");
        ReportOverviewViewData pending = presenter.GetSelectedEntry();
        Assert.That(open.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(open.SuspectId, Is.Null);
        Assert.That(pending.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(pending.SuspectId, Is.EqualTo("bob"));
        CollectionAssert.AreEqual(new[] { "E_SCHEDULE" }, pending.EvidenceIds);
        Assert.That(pending.DecisionReasons, Is.Empty);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)pending.EvidenceIds).Add("OTHER"));

        context.AdvanceTo(600);
        ReportOverviewViewData approved = presenter.GetSelectedEntry();
        Assert.That(pending.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(pending.DecisionSummary, Is.Empty);
        Assert.That(approved.Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(approved.DecisionSummary, Is.Not.Empty);
        Assert.That(approved.DecisionReasons, Is.Not.Empty);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)approved.DecisionReasons).Clear());
    }

    [Test]
    public void ReopeningAfterHiddenReview_KeepsSelectionAndQueriesLatestDecision()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        var state = new AppReadingState();
        var presenter = new ReportOverviewPresenter(context.Session, state);
        Assert.That(presenter.TrySelectEntry("D0I1"), Is.True);
        state.SetListScrollPosition(0.25f);
        state.SetReaderScrollPosition(0.4f);
        int oldChanges = 0;
        presenter.Changed += () => oldChanges++;
        presenter.Connect();
        presenter.Disconnect();
        int before = oldChanges;
        context.Submit("D0I1", "bob", "E_SCHEDULE");
        context.AdvanceTo(600);
        Assert.That(oldChanges, Is.EqualTo(before));

        var reopened = new ReportOverviewPresenter(context.Session, state);
        int refreshes = 0;
        reopened.Changed += () => refreshes++;
        reopened.Connect();
        Assert.That(refreshes, Is.EqualTo(1));
        Assert.That(reopened.GetSelectedEntry().Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(state.SelectedItemId, Is.EqualTo("D0I1"));
        Assert.That(state.ListScrollPosition, Is.EqualTo(0.25f));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.4f));
        reopened.Disconnect();
    }

    [Test]
    public void RepeatedConnect_DoesNotDuplicateDeliverySubmissionOrReviewEvents()
    {
        var context = new WorkdaySessionTestContext();
        var presenter = new ReportOverviewPresenter(context.Session, new AppReadingState());
        int changes = 0;
        presenter.Changed += () => changes++;
        presenter.Connect();
        presenter.Connect();
        Assert.That(changes, Is.EqualTo(2));
        changes = 0;
        context.AdvanceTo(590);
        Assert.That(changes, Is.EqualTo(1));
        context.Submit("D0I1", "bob", "E_SCHEDULE");
        Assert.That(changes, Is.EqualTo(2));
        context.AdvanceTo(600);
        Assert.That(changes, Is.EqualTo(3));
        presenter.Disconnect();
        presenter.Disconnect();
        context.AdvanceTo(675);
        Assert.That(changes, Is.EqualTo(3));
    }

    [Test]
    public void SelectedOverdueIncident_RemainsReadableAndRaisesRefresh()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        var presenter = new ReportOverviewPresenter(context.Session, new AppReadingState());
        Assert.That(presenter.TrySelectEntry("D0I1"), Is.True);
        presenter.Connect();
        int changes = 0;
        presenter.Changed += () => changes++;
        context.AdvanceTo(631);
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(presenter.GetSelectedEntry().Status, Is.EqualTo(IncidentStatus.Overdue));
        presenter.Disconnect();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalDay_StillOffersDeliveredHistoryWithoutAcceptingNewWork(bool fired)
    {
        var context = new WorkdaySessionTestContext();
        if (!fired)
        {
            context.AdvanceTo(590);
            context.Submit("D0I1", "bob", "E_SCHEDULE");
        }
        context.Session.AdvanceRealSeconds(10000);
        Assert.That(context.Session.Workday.State, Is.EqualTo(
            fired ? WorkdayState.Fired : WorkdayState.Completed));
        var presenter = new ReportOverviewPresenter(context.Session, new AppReadingState());
        Assert.That(presenter.GetEntries().Count, Is.EqualTo(3));
        Assert.That(presenter.TrySelectEntry("D0I1"), Is.True);
        Assert.That(presenter.GetSelectedEntry().Status, Is.EqualTo(
            fired ? IncidentStatus.Overdue : IncidentStatus.Approved));
        Assert.That(context.Session.Workday.CanAcceptWork, Is.False);
    }
}
