using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ReportSubmissionServiceTests
{
    private DayData day;
    private GameClock clock;
    private EventScheduler scheduler;
    private DayDeliveryService deliveries;
    private EvidenceService evidence;
    private ReportSubmissionValidator validator;
    private ReportSubmissionService submissions;
    private List<QueuedHrReview> notifications;
    private readonly List<UnityEngine.Object> temporaryObjects =
        new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        var definition = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
        Assert.That(definition, Is.Not.Null);
        UseDay(definition.GetData());
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var temporary in temporaryObjects)
        {
            if (temporary != null)
                UnityEngine.Object.DestroyImmediate(temporary);
        }
        temporaryObjects.Clear();
    }

    private void UseDay(DayData data)
    {
        day = data;
        clock = new GameClock(data.ScheduleData.Settings.OpensAt, 20);
        scheduler = new EventScheduler(clock);
        deliveries = new DayDeliveryService(data, scheduler);
        evidence = new EvidenceService(data.EvidenceDatabase);
        validator = new ReportSubmissionValidator(
            new List<string>(data.ScheduleData.EmployeeIds), evidence);
        submissions = new ReportSubmissionService(scheduler, deliveries, validator);
        notifications = new List<QueuedHrReview>();
        submissions.ReviewQueued += notifications.Add;
    }

    private void Advance(int minute, int second = 0, int millisecond = 0)
    {
        scheduler.AdvanceTo(new GameTime(0, minute, second, millisecond));
    }

    private void Discover(string evidenceId)
    {
        Assert.That(evidence.MarkDiscovered(
            evidenceId, clock.Now.DayIndex, clock.Now.MinuteOfDay), Is.True);
    }

    private void AssertUnsubmitted(string instanceId = "D0I2")
    {
        var incident = deliveries.GetDelivered(instanceId);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
        Assert.That(incident.Decision, Is.Null);
        Assert.That(submissions.PendingReviews, Is.Empty);
        Assert.That(notifications, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Constructor_RejectsNullDependency(int input)
    {
        Assert.Throws<ArgumentNullException>(() => new ReportSubmissionService(
            input == 0 ? null : scheduler,
            input == 1 ? null : deliveries,
            input == 2 ? null : validator));
    }

    [TestCase("D0I2")]
    [TestCase("unknown")]
    [TestCase("d0i2")]
    public void MissingOrUndeliveredIncident_ReturnsFailureWithoutQueueing(string id)
    {
        var result = submissions.TrySubmit(id, "dave", new List<string>());
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Reason, Is.Not.Empty);
        Assert.That(deliveries.DeliveredIncidents, Is.Empty);
        Assert.That(submissions.PendingReviews, Is.Empty);
        Assert.That(notifications, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void BlankInstanceId_IsRejected(string id)
    {
        Assert.Throws<ArgumentException>(
            () => submissions.TrySubmit(id, "dave", new List<string>()));
        Assert.That(submissions.PendingReviews, Is.Empty);
    }

    [Test]
    public void AcceptedSubmission_UsesPreciseClockAndPublishesStoredWork()
    {
        Advance(680, 30, 250);
        Discover("E_DAVE_PRINTER");
        var selected = new List<string> { "E_DAVE_PRINTER" };
        bool sawConsistentState = false;

        submissions.ReviewQueued += review =>
        {
            sawConsistentState = true;
            Assert.That(review.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
            Assert.That(review.Report, Is.SameAs(review.Incident.SubmittedReport));
            Assert.That(submissions.PendingReviews[0], Is.SameAs(review));
        };

        var result = submissions.TrySubmit("D0I2", "dave", selected);
        selected.Clear();

        Assert.That(result.IsValid, Is.True);
        Assert.That(sawConsistentState, Is.True);
        Assert.That(notifications.Count, Is.EqualTo(1));
        Assert.That(submissions.PendingReviews.Count, Is.EqualTo(1));
        QueuedHrReview job = submissions.PendingReviews[0];
        Assert.That(notifications[0], Is.SameAs(job));
        Assert.That(job.Incident, Is.SameAs(deliveries.GetDelivered("D0I2")));
        Assert.That(job.Report, Is.SameAs(job.Incident.SubmittedReport));
        Assert.That(job.Report.SubmittedDayIndex, Is.Zero);
        Assert.That(job.Report.SubmittedMinute, Is.EqualTo(680));
        Assert.That(job.Report.EvidenceIds, Is.EqualTo(new[] { "E_DAVE_PRINTER" }));
        Assert.That(job.SubmittedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 680, 30, 250).TotalMilliseconds));
        Assert.That(job.ReviewAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 695, 30, 250).TotalMilliseconds));
        Assert.That(job.Incident.Decision, Is.Null);
    }

    [TestCase("D0I1", "bob", "E_SCHEDULE", 630, -1, true)]
    [TestCase("D0I1", "bob", "E_SCHEDULE", 630, 0, true)]
    [TestCase("D0I1", "bob", "E_SCHEDULE", 630, 1, false)]
    [TestCase("D0I2", "dave", "E_DAVE_PRINTER", 720, -1, true)]
    [TestCase("D0I2", "dave", "E_DAVE_PRINTER", 720, 0, true)]
    [TestCase("D0I2", "dave", "E_DAVE_PRINTER", 720, 1, false)]
    [TestCase("D0I3", "steve", "E_SUPPLIES_RECEIPT", 900, -1, true)]
    [TestCase("D0I3", "steve", "E_SUPPLIES_RECEIPT", 900, 0, true)]
    [TestCase("D0I3", "steve", "E_SUPPLIES_RECEIPT", 900, 1, false)]
    public void Deadline_IsInclusiveToExactMillisecond(
        string id, string suspect, string document, int deadline,
        int offset, bool expected)
    {
        Discover(document);
        var now = new GameTime(new GameTime(0, deadline).TotalMilliseconds + offset);
        scheduler.AdvanceTo(now);

        var result = submissions.TrySubmit(id, suspect, new List<string> { document });
        Assert.That(result.IsValid, Is.EqualTo(expected));

        if (expected)
        {
            Assert.That(submissions.PendingReviews.Count, Is.EqualTo(1));
            Assert.That(submissions.PendingReviews[0].SubmittedAt.TotalMilliseconds,
                Is.EqualTo(now.TotalMilliseconds));
            Assert.That(notifications.Count, Is.EqualTo(1));
        }
        else
        {
            Assert.That(now.MinuteOfDay, Is.EqualTo(deadline));
            AssertUnsubmitted(id);
        }
    }

    [Test]
    public void UnknownSuspect_ReturnsFailureWithoutChangingIncident()
    {
        Advance(675);
        var result = submissions.TrySubmit("D0I2", "unknown", new List<string>());
        Assert.That(result.IsValid, Is.False);
        AssertUnsubmitted();
    }

    [Test]
    public void UnknownEvidence_ReturnsFailureWithoutChangingIncident()
    {
        Advance(675);
        var result = submissions.TrySubmit("D0I2", "dave",
            new List<string> { "unknown" });
        Assert.That(result.IsValid, Is.False);
        AssertUnsubmitted();
    }

    [Test]
    public void Receipt_RequiresDiscoveryButNotReading()
    {
        Advance(840);
        var selected = new List<string> { "E_SUPPLIES_RECEIPT" };
        Assert.That(submissions.TrySubmit("D0I3", "steve", selected).IsValid, Is.False);
        AssertUnsubmitted("D0I3");

        Discover("E_SUPPLIES_RECEIPT");
        Assert.That(evidence.IsRead("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(submissions.TrySubmit("D0I3", "steve", selected).IsValid, Is.True);
        Assert.That(submissions.PendingReviews.Count, Is.EqualTo(1));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void BlankSuspect_IsRejectedWithoutQueueing(string suspect)
    {
        Advance(675);
        Assert.Throws<ArgumentException>(() => submissions.TrySubmit(
            "D0I2", suspect, new List<string>()));
        AssertUnsubmitted();
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void MalformedEvidenceSelection_IsRejectedWithoutQueueing(int problem)
    {
        Advance(675);
        var selected = new List<string>();
        if (problem == 0) selected = null;
        if (problem == 1) selected.Add(" ");
        if (problem == 2) selected.AddRange(new[] { "E_SCHEDULE", "E_SCHEDULE" });
        if (problem == 3) selected.Add(null);

        Assert.Catch<ArgumentException>(() => submissions.TrySubmit("D0I2", "dave", selected));
        AssertUnsubmitted();
    }

    [TestCase(675)]
    [TestCase(720)]
    [TestCase(721)]
    public void RepeatedSubmission_PreservesFirstReportAndSingleQueueEntry(int retryMinute)
    {
        Advance(675);
        Discover("E_DAVE_PRINTER");
        Discover("E_SCHEDULE");
        Assert.That(submissions.TrySubmit("D0I2", "dave",
            new List<string> { "E_DAVE_PRINTER" }).IsValid, Is.True);
        QueuedHrReview original = submissions.PendingReviews[0];

        Advance(retryMinute);
        var retry = submissions.TrySubmit("D0I2", "bob",
            new List<string> { "E_SCHEDULE" });

        Assert.That(retry.IsValid, Is.False);
        Assert.That(submissions.PendingReviews.Count, Is.EqualTo(1));
        Assert.That(submissions.PendingReviews[0], Is.SameAs(original));
        Assert.That(deliveries.GetDelivered("D0I2").SubmittedReport,
            Is.SameAs(original.Report));
        Assert.That(notifications.Count, Is.EqualTo(1));
    }

    [Test]
    public void PendingReviewView_IsReadOnlyAndReflectsAcceptedWork()
    {
        IReadOnlyList<QueuedHrReview> view = submissions.PendingReviews;
        Assert.That(view, Is.Empty);
        Advance(675);
        Assert.That(submissions.TrySubmit("D0I2", "dave", new List<string>()).IsValid, Is.True);

        Assert.That(view.Count, Is.EqualTo(1));
        Assert.That(submissions.PendingReviews, Is.SameAs(view));
        Assert.Throws<NotSupportedException>(() => ((IList<QueuedHrReview>)view).Clear());
    }

    [Test]
    public void DifferentIncidents_QueueIndependentReportsAndReviewTimes()
    {
        Advance(590);
        Assert.That(submissions.TrySubmit("D0I1", "bob", new List<string>()).IsValid, Is.True);
        Advance(675);
        Assert.That(submissions.TrySubmit("D0I2", "dave", new List<string>()).IsValid, Is.True);
        Advance(840);
        Assert.That(submissions.TrySubmit("D0I3", "steve", new List<string>()).IsValid, Is.True);

        Assert.That(submissions.PendingReviews.Count, Is.EqualTo(3));
        Assert.That(notifications.Count, Is.EqualTo(3));
        var ids = new List<string>();
        var dueTimes = new List<long>();
        foreach (var review in submissions.PendingReviews)
        {
            ids.Add(review.Report.IncidentInstanceId);
            dueTimes.Add(review.ReviewAt.TotalMilliseconds);
            Assert.That(review.Report, Is.SameAs(review.Incident.SubmittedReport));
        }
        Assert.That(ids, Is.EqualTo(new[] { "D0I1", "D0I2", "D0I3" }));
        Assert.That(dueTimes, Is.EqualTo(new[]
        {
            new GameTime(0, 600).TotalMilliseconds,
            new GameTime(0, 690).TotalMilliseconds,
            new GameTime(0, 855).TotalMilliseconds
        }));
    }

    [Test]
    public void QueueNotificationException_DoesNotEraseAcceptedWork()
    {
        Advance(675);
        var failure = new InvalidOperationException("Test notification failure.");
        submissions.ReviewQueued += review => { throw failure; };

        var caught = Assert.Throws<InvalidOperationException>(
            () => submissions.TrySubmit("D0I2", "dave", new List<string>()));

        Assert.That(caught, Is.SameAs(failure));
        Assert.That(submissions.PendingReviews.Count, Is.EqualTo(1));
        QueuedHrReview accepted = submissions.PendingReviews[0];
        Assert.That(accepted.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(accepted.Report, Is.SameAs(accepted.Incident.SubmittedReport));
        Assert.That(submissions.TrySubmit("D0I2", "bob", new List<string>()).IsValid, Is.False);
        Assert.That(notifications.Count, Is.EqualTo(1));
    }

    [Test]
    public void UnrepresentableReviewTime_IsRejectedBeforePendingTransition()
    {
        var original = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I1_CoffeeSpill.asset");
        Assert.That(original, Is.Not.Null);
        var definition = UnityEngine.Object.Instantiate(original);
        temporaryObjects.Add(definition);
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("reviewDelayMinutes").intValue = int.MaxValue;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var settings = new WorkdaySettings("last", "Last day", int.MaxValue, 540, 1020, 20);
        var schedule = new DayScheduleData(settings, new List<string> { "bob" },
            new List<ApprovedScheduleRecord>(), new List<ApprovedScheduleRecord>(),
            new List<Claim>());
        var plan = new DayIncidentPlan(definition, new IncidentScheduleEntry(
            "last_incident", "I1", new TimeRange(int.MaxValue, 575, 580), 590, 630), "bob");
        UseDay(new DayData(schedule, new List<EvidenceDefinition>(),
            new List<DayIncidentPlan> { plan }));
        scheduler.AdvanceTo(new GameTime(int.MaxValue, 590));

        Assert.Throws<ArgumentOutOfRangeException>(() => submissions.TrySubmit(
            "last_incident", "bob", new List<string>()));
        AssertUnsubmitted("last_incident");
    }

    [Test]
    public void FutureEvidence_CannotAttachUntilItBecomesAvailableAndDiscovered()
    {
        var document = ScriptableObject.CreateInstance<EvidenceDefinition>();
        temporaryObjects.Add(document);
        var serialized = new SerializedObject(document);
        serialized.FindProperty("evidenceId").stringValue = "future_document";
        serialized.FindProperty("displayName").stringValue = "Future document";
        serialized.FindProperty("bodyText").stringValue = "Future content.";
        serialized.FindProperty("source").intValue = (int)EvidenceSource.InWatch;
        serialized.FindProperty("availableMinute").intValue = 700;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var documents = new List<EvidenceDefinition>(day.EvidenceDatabase.All);
        documents.Add(document);
        UseDay(new DayData(day.ScheduleData, documents,
            new List<DayIncidentPlan>(day.Incidents)));
        Advance(675);
        var selected = new List<string> { "future_document" };

        Assert.That(evidence.MarkDiscovered("future_document", 0, 675), Is.False);
        Assert.That(submissions.TrySubmit("D0I2", "dave", selected).IsValid, Is.False);
        AssertUnsubmitted();

        Advance(700);
        Assert.That(submissions.TrySubmit("D0I2", "dave", selected).IsValid, Is.False);
        AssertUnsubmitted();
        Discover("future_document");
        Assert.That(submissions.TrySubmit("D0I2", "dave", selected).IsValid, Is.True);
        Assert.That(submissions.PendingReviews.Count, Is.EqualTo(1));
    }
}
