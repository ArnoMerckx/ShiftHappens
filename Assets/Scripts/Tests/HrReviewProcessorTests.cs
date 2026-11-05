using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class HrReviewProcessorTests
{
    private HrReviewTestContext context;

    [SetUp]
    public void SetUp()
    {
        context = new HrReviewTestContext();
    }

    private QueuedHrReview SubmitPrinter(int minute = 675)
    {
        return context.Submit("D0I2", "dave",
            new List<string> { "E_DAVE_PRINTER" }, new GameTime(0, minute));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Constructor_RejectsNullDependency(int input)
    {
        Assert.Throws<ArgumentNullException>(() => new HrReviewProcessor(
            input == 0 ? null : context.Submissions,
            input == 1 ? null : context.Scheduler,
            input == 2 ? null : context.Day.ScheduleData.HrKnowledge,
            input == 3 ? null : context.Day.EvidenceDatabase));
    }

    [Test]
    public void Review_AppliesAtExactDueInstantAndNotifiesAfterQueueRemoval()
    {
        var processor = context.AttachProcessor();
        QueuedHrReview job = context.Submit("D0I2", "dave",
            new List<string> { "E_DAVE_PRINTER" }, new GameTime(0, 680, 30, 250));
        int completions = 0;
        processor.ReviewCompleted += (review, decision) =>
        {
            completions++;
            Assert.That(review, Is.SameAs(job));
            Assert.That(decision, Is.SameAs(job.Incident.Decision));
            Assert.That(decision.Report, Is.SameAs(job.Report));
            Assert.That(context.Submissions.PendingReviews, Is.Empty);
            Assert.That(context.Clock.Now.TotalMilliseconds,
                Is.EqualTo(job.ReviewAt.TotalMilliseconds));
        };

        context.Scheduler.AdvanceTo(new GameTime(job.ReviewAt.TotalMilliseconds - 1));
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(job.Incident.Decision, Is.Null);
        Assert.That(completions, Is.Zero);

        context.Scheduler.AdvanceTo(job.ReviewAt);
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(completions, Is.EqualTo(1));
        context.Scheduler.AdvanceTo(new GameTime(0, 720));
        Assert.That(completions, Is.EqualTo(1));
    }

    [TestCase("D0I1", "bob", "E_SCHEDULE", 590, HrVerdict.Approved)]
    [TestCase("D0I1", "alice", "E_SCHEDULE", 590, HrVerdict.Rejected)]
    [TestCase("D0I2", "dave", "E_DAVE_PRINTER", 675, HrVerdict.Approved)]
    [TestCase("D0I2", "bob", "E_SCHEDULE", 675, HrVerdict.Rejected)]
    [TestCase("D0I2", "dave", "E_OFFICE_SOCIAL", 675, HrVerdict.Rejected)]
    [TestCase("D0I3", "karen", "E_KAREN_COLLECTION", 840, HrVerdict.Approved)]
    [TestCase("D0I3", "steve", "E_SUPPLIES_RECEIPT", 840, HrVerdict.Approved)]
    [TestCase("D0I3", "steve", "E_SCHEDULE", 840, HrVerdict.Rejected)]
    public void DelayedReview_PreservesAgreedCaseOutcomes(
        string instanceId, string suspect, string document, int minute, HrVerdict expected)
    {
        context.AttachProcessor();
        QueuedHrReview job = context.Submit(instanceId, suspect,
            new List<string> { document }, new GameTime(0, minute));
        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(job.Incident.Decision.Verdict, Is.EqualTo(expected));
        Assert.That(job.Incident.Decision.Report, Is.SameAs(job.Report));
        Assert.That(job.Incident.Decision.Summary, Is.Not.Empty);
        Assert.That(context.Submissions.PendingReviews, Is.Empty);
    }

    [Test]
    public void AcceptedAtDeadline_ReviewsAfterDeadlineWithoutResubmissionValidation()
    {
        context.AttachProcessor();
        QueuedHrReview job = SubmitPrinter(720);
        Assert.That(job.ReviewAt.MinuteOfDay, Is.EqualTo(735));

        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(job.Incident.Decision.Verdict, Is.EqualTo(HrVerdict.Approved));
        Assert.That(job.Incident.SubmittedReport, Is.SameAs(job.Report));
    }

    [Test]
    public void Constructor_SchedulesPreviouslyQueuedFutureReview()
    {
        QueuedHrReview job = SubmitPrinter();
        context.Scheduler.AdvanceTo(new GameTime(0, 680));
        context.AttachProcessor();
        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(context.Submissions.PendingReviews, Is.Empty);
    }

    [Test]
    public void Constructor_CanAttachExactlyAtReviewTime()
    {
        QueuedHrReview job = SubmitPrinter();
        context.Scheduler.AdvanceTo(job.ReviewAt);
        context.AttachProcessor();
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Pending));

        context.Scheduler.AdvanceTo(context.Clock.Now);

        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Approved));
    }

    [Test]
    public void LateAttachment_FailsBeforeSchedulingOrSubscribing()
    {
        QueuedHrReview job = SubmitPrinter();
        context.Scheduler.AdvanceTo(new GameTime(job.ReviewAt.TotalMilliseconds + 1));
        int pendingBefore = context.Scheduler.PendingCount;

        Assert.Throws<InvalidOperationException>(() => context.AttachProcessor());
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(pendingBefore));
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Pending));

        context.Submit("D0I3", "steve", new List<string>(), new GameTime(0, 840));
        Assert.That(context.Scheduler.PendingCount, Is.Zero);
        Assert.That(context.Submissions.PendingReviews.Count, Is.EqualTo(2));
    }

    [Test]
    public void EarlyCallbackAndCompletedCallbackReplay_HaveNoEffects()
    {
        var processor = context.AttachProcessor();
        QueuedHrReview job = SubmitPrinter();
        ScheduledEvent scheduled = context.Scheduler.PeekNext();
        Assert.That(scheduled.DueTime.TotalMilliseconds,
            Is.EqualTo(job.ReviewAt.TotalMilliseconds));
        int completions = 0;
        processor.ReviewCompleted += (review, decision) => completions++;

        scheduled.Callback();
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(completions, Is.Zero);
        Assert.That(context.Submissions.PendingReviews.Count, Is.EqualTo(1));

        context.Scheduler.AdvanceTo(job.ReviewAt);
        HrEvaluationResult original = job.Incident.Decision;
        scheduled.Callback();
        scheduled.Callback();

        Assert.That(completions, Is.EqualTo(1));
        Assert.That(job.Incident.Decision, Is.SameAs(original));
        Assert.That(context.Submissions.PendingReviews, Is.Empty);
    }

    [Test]
    public void AlreadyFinalizedIncident_DiscardsStaleJobWithoutCompletionSignal()
    {
        var processor = context.AttachProcessor();
        QueuedHrReview job = SubmitPrinter();
        var decision = new HrEvaluator().Evaluate(new EvaluationContext(
            job.Incident, context.Day.ScheduleData.HrKnowledge, context.Day.EvidenceDatabase));
        Assert.That(job.Incident.TryApplyDecision(decision), Is.True);
        int completions = 0;
        processor.ReviewCompleted += (review, result) => completions++;

        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(context.Submissions.PendingReviews, Is.Empty);
        Assert.That(job.Incident.Decision, Is.SameAs(decision));
        Assert.That(completions, Is.Zero);
    }

    [Test]
    public void GamePause_DefersReviewUntilResumed()
    {
        context.AttachProcessor();
        QueuedHrReview job = SubmitPrinter();
        context.Clock.SetPaused(true);
        context.Scheduler.AdvanceTo(job.ReviewAt);
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Pending));

        context.Clock.SetPaused(false);
        context.Scheduler.AdvanceTo(job.ReviewAt);
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Approved));
    }

    [Test]
    public void Review_UsesHrPriorityBetweenDeliveryAndTimerExpiry()
    {
        var processor = context.AttachProcessor();
        QueuedHrReview job = SubmitPrinter();
        var order = new List<string>();
        processor.ReviewCompleted += (review, result) => order.Add("review");
        context.Scheduler.Schedule(new ScheduledEvent("test_timer", job.ReviewAt,
            ScheduledEventPriority.TimerExpiry, () => order.Add("timer")));
        context.Scheduler.Schedule(new ScheduledEvent("test_delivery", job.ReviewAt,
            ScheduledEventPriority.Delivery, () => order.Add("delivery")));

        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(order, Is.EqualTo(new[] { "delivery", "review", "timer" }));
    }

    [Test]
    public void TwoReviewsDueTogether_AreBothProcessedInRegistrationOrder()
    {
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
        Assert.That(definition, Is.Not.Null);
        var plans = new List<DayIncidentPlan>
        {
            new DayIncidentPlan(definition, new IncidentScheduleEntry(
                "first", "I2", new TimeRange(0, 635, 640), 675, 720)),
            new DayIncidentPlan(definition, new IncidentScheduleEntry(
                "second", "I2", new TimeRange(0, 635, 640), 675, 720))
        };
        context = new HrReviewTestContext(new DayData(context.Day.ScheduleData,
            new List<EvidenceDefinition>(context.Day.EvidenceDatabase.All), plans));
        var processor = context.AttachProcessor();
        var completed = new List<string>();
        processor.ReviewCompleted += (review, result) => completed.Add(review.Report.IncidentInstanceId);
        QueuedHrReview first = context.Submit("first", "dave",
            new List<string> { "E_DAVE_PRINTER" }, new GameTime(0, 675));
        QueuedHrReview second = context.Submit("second", "bob",
            new List<string> { "E_SCHEDULE" }, new GameTime(0, 675));

        context.Scheduler.AdvanceTo(first.ReviewAt);

        Assert.That(completed, Is.EqualTo(new[] { "first", "second" }));
        Assert.That(first.Incident.Decision.Verdict, Is.EqualTo(HrVerdict.Approved));
        Assert.That(second.Incident.Decision.Verdict, Is.EqualTo(HrVerdict.Rejected));
        Assert.That(context.Submissions.PendingReviews, Is.Empty);
    }

    [Test]
    public void EvaluationFailure_RetainsJobAndDoesNotApplyDecisionOrNotify()
    {
        var processor = context.AttachProcessor(
            database: new EvidenceDatabase(new List<EvidenceDefinition>()));
        QueuedHrReview job = SubmitPrinter();
        int completions = 0;
        processor.ReviewCompleted += (review, decision) => completions++;

        Assert.Throws<KeyNotFoundException>(() => context.Scheduler.AdvanceTo(job.ReviewAt));

        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(job.Incident.Decision, Is.Null);
        Assert.That(context.Submissions.PendingReviews[0], Is.SameAs(job));
        Assert.That(completions, Is.Zero);
        context.Scheduler.AdvanceTo(new GameTime(job.ReviewAt.TotalMilliseconds + 1));
        Assert.That(completions, Is.Zero);
    }

    [Test]
    public void Evaluation_UsesExplicitHrKnowledgeRatherThanAllWorldRecords()
    {
        context.AttachProcessor(new HrKnowledge(
            new List<ApprovedScheduleRecord>(), new List<Claim>()));
        QueuedHrReview job = context.Submit("D0I2", "bob",
            new List<string> { "E_SCHEDULE" }, new GameTime(0, 675));

        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(context.Day.ScheduleData.WorldApprovedRecords.Count, Is.EqualTo(4));
        Assert.That(job.Incident.Decision.Verdict, Is.EqualTo(HrVerdict.Approved));
    }
}
