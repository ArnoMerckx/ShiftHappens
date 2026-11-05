using System;
using System.Collections.Generic;
using NUnit.Framework;

public class WorkdayProgressTests
{
    private HrReviewTestContext context;

    [SetUp]
    public void SetUp()
    {
        context = new HrReviewTestContext();
        context.AttachProcessor();
    }

    private WorkdayProgress Progress(CredibilityRules rules = null)
    {
        return new WorkdayProgress(
            rules ?? new CredibilityRules(), context.Processor, context.Scheduler);
    }

    private QueuedHrReview SubmitPrinter(string document = "E_OFFICE_SOCIAL")
    {
        return context.Submit("D0I2", "dave", new List<string> { document },
            new GameTime(0, 675));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Constructor_RejectsNullDependency(int input)
    {
        Assert.Throws<ArgumentNullException>(() => new WorkdayProgress(
            input == 0 ? null : new CredibilityRules(),
            input == 1 ? null : context.Processor,
            input == 2 ? null : context.Scheduler));
    }

    [Test]
    public void NewSession_StartsWithChosenCredibilityAndEmptyHistory()
    {
        WorkdayProgress progress = Progress();
        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.History, Is.Empty);
        Assert.That(progress.HasReachedFiringThreshold, Is.False);
    }

    [TestCase("E_DAVE_PRINTER", HrVerdict.Approved, 100)]
    [TestCase("E_OFFICE_SOCIAL", HrVerdict.Rejected, 75)]
    public void Review_RecordsOutcomeAndAppliesOnlyRejectionPenalty(
        string document, HrVerdict verdict, int expectedCredibility)
    {
        WorkdayProgress progress = Progress();
        QueuedHrReview job = SubmitPrinter(document);
        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.History, Is.Empty);

        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(progress.Credibility, Is.EqualTo(expectedCredibility));
        Assert.That(progress.History.Count, Is.EqualTo(1));
        Assert.That(progress.History[0].Decision.Verdict, Is.EqualTo(verdict));
        Assert.That(progress.History[0].CredibilityBefore, Is.EqualTo(100));
        Assert.That(progress.History[0].CredibilityAfter, Is.EqualTo(expectedCredibility));
    }

    [Test]
    public void ChangedNotification_SeesCommittedHistoryScoreAndDecision()
    {
        WorkdayProgress progress = Progress();
        QueuedHrReview job = SubmitPrinter();
        int changes = 0;
        progress.Changed += () =>
        {
            changes++;
            Assert.That(progress.Credibility, Is.EqualTo(75));
            Assert.That(progress.History.Count, Is.EqualTo(1));
            Assert.That(context.Submissions.PendingReviews, Is.Empty);
            Assert.That(progress.History[0].Report, Is.SameAs(job.Report));
            Assert.That(progress.History[0].Decision, Is.SameAs(job.Incident.Decision));
        };

        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void History_PreservesReportTimeAndScoreSnapshotsAfterLaterReviews()
    {
        WorkdayProgress progress = Progress();
        var selected = new List<string> { "E_OFFICE_SOCIAL" };
        QueuedHrReview job = context.Submit("D0I2", "dave", selected,
            new GameTime(0, 675, 20, 250));
        selected.Clear();
        context.Scheduler.AdvanceTo(job.ReviewAt);
        ReviewHistoryEntry entry = progress.History[0];

        Assert.That(entry.Report, Is.SameAs(job.Report));
        Assert.That(entry.Report.EvidenceIds, Is.EqualTo(new[] { "E_OFFICE_SOCIAL" }));
        Assert.That(entry.Report.SuspectId, Is.EqualTo("dave"));
        Assert.That(entry.Decision, Is.SameAs(job.Incident.Decision));
        Assert.That(entry.SubmittedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 675, 20, 250).TotalMilliseconds));
        Assert.That(entry.ReviewedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 690, 20, 250).TotalMilliseconds));

        QueuedHrReview later = context.Submit("D0I3", "bob",
            new List<string> { "E_OFFICE_SOCIAL" }, new GameTime(0, 840));
        context.Scheduler.AdvanceTo(later.ReviewAt);

        Assert.That(progress.Credibility, Is.EqualTo(50));
        Assert.That(progress.History.Count, Is.EqualTo(2));
        Assert.That(progress.History[0], Is.SameAs(entry));
        Assert.That(entry.CredibilityBefore, Is.EqualTo(100));
        Assert.That(entry.CredibilityAfter, Is.EqualTo(75));
        Assert.That(entry.ReviewedAt.TotalMilliseconds,
            Is.EqualTo(job.ReviewAt.TotalMilliseconds));
    }

    [Test]
    public void CompletedCallbackReplay_DoesNotRepeatPenaltyHistoryOrNotification()
    {
        WorkdayProgress progress = Progress();
        QueuedHrReview job = SubmitPrinter();
        ScheduledEvent scheduled = context.Scheduler.PeekNext();
        Assert.That(scheduled.DueTime.TotalMilliseconds, Is.EqualTo(job.ReviewAt.TotalMilliseconds));
        int changes = 0;
        progress.Changed += () => changes++;

        context.Scheduler.AdvanceTo(job.ReviewAt);
        scheduled.Callback();
        scheduled.Callback();

        Assert.That(progress.Credibility, Is.EqualTo(75));
        Assert.That(progress.History.Count, Is.EqualTo(1));
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void SameEmployeeAcrossIncidents_IsRecordedWithoutExtraRepeatedBlamePenalty()
    {
        WorkdayProgress progress = Progress();
        string[] ids = { "D0I1", "D0I2", "D0I3" };
        int[] deliveryMinutes = { 590, 675, 840 };

        for (int i = 0; i < ids.Length; i++)
        {
            QueuedHrReview job = context.Submit(ids[i], "bob",
                new List<string> { "E_OFFICE_SOCIAL" }, new GameTime(0, deliveryMinutes[i]));
            context.Scheduler.AdvanceTo(job.ReviewAt);
        }

        Assert.That(progress.Credibility, Is.EqualTo(25));
        Assert.That(progress.History.Count, Is.EqualTo(3));
        Assert.That(progress.HasReachedFiringThreshold, Is.False);
        for (int i = 0; i < ids.Length; i++)
        {
            ReviewHistoryEntry entry = progress.History[i];
            Assert.That(entry.Report.IncidentInstanceId, Is.EqualTo(ids[i]));
            Assert.That(entry.Report.SuspectId, Is.EqualTo("bob"));
            Assert.That(entry.CredibilityBefore - entry.CredibilityAfter, Is.EqualTo(25));
        }
    }

    [Test]
    public void HistoryView_IsReadOnlyAndReflectsCompletedReviews()
    {
        WorkdayProgress progress = Progress();
        IReadOnlyList<ReviewHistoryEntry> view = progress.History;
        QueuedHrReview job = SubmitPrinter();
        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(view.Count, Is.EqualTo(1));
        Assert.That(progress.History, Is.SameAs(view));
        Assert.Throws<NotSupportedException>(() => ((IList<ReviewHistoryEntry>)view).Clear());
    }

    [TestCase(10, 25, 0, 0, true)]
    [TestCase(100, 100, 0, 0, true)]
    [TestCase(100, 25, 75, 75, true)]
    [TestCase(100, 25, 80, 75, true)]
    [TestCase(100, 25, 74, 75, false)]
    public void Credibility_ClampsAtZeroAndChecksInclusiveFiringThreshold(
        int start, int penalty, int threshold, int expected, bool reached)
    {
        WorkdayProgress progress = Progress(new CredibilityRules(
            start, penalty, 35, threshold));
        Assert.That(progress.HasReachedFiringThreshold, Is.False);
        QueuedHrReview job = SubmitPrinter();
        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(progress.Credibility, Is.EqualTo(expected));
        Assert.That(progress.HasReachedFiringThreshold, Is.EqualTo(reached));
        Assert.That(progress.History[0].CredibilityAfter, Is.EqualTo(expected));
    }

    [Test]
    public void ZeroRejectionPenalty_StillRecordsAndNotifiesOutcome()
    {
        WorkdayProgress progress = Progress(new CredibilityRules(rejectedReportPenalty: 0));
        int changes = 0;
        progress.Changed += () => changes++;
        QueuedHrReview job = SubmitPrinter();
        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.History.Count, Is.EqualTo(1));
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void SeparateSessions_DoNotShareCredibilityOrHistory()
    {
        WorkdayProgress first = Progress();
        var secondContext = new HrReviewTestContext();
        secondContext.AttachProcessor();
        var second = new WorkdayProgress(new CredibilityRules(),
            secondContext.Processor, secondContext.Scheduler);
        QueuedHrReview job = SubmitPrinter();
        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(first.Credibility, Is.EqualTo(75));
        Assert.That(first.History.Count, Is.EqualTo(1));
        Assert.That(second.Credibility, Is.EqualTo(100));
        Assert.That(second.History, Is.Empty);
    }

    [Test]
    public void ChangedSubscriberException_DoesNotRollBackOrRepeatCommittedEffects()
    {
        WorkdayProgress progress = Progress();
        QueuedHrReview job = SubmitPrinter();
        ScheduledEvent scheduled = context.Scheduler.PeekNext();
        var failure = new InvalidOperationException("Test progress notification failure.");
        int changes = 0;
        progress.Changed += () =>
        {
            changes++;
            throw failure;
        };

        var caught = Assert.Throws<InvalidOperationException>(
            () => context.Scheduler.AdvanceTo(job.ReviewAt));

        Assert.That(caught, Is.SameAs(failure));
        Assert.That(context.Submissions.PendingReviews, Is.Empty);
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Rejected));
        Assert.That(progress.Credibility, Is.EqualTo(75));
        Assert.That(progress.History.Count, Is.EqualTo(1));
        scheduled.Callback();
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void CustomRules_UseRejectionAmountRatherThanMissedDeadlineAmount()
    {
        WorkdayProgress progress = Progress(new CredibilityRules(80, 12, 77));
        QueuedHrReview job = SubmitPrinter();
        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(progress.Credibility, Is.EqualTo(68));
        Assert.That(progress.History[0].CredibilityBefore, Is.EqualTo(80));
        Assert.That(progress.History[0].CredibilityAfter, Is.EqualTo(68));
    }
}
