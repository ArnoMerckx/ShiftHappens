using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ShareLessPresenterTests
{
    [Test]
    public void MissingDependencies_AreRejected()
    {
        var session = new ConsequenceTestContext().Session;
        Assert.Throws<ArgumentNullException>(() =>
            new ShareLessPresenter(null, new AppReadingState()));
        Assert.Throws<ArgumentNullException>(() =>
            new ShareLessPresenter(session, null));
    }

    [Test]
    public void Roster_PreservesAuthoredOrderAndBiosDespiteDifferentTrustAndPresentationOrder()
    {
        DayData day = CreateProfileDay();
        var opening = new WorkdayOpeningState(80, new List<EmployeeTrustEntry> {
            new EmployeeTrustEntry("karen", 50),
            new EmployeeTrustEntry("dave", 0),
            new EmployeeTrustEntry("bob", 99),
            new EmployeeTrustEntry("alice", 50),
            new EmployeeTrustEntry("steve", 20)
        }, new List<AssignmentHistoryEntry>());
        var session = new WorkdaySession(day, openingState: opening);
        var presenter = new ShareLessPresenter(session, new AppReadingState());

        List<EmployeeViewData> employees = presenter.GetEmployees();
        CollectionAssert.AreEqual(new[] { "steve", "alice", "bob", "dave", "karen" },
            employees.ConvertAll(employee => employee.EmployeeId));
        Assert.That(employees[0].DisplayName, Is.EqualTo("Steve"));
        Assert.That(employees[0].Role, Is.EqualTo(EmployeeRole.Dispatch));
        Assert.That(employees[0].Bio, Is.EqualTo("Moves dispatch equipment."));
        Assert.That(employees[0].InterestsAndExperience, Is.EqualTo("Builds and troubleshoots PCs."));
        Assert.That(presenter.GetTrust("steve"), Is.EqualTo(20));
        Assert.That(presenter.GetTrust("bob"), Is.EqualTo(99));
        Assert.That(presenter.GetTrust("dave"), Is.Zero);

        employees.Reverse();
        employees.Clear();
        CollectionAssert.AreEqual(new[] { "steve", "alice", "bob", "dave", "karen" },
            presenter.GetEmployees().ConvertAll(employee => employee.EmployeeId));
        Assert.That(session.GetEmployeeRoster().Count, Is.EqualTo(5));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("unknown")]
    [TestCase("STEVE")]
    [TestCase(" steve ")]
    public void InvalidSelection_PreservesSelectedEmployeeAndScroll(string invalidId)
    {
        var state = new AppReadingState();
        var presenter = new ShareLessPresenter(new ConsequenceTestContext().Session, state);
        Assert.That(presenter.TrySelectEmployee("steve"), Is.True);
        state.SetListScrollPosition(0.25f);
        state.SetReaderScrollPosition(0.75f);

        Assert.That(presenter.TrySelectEmployee(invalidId), Is.False);
        Assert.That(presenter.GetSelectedEmployee().EmployeeId, Is.EqualTo("steve"));
        Assert.That(state.ListScrollPosition, Is.EqualTo(0.25f));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.75f));
    }

    [Test]
    public void MissingOrStaleSelection_ReturnsNoProfileWithoutSelectingAnAlternative()
    {
        var state = new AppReadingState();
        var presenter = new ShareLessPresenter(new ConsequenceTestContext().Session, state);
        Assert.That(presenter.GetSelectedEmployee(), Is.Null);
        Assert.That(state.SelectedItemId, Is.Null);
        state.SelectItem("removed_employee");
        Assert.That(presenter.GetSelectedEmployee(), Is.Null);
        Assert.That(state.SelectedItemId, Is.EqualTo("removed_employee"));
    }

    [Test]
    public void ReconstructedPresenter_RetainsSelectionAndPerEmployeeScrollInExistingAppState()
    {
        var session = new ConsequenceTestContext().Session;
        var apps = new ShiftOSAppState();
        var first = new ShareLessPresenter(session, apps.ShareLess);
        first.TrySelectEmployee("steve");
        apps.ShareLess.SetListScrollPosition(0.3f);
        apps.ShareLess.SetReaderScrollPosition(0.7f);
        first.TrySelectEmployee("alice");
        apps.ShareLess.SetReaderScrollPosition(0.2f);

        var reopened = new ShareLessPresenter(session, apps.ShareLess);
        Assert.That(reopened.State, Is.SameAs(apps.ShareLess));
        Assert.That(reopened.GetSelectedEmployee().EmployeeId, Is.EqualTo("alice"));
        Assert.That(reopened.State.ReaderScrollPosition, Is.EqualTo(0.2f));
        Assert.That(reopened.TrySelectEmployee("steve"), Is.True);
        Assert.That(reopened.State.ReaderScrollPosition, Is.EqualTo(0.7f));
        Assert.That(reopened.State.ListScrollPosition, Is.EqualTo(0.3f));
        Assert.That(apps.Swarms.SelectedItemId, Is.Null);
        Assert.That(apps.InWatch.SelectedItemId, Is.Null);
        var separate = new ShareLessPresenter(session, new ShiftOSAppState().ShareLess);
        Assert.That(separate.GetSelectedEmployee(), Is.Null);
    }

    [TestCase("dave", 50, 55)]
    [TestCase("dave", 98, 100)]
    [TestCase("bob", 50, 45)]
    [TestCase("bob", 2, 0)]
    public void ExistingPresenter_ReadsCurrentTrustAfterRealCommittedOutcome(
        string helper, int initialTrust, int expectedTrust)
    {
        var context = new ConsequenceTestContext(CreateProfileDay(), trust: initialTrust);
        var presenter = new ShareLessPresenter(context.Session, new AppReadingState());
        presenter.TrySelectEmployee(helper);
        EmployeeViewData bio = presenter.GetSelectedEmployee();
        Assert.That(presenter.GetTrust(helper), Is.EqualTo(initialTrust));
        context.Advance(595);

        var result = context.Session.TryDelegate("R1", helper);
        Assert.That(result.IsAccepted, Is.True, result.Reason);
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(presenter.GetTrust(helper), Is.EqualTo(expectedTrust));
        Assert.That(presenter.GetTrust("alice"), Is.EqualTo(expectedTrust));
        Assert.That(presenter.GetSelectedEmployee(), Is.SameAs(bio));
        Assert.That(presenter.State.SelectedItemId, Is.EqualTo(helper));
        Assert.That(presenter.GetSelectedEmployee().Bio, Is.EqualTo(bio.Bio));
        var reopened = new ShareLessPresenter(context.Session, presenter.State);
        Assert.That(reopened.GetTrust(helper), Is.EqualTo(expectedTrust));
        Assert.That(context.Session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(context.Session.Workload.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void BrowsingProfiles_DoesNotReadEvidenceSubmitWorkAdvanceTimeOrPublishNotifications()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(595);
        var session = context.Session;
        var state = new AppReadingState();
        var presenter = new ShareLessPresenter(session, state);
        long beforeTime = session.Now.TotalMilliseconds;
        int beforeNotices = session.Notifications.PendingCount;
        var beforeRequests = session.GetRequestOverview();
        Assert.That(beforeRequests.Count, Is.GreaterThan(0));
        int changes = 0;
        session.EvidenceChanged += id => changes++;
        session.Consequences.ConsequenceCommitted += entry => changes++;

        for (int i = 0; i < 3; i++)
        {
            foreach (EmployeeViewData employee in presenter.GetEmployees())
            {
                Assert.That(presenter.TrySelectEmployee(employee.EmployeeId), Is.True);
                Assert.That(presenter.GetSelectedEmployee().EmployeeId, Is.EqualTo(employee.EmployeeId));
                Assert.That(presenter.GetTrust(employee.EmployeeId), Is.EqualTo(50));
            }
        }

        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(beforeTime));
        Assert.That(session.Notifications.PendingCount, Is.EqualTo(beforeNotices));
        Assert.That(changes, Is.Zero);
        Assert.That(session.Consequences.History, Is.Empty);
        Assert.That(session.Workload.History, Is.Empty);
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
        Assert.That(session.IsEvidenceRead("E_SCHEDULE"), Is.False);
        Assert.That(session.IsEvidenceDiscovered("E_SUPPLIES_RECEIPT"), Is.False);
        var afterRequests = session.GetRequestOverview();
        Assert.That(afterRequests.Count, Is.EqualTo(beforeRequests.Count));
        for (int i = 0; i < beforeRequests.Count; i++)
        {
            Assert.That(afterRequests[i].InstanceId, Is.EqualTo(beforeRequests[i].InstanceId));
            Assert.That(afterRequests[i].Status, Is.EqualTo(beforeRequests[i].Status));
            Assert.That(afterRequests[i].HelperId, Is.EqualTo(beforeRequests[i].HelperId));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalReview_AllowsDirectoryNavigationWithoutNewConsequences(bool fired)
    {
        var context = new ConsequenceTestContext(credibility: fired ? 10 : 80);
        if (fired)
        {
            context.Advance(595);
            Assert.That(context.Session.TryDelegate("R1", "bob").IsAccepted, Is.True);
        }
        else
        {
            context.Advance(1020);
        }

        var session = context.Session;
        Assert.That(session.Workday.State, Is.EqualTo(
            fired ? WorkdayState.Fired : WorkdayState.Completed));
        Assert.That(session.Workday.CanAcceptWork, Is.False);
        int beforeHistory = session.Consequences.History.Count;
        int beforeAssignments = session.Workload.History.Count;
        int beforeScore = session.Progress.Credibility;
        long beforeTime = session.Now.TotalMilliseconds;
        var presenter = new ShareLessPresenter(session, new AppReadingState());
        Assert.That(presenter.TrySelectEmployee("bob"), Is.True);
        Assert.That(presenter.GetSelectedEmployee().EmployeeId, Is.EqualTo("bob"));
        Assert.That(presenter.GetTrust("bob"), Is.EqualTo(fired ? 45 : 50));
        Assert.That(presenter.TrySelectEmployee("steve"), Is.True);
        Assert.That(presenter.GetSelectedEmployee().EmployeeId, Is.EqualTo("steve"));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(beforeHistory));
        Assert.That(session.Workload.History.Count, Is.EqualTo(beforeAssignments));
        Assert.That(session.Progress.Credibility, Is.EqualTo(beforeScore));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(beforeTime));
    }

    private static DayData CreateProfileDay()
    {
        DayData source = ConsequenceTestContext.CreateDay();
        var profiles = new List<EmployeeViewData> {
            new EmployeeViewData("karen", "Karen"),
            new EmployeeViewData("dave", "Dave"),
            new EmployeeViewData("bob", "Bob"),
            new EmployeeViewData("alice", "Alice"),
            new EmployeeViewData("steve", "Steve", EmployeeRole.Dispatch,
                "Moves dispatch equipment.", "Builds and troubleshoots PCs.")
        };
        var schedule = new DayScheduleData(source.ScheduleData.Settings,
            new List<string> { "steve", "alice", "bob", "dave", "karen" },
            new List<ApprovedScheduleRecord>(source.ScheduleData.WorldApprovedRecords),
            new List<ApprovedScheduleRecord>(), new List<Claim>(), profiles,
            new List<EmployeeAvailability>(source.ScheduleData.OrdinaryAvailability));
        return new DayData(schedule, new List<EvidenceDefinition>(),
            new List<DayIncidentPlan>(), new List<DayRequestPlan>(source.Requests));
    }
}
