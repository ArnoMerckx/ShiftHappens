using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ReviewHistoryEntryTests
{
    private HrReviewTestContext context;
    private QueuedHrReview job;
    private HrEvaluationResult decision;

    [SetUp]
    public void SetUp()
    {
        context = new HrReviewTestContext();
        job = context.Submit("D0I2", "dave", new List<string>(),
            new GameTime(0, 675, 30, 250));
        context.Scheduler.AdvanceTo(job.ReviewAt);
        decision = new HrEvaluator().Evaluate(new EvaluationContext(
            job.Incident, context.Day.ScheduleData.HrKnowledge, context.Day.EvidenceDatabase));
        Assert.That(job.Incident.TryApplyDecision(decision), Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Constructor_RejectsNullReference(bool missingReview)
    {
        Assert.Throws<ArgumentNullException>(() => new ReviewHistoryEntry(
            missingReview ? null : job,
            missingReview ? decision : null,
            context.Clock.Now, 100, 75));
    }

    [Test]
    public void EquivalentButDifferentReportIdentity_IsRejected()
    {
        var replacement = new IncidentReport(
            job.Report.IncidentInstanceId, job.Report.SuspectId,
            new List<string>(job.Report.EvidenceIds),
            job.Report.SubmittedDayIndex, job.Report.SubmittedMinute);
        var wrongDecision = new HrEvaluationResult(replacement, new List<HrRuleResult>());

        Assert.Throws<ArgumentException>(() => new ReviewHistoryEntry(
            job, wrongDecision, context.Clock.Now, 100, 75));
    }

    [Test]
    public void CompletionBeforeDueTime_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ReviewHistoryEntry(
            job, decision, new GameTime(job.ReviewAt.TotalMilliseconds - 1), 100, 75));
    }

    [TestCase(-1, 0)]
    [TestCase(100, -1)]
    [TestCase(100, 101)]
    public void InvalidScoreSnapshots_AreRejected(int before, int after)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReviewHistoryEntry(
            job, decision, context.Clock.Now, before, after));
    }

    [Test]
    public void Entry_RetainsImmutableReferencesAndActualCompletionTimestamp()
    {
        context.Scheduler.AdvanceTo(new GameTime(job.ReviewAt.TotalMilliseconds + 5000));
        var entry = new ReviewHistoryEntry(job, decision, context.Clock.Now, 100, 75);

        Assert.That(entry.Report, Is.SameAs(job.Report));
        Assert.That(entry.Decision, Is.SameAs(decision));
        Assert.That(entry.SubmittedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 675, 30, 250).TotalMilliseconds));
        Assert.That(entry.ReviewedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 690, 35, 250).TotalMilliseconds));
        Assert.That(entry.CredibilityBefore, Is.EqualTo(100));
        Assert.That(entry.CredibilityAfter, Is.EqualTo(75));
    }
}
