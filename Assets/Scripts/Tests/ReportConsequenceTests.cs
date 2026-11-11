using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public class ReportConsequenceTests
{
    private DayData monday;

    [SetUp]
    public void SetUp()
    {
        var definition = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
        Assert.That(definition, Is.Not.Null);
        monday = definition.GetData();
    }

    // Isolate the selected HR incident from unrelated missed deadlines/help requests.
    // Use unchanged Monday truth, schedules, knowledge and evidence assets.
    private ConsequenceTestContext Context(int index, int trust = 50, ConsequenceRules rules = null,
        List<DayRequestPlan> requests = null)
    {
        var day = new DayData(monday.ScheduleData,
            new List<EvidenceDefinition>(monday.EvidenceDatabase.All),
            new List<DayIncidentPlan> { monday.Incidents[index] }, requests);
        return new ConsequenceTestContext(day, trust: trust, rules: rules);
    }

    private static void ReplayReport(ConsequenceCoordinator coordinator, QueuedHrReview review)
    {
        var method = typeof(ConsequenceCoordinator).GetMethod("OnReportAccepted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var callback = (Action<QueuedHrReview>)Delegate.CreateDelegate(
            typeof(Action<QueuedHrReview>), coordinator, method);
        callback(review);
    }

    [TestCase(0, "bob", "E_SCHEDULE", HrVerdict.Approved, false)]
    [TestCase(0, "alice", "E_SCHEDULE", HrVerdict.Rejected, true)]
    [TestCase(1, "dave", "E_DAVE_PRINTER", HrVerdict.Approved, false)]
    [TestCase(1, "bob", "E_SCHEDULE", HrVerdict.Rejected, true)]
    [TestCase(1, "dave", "E_OFFICE_SOCIAL", HrVerdict.Rejected, false)]
    [TestCase(2, "karen", "E_KAREN_COLLECTION", HrVerdict.Approved, false)]
    [TestCase(2, "steve", "E_SUPPLIES_RECEIPT", HrVerdict.Approved, true)]
    [TestCase(2, "steve", "E_SCHEDULE", HrVerdict.Rejected, true)]
    public void AllEightCases_ApplyTrustAtAcceptanceAndOnlyHrCredibilityAtLaterReview(
        int index, string suspect, string evidenceId, HrVerdict verdict, bool wrongful)
    {
        var context = Context(index);
        var session = context.Session;
        var plan = monday.Incidents[index];
        context.Advance(plan.Schedule.DeliveryMinute, 12, 345);
        Assert.That(session.TryDiscoverEvidence(evidenceId), Is.True);
        int expectedTrust = wrongful ? 40 : 50;
        int queuedNotices = 0;
        session.Submissions.ReviewQueued += accepted =>
        {
            queuedNotices++;
            Assert.That(session.Trust.GetTrust(suspect), Is.EqualTo(expectedTrust));
            Assert.That(session.Progress.Credibility, Is.EqualTo(80));
            Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
            Assert.That(session.Progress.History, Is.Empty);
        };
        var result = session.Submissions.TrySubmit(plan.Schedule.InstanceId, suspect,
            new List<string> { evidenceId });
        Assert.That(result.IsValid, Is.True, result.Reason);
        Assert.That(queuedNotices, Is.EqualTo(1));
        Assert.That(session.Trust.GetTrust(suspect), Is.EqualTo(expectedTrust));
        Assert.That(session.Progress.Credibility, Is.EqualTo(80));
        Assert.That(session.Workload.History, Is.Empty);
        var consequence = session.Consequences.History[0];
        Assert.That(consequence.Kind, Is.EqualTo(ConsequenceKind.ReportAccepted));
        Assert.That(consequence.CredibilityBefore, Is.EqualTo(80));
        Assert.That(consequence.CredibilityAfter, Is.EqualTo(80));
        Assert.That(consequence.TrustChanges.Count, Is.EqualTo(wrongful ? 1 : 0));
        Assert.That(consequence.RecordedAt.TotalMilliseconds, Is.EqualTo(session.Now.TotalMilliseconds));
        if (wrongful)
        {
            Assert.That(consequence.TrustChanges[0].EmployeeId, Is.EqualTo(suspect));
            Assert.That(consequence.TrustChanges[0].Before, Is.EqualTo(50));
            Assert.That(consequence.TrustChanges[0].After, Is.EqualTo(40));
        }

        QueuedHrReview review = session.Submissions.PendingReviews[0];
        ReplayReport(session.Consequences, review);
        ReplayReport(session.Consequences,
            new QueuedHrReview(review.Incident, review.Report, review.SubmittedAt));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Trust.GetTrust(suspect), Is.EqualTo(expectedTrust));
        ScheduledEvent callback = context.Scheduler.PeekNext();
        Assert.That(callback.DueTime.TotalMilliseconds, Is.EqualTo(review.ReviewAt.TotalMilliseconds));
        context.Scheduler.AdvanceTo(review.ReviewAt);
        callback.Callback();
        callback.Callback();
        ReplayReport(session.Consequences, review);

        int expectedCredibility = verdict == HrVerdict.Rejected ? 55 : 80;
        Assert.That(review.Incident.Decision.Verdict, Is.EqualTo(verdict));
        Assert.That(session.Progress.Credibility, Is.EqualTo(expectedCredibility));
        Assert.That(session.Trust.GetTrust(suspect), Is.EqualTo(expectedTrust));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Consequences.History[0], Is.SameAs(consequence));
        Assert.That(session.Progress.History.Count, Is.EqualTo(1));
        Assert.That(session.Progress.History[0].CredibilityBefore, Is.EqualTo(80));
        Assert.That(session.Progress.History[0].CredibilityAfter, Is.EqualTo(expectedCredibility));
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
        Assert.That(session.Workload.History, Is.Empty);
        foreach (string id in session.Trust.EmployeeIds)
            if (id != suspect) Assert.That(session.Trust.GetTrust(id), Is.EqualTo(50));
    }

    [TestCase(5, 0, -10)]
    [TestCase(0, 0, -10)]
    [TestCase(50, 43, -7)]
    public void WrongfulBlame_RecordsActualClampAndConfiguredPenaltyOnce(
        int openingTrust, int expectedTrust, int penalty)
    {
        var context = Context(2, openingTrust,
            new ConsequenceRules(wrongfulBlameTrustDelta: penalty));
        var session = context.Session;
        context.Advance(840);
        Assert.That(session.TryDiscoverEvidence("E_SUPPLIES_RECEIPT"), Is.True);
        Assert.That(session.Submissions.TrySubmit("D0I3", "steve",
            new List<string> { "E_SUPPLIES_RECEIPT" }).IsValid, Is.True);
        var entry = session.Consequences.History[0].TrustChanges[0];
        Assert.That(entry.Before, Is.EqualTo(openingTrust));
        Assert.That(entry.After, Is.EqualTo(expectedTrust));
        Assert.That(entry.Delta, Is.EqualTo(expectedTrust - openingTrust));
        context.Advance(855);
        Assert.That(session.Progress.History[0].Decision.Verdict, Is.EqualTo(HrVerdict.Approved));
        Assert.That(session.Trust.GetTrust("steve"), Is.EqualTo(expectedTrust));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void InvalidCommandsHaveNoConsequences_DuplicateAcceptedReportCannotRepeatTrustLoss()
    {
        var context = Context(2);
        var session = context.Session;
        var receipt = new List<string> { "E_SUPPLIES_RECEIPT" };
        Assert.That(session.Submissions.TrySubmit("D0I3", "steve", receipt).IsValid, Is.False);
        context.Advance(840);
        Assert.That(session.Submissions.TrySubmit("missing", "steve", receipt).IsValid, Is.False);
        Assert.That(session.Submissions.TrySubmit("D0I3", "steve", receipt).IsValid, Is.False);
        Assert.That(session.Submissions.TrySubmit("D0I3", "outside_roster", new List<string>()).IsValid, Is.False);
        Assert.That(session.Submissions.TrySubmit("D0I3", "steve", new List<string> { "unknown_evidence" }).IsValid, Is.False);
        ReplayReport(session.Consequences, null);
        Assert.That(session.Progress.Credibility, Is.EqualTo(80));
        Assert.That(session.Trust.GetTrust("steve"), Is.EqualTo(50));
        Assert.That(session.Consequences.History, Is.Empty);
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
        Assert.That(session.Deliveries.GetDelivered("D0I3").Status, Is.EqualTo(IncidentStatus.Open));

        session.TryDiscoverEvidence("E_SUPPLIES_RECEIPT");
        Assert.That(session.Submissions.TrySubmit("D0I3", "steve", receipt).IsValid, Is.True);
        Assert.That(session.Submissions.TrySubmit("D0I3", "karen", new List<string>()).IsValid, Is.False);
        Assert.That(session.Trust.GetTrust("steve"), Is.EqualTo(40));
        Assert.That(session.Trust.GetTrust("karen"), Is.EqualTo(50));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(1));
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(1));
    }

    [Test]
    public void RequestAndReportWithSameInstanceId_HaveIndependentOnceOnlyConsequences()
    {
        var source = monday.Requests[0];
        var request = new DayRequestPlan("D0I3", source.Request, source.Profile,
            new GameTime(0, 840), new TimeRange(0, 850, 860));
        var context = Context(2, requests: new List<DayRequestPlan> { request });
        var session = context.Session;
        context.Advance(840);
        session.TryDiscoverEvidence("E_SUPPLIES_RECEIPT");
        Assert.That(session.Submissions.TrySubmit("D0I3", "steve",
            new List<string> { "E_SUPPLIES_RECEIPT" }).IsValid, Is.True);
        Assert.That(session.TryDelegate("D0I3", "dave").IsAccepted, Is.True);
        Assert.That(session.Progress.Credibility, Is.EqualTo(85));
        Assert.That(session.Trust.GetTrust("steve"), Is.EqualTo(40));
        Assert.That(session.Trust.GetTrust("alice"), Is.EqualTo(55));
        Assert.That(session.Trust.GetTrust("dave"), Is.EqualTo(55));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(2));
        Assert.That(session.Consequences.History[0].Kind, Is.EqualTo(ConsequenceKind.ReportAccepted));
        Assert.That(session.Consequences.History[1].Kind, Is.EqualTo(ConsequenceKind.RequestResolved));
        Assert.That(session.Workload.GetAttemptedCount("dave"), Is.EqualTo(1));
    }
}
