using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public class WorkdayDelegationIntegrationTests
{
    private static DayData MondayInputs()
    {
        var definition = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
        Assert.That(definition, Is.Not.Null);
        return definition.GetData();
    }

    private static DayData TestDay(bool lateHr = false, bool originalHr = false,
        bool emptyRequests = false)
    {
        DayData monday = MondayInputs();
        var incidents = new List<DayIncidentPlan>();
        if (originalHr) incidents.AddRange(monday.Incidents);
        if (lateHr)
        {
            var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
                "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
            Assert.That(definition, Is.Not.Null);
            incidents.Add(new DayIncidentPlan(definition, new IncidentScheduleEntry(
                "late_hr", definition.IncidentId, new TimeRange(0, 635, 640), 1000, 1019), "dave"));
        }
        var requests = new List<DayRequestPlan>();
        if (!emptyRequests)
        {
            requests.AddRange(monday.Requests);
            var endRequest = monday.Requests[1];
            requests.Add(new DayRequestPlan("extra_end", endRequest.Request, endRequest.Profile,
                endRequest.DeliveryAt));
            var fixedRequest = monday.Requests[0];
            requests.Add(new DayRequestPlan("future_help", fixedRequest.Request, fixedRequest.Profile,
                new GameTime(0, 950), new TimeRange(0, 960, 970)));
        }
        return new DayData(monday.ScheduleData,
            new List<EvidenceDefinition>(monday.EvidenceDatabase.All), incidents, requests);
    }

    private static void Reject(DelegationSubmissionResult result)
    {
        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Decision, Is.Null);
        Assert.That(string.IsNullOrWhiteSpace(result.Reason), Is.False);
    }

    private static RequestOverviewViewData Overview(WorkdaySession session, string id)
    {
        foreach (var entry in session.GetRequestOverview())
            if (entry.InstanceId == id) return entry;
        Assert.Fail("Missing delivered request: " + id);
        return null;
    }

    [Test]
    public void Closing_ExpiresAllOpenEndRequestsBeforeCompletedSummaryAndRejectsCommands()
    {
        var context = new WorkdaySessionTestContext(TestDay());
        var session = context.Session;
        var order = new List<string>();
        int closingExpiries = 0;
        session.Delegation.RequestExpired += entry =>
        {
            if (entry.ExpiresAt.TotalMilliseconds != session.Settings.ClosesAt.TotalMilliseconds) return;
            closingExpiries++;
            order.Add("expiry");
            Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
            Assert.That(entry.RecordedAt.TotalMilliseconds, Is.EqualTo(session.Now.TotalMilliseconds));
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Working));
            Assert.That(session.Workday.CanAcceptWork, Is.False);
            Assert.That(session.Workday.Summary, Is.Null);
            Reject(session.TryDelegate(entry.RequestInstanceId, "bob"));
        };
        session.Workday.StateChanged += () =>
        {
            order.Add("completed");
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
            Assert.That(closingExpiries, Is.EqualTo(2));
            Assert.That(Overview(session, "D0R2").Status, Is.EqualTo(RequestStatus.Expired));
            Assert.That(Overview(session, "extra_end").Status, Is.EqualTo(RequestStatus.Expired));
            Reject(session.TryDelegate("D0R2", "bob"));
        };
        context.AdvanceTo(1019, 59);
        Assert.That(Overview(session, "D0R2").Status, Is.EqualTo(RequestStatus.Open));
        context.AdvanceTo(1020);
        CollectionAssert.AreEqual(new[] { "expiry", "expiry", "completed" }, order);
        var summary = session.Workday.Summary;
        Assert.That(summary.Credibility, Is.EqualTo(60));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(40));
        Assert.That(session.Trust.GetTrust("karen"), Is.EqualTo(40));
        int historyCount = session.Delegation.Expiries.Count;
        session.AdvanceRealSeconds(10000);
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
        Assert.That(session.Workday.Summary, Is.SameAs(summary));
        Assert.That(session.Delegation.Expiries.Count, Is.EqualTo(historyCount));
    }

    [Test]
    public void ClosingWithPendingHr_SettlesRequestsThenWaitsForRealReview()
    {
        var context = new WorkdaySessionTestContext(TestDay(lateHr: true));
        var session = context.Session;
        context.AdvanceTo(1018);
        context.Submit("late_hr", "dave", "E_DAVE_PRINTER");
        context.AdvanceTo(1020);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Closing));
        Assert.That(session.Workday.Summary, Is.Null);
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(1));
        Assert.That(Overview(session, "D0R2").Status, Is.EqualTo(RequestStatus.Expired));
        Assert.That(Overview(session, "extra_end").Status, Is.EqualTo(RequestStatus.Expired));
        int expiryCount = session.Delegation.Expiries.Count;
        Reject(session.TryDelegate("D0R2", "bob"));
        context.AdvanceTo(1033);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(session.Progress.History.Count, Is.EqualTo(1));
        Assert.That(session.Progress.History[0].Decision.Verdict, Is.EqualTo(HrVerdict.Approved));
        Assert.That(session.Delegation.Expiries.Count, Is.EqualTo(expiryCount));
        Assert.That(session.Progress.Credibility, Is.EqualTo(60));
        Assert.That(session.Progress.History[0].CredibilityBefore, Is.EqualTo(60));
        Assert.That(session.Progress.History[0].CredibilityAfter, Is.EqualTo(60));
    }

    [Test]
    public void HrReviewAtClosing_ThenRequestExpiry_ThenTerminalSummary()
    {
        var context = new WorkdaySessionTestContext(TestDay(lateHr: true));
        var session = context.Session;
        var order = new List<string>();
        context.AdvanceTo(1005);
        context.Submit("late_hr", "dave", "E_DAVE_PRINTER");
        session.Reviews.ReviewCompleted += (review, result) =>
        {
            order.Add("hr");
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Working));
            Assert.That(session.Workday.CanAcceptWork, Is.False);
            Reject(session.TryDelegate("D0R2", "bob"));
        };
        session.Delegation.RequestExpired += entry =>
        {
            if (entry.ExpiresAt.TotalMilliseconds == session.Settings.ClosesAt.TotalMilliseconds)
                order.Add("expiry");
        };
        session.Workday.StateChanged += () => order.Add("completed");
        context.AdvanceTo(1020);
        CollectionAssert.AreEqual(new[] { "hr", "expiry", "expiry", "completed" }, order);
    }

    [Test]
    public void ExistingHrFiring_StopsRequestEventsAndRejectsLaterDelegation()
    {
        var context = new WorkdaySessionTestContext(TestDay(originalHr: true));
        var session = context.Session;
        int resolvedEvents = 0;
        session.Delegation.DecisionResolved += decision => resolvedEvents++;
        session.AdvanceRealSeconds(10000);
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Fired));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(new GameTime(0, 900, 0, 1).TotalMilliseconds));
        Assert.That(Overview(session, "D0R2").Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(session.Delegation.TryGetDelivered("future_help", out var missing), Is.False);
        Assert.That(missing, Is.Null);
        int expiryCount = session.Delegation.Expiries.Count;
        var summary = session.Workday.Summary;
        Reject(session.TryDelegate("D0R2", "bob"));
        Reject(session.TryDelegate("future_help", "steve"));
        session.SetPaused(true);
        session.SetPaused(false);
        session.AdvanceRealSeconds(10000);
        Assert.That(session.Workday.Summary, Is.SameAs(summary));
        Assert.That(session.Delegation.Expiries.Count, Is.EqualTo(expiryCount));
        Assert.That(resolvedEvents, Is.Zero);
        Assert.That(session.GetRequestOverview().Count, Is.EqualTo(3));
    }

    [Test]
    public void OverviewIsDetachedSnapshot_AndQueriesDoNotReorderRosterOrReplayDecision()
    {
        var context = new WorkdaySessionTestContext(TestDay());
        var session = context.Session;
        Reject(session.TryDelegate("D0R1", "steve"));
        Assert.That(session.GetRequestOverview(), Is.Empty);
        context.AdvanceTo(600);
        var old = Overview(session, "D0R1");
        var oldList = session.GetRequestOverview();
        var roster = session.GetEmployeeRoster();
        int resolved = 0;
        session.Delegation.DecisionResolved += decision => resolved++;
        var result = session.TryDelegate("D0R1", "steve");
        Assert.That(result.IsAccepted, Is.True);
        oldList.Clear();
        var current = Overview(session, "D0R1");
        Assert.That(old.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(old.HelperId, Is.Empty);
        Assert.That(old.Explanation, Is.Empty);
        Assert.That(current.Status, Is.EqualTo(RequestStatus.Resolved));
        Assert.That(current.HelperId, Is.EqualTo("steve"));
        Assert.That(current.Explanation, Is.EqualTo(result.Decision.Explanation));
        Assert.That(current.SubmissionCutoff.TotalMilliseconds, Is.EqualTo(new GameTime(0, 630).TotalMilliseconds));
        Reject(session.TryDelegate("D0R1", "dave"));
        context.AdvanceTo(860);
        Assert.That(Overview(session, "D0R2").SubmissionCutoff.TotalMilliseconds,
            Is.EqualTo(session.Settings.ClosesAt.TotalMilliseconds));
        context.AdvanceTo(1020);
        Assert.That(current.Status, Is.EqualTo(RequestStatus.Resolved));
        Assert.That(resolved, Is.EqualTo(1));
        var afterRoster = session.GetEmployeeRoster();
        Assert.That(afterRoster.Count, Is.EqualTo(roster.Count));
        for (int i = 0; i < roster.Count; i++)
            Assert.That(afterRoster[i].EmployeeId, Is.EqualTo(roster[i].EmployeeId));
    }

    [Test]
    public void PublicOverview_ExcludesHiddenRankingAndMutableRuntimeReferences()
    {
        Assert.Throws<ArgumentNullException>(() => new RequestOverviewViewData(null));
        var properties = typeof(RequestOverviewViewData).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var names = new List<string>();
        foreach (var property in properties)
        {
            names.Add(property.Name);
            Assert.That(property.GetSetMethod(), Is.Null);
            Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(RequestInstance)));
            Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(RequestProfileData)));
            Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(DelegationOutcome)));
            Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(DelegationDecision)));
        }
        CollectionAssert.AreEquivalent(new[] { "InstanceId", "Request", "DeliveryAt", "HelpWindow",
            "SubmissionCutoff", "Status", "HelperId", "Explanation" }, names);
    }

    [Test]
    public void PausedSession_UsesSameFrozenSchedulerForRequestsAndResumesNormally()
    {
        var context = new WorkdaySessionTestContext(TestDay());
        var session = context.Session;
        session.SetPaused(true);
        session.AdvanceRealSeconds(10000);
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(session.Settings.OpensAt.TotalMilliseconds));
        Assert.That(session.GetRequestOverview(), Is.Empty);
        session.SetPaused(false);
        context.AdvanceTo(600);
        Assert.That(session.GetRequestOverview().Count, Is.EqualTo(1));
        session.SetPaused(true);
        session.AdvanceRealSeconds(10000);
        Assert.That(Overview(session, "D0R1").Status, Is.EqualTo(RequestStatus.Open));
        session.SetPaused(false);
        context.AdvanceTo(631);
        Assert.That(Overview(session, "D0R1").Status, Is.EqualTo(RequestStatus.Expired));
    }

    [Test]
    public void LegacyEmptyRequestDay_CompletesWithNoRequestEvents()
    {
        var context = new WorkdaySessionTestContext(TestDay(emptyRequests: true));
        Assert.That(context.Session.Delegation, Is.Not.Null);
        context.AdvanceTo(1020);
        Assert.That(context.Session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(context.Session.GetRequestOverview(), Is.Empty);
        Assert.That(context.Session.Delegation.Expiries, Is.Empty);
        Reject(context.Session.TryDelegate("D0R1", "steve"));
    }
}
