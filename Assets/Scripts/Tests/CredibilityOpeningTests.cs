using System;
using System.Collections.Generic;
using NUnit.Framework;

public class CredibilityOpeningTests
{
    private HrReviewTestContext context;

    [SetUp]
    public void SetUp()
    {
        context = new HrReviewTestContext();
        context.AttachProcessor();
    }

    private WorkdayProgress Create(CredibilityRules rules, int? opening = null)
    {
        return new WorkdayProgress(rules, context.Processor, context.Scheduler, opening);
    }

    [TestCase(null, 80)]
    [TestCase(11, 11)]
    [TestCase(65, 65)]
    [TestCase(100, 100)]
    public void Opening_UsesExplicitValueOrRulesDefault(int? opening, int expected)
    {
        var rules = new CredibilityRules(80, 25, 35, 10);
        WorkdayProgress progress = Create(rules, opening);

        Assert.That(progress.Credibility, Is.EqualTo(expected));
        Assert.That(progress.HasReachedFiringThreshold, Is.False);
        Assert.That(progress.History, Is.Empty);
        Assert.That(progress.MissedDeadlines, Is.Empty);
        Assert.That(rules.StartingCredibility, Is.EqualTo(80));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(101)]
    public void InvalidPlayableOpening_IsRejected(int opening)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(new CredibilityRules(80, 25, 35, 10), opening));
        Assert.That(error.ParamName, Is.EqualTo("openingCredibility"));
    }

    [Test]
    public void Opening_UsesConfiguredMaximumInsteadOfHardcoded100()
    {
        WorkdayProgress progress = Create(
            new CredibilityRules(maximumCredibility: 140), 140);
        Assert.That(progress.Credibility, Is.EqualTo(140));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(new CredibilityRules(maximumCredibility: 140), 141));
    }

    [TestCase("E_DAVE_PRINTER", 65, 65, HrVerdict.Approved)]
    [TestCase("E_OFFICE_SOCIAL", 65, 40, HrVerdict.Rejected)]
    [TestCase("E_OFFICE_SOCIAL", 11, 0, HrVerdict.Rejected)]
    public void Review_RecordsActualInjectedBeforeAndClampedAfter(
        string evidenceId, int opening, int expectedAfter, HrVerdict verdict)
    {
        WorkdayProgress progress = Create(new CredibilityRules(100, 25, 35, 10), opening);
        QueuedHrReview review = context.Submit("D0I2", "dave",
            new List<string> { evidenceId }, new GameTime(0, 675));
        ScheduledEvent callback = context.Scheduler.PeekNext();
        Assert.That(callback.DueTime.TotalMilliseconds, Is.EqualTo(review.ReviewAt.TotalMilliseconds));

        int changes = 0;
        progress.Changed += () =>
        {
            changes++;
            Assert.That(progress.Credibility, Is.EqualTo(expectedAfter));
            Assert.That(progress.History.Count, Is.EqualTo(1));
            Assert.That(progress.History[0].CredibilityBefore, Is.EqualTo(opening));
            Assert.That(progress.History[0].CredibilityAfter, Is.EqualTo(expectedAfter));
            Assert.That(progress.History[0].Decision.Verdict, Is.EqualTo(verdict));
            Assert.That(context.Submissions.PendingReviews, Is.Empty);
        };

        context.Scheduler.AdvanceTo(review.ReviewAt);
        callback.Callback();
        callback.Callback();

        Assert.That(changes, Is.EqualTo(1));
        Assert.That(progress.Credibility, Is.EqualTo(expectedAfter));
        Assert.That(progress.History.Count, Is.EqualTo(1));
        Assert.That(progress.HasReachedFiringThreshold, Is.EqualTo(expectedAfter <= 10));
    }

    [TestCase(65, 30)]
    [TestCase(11, 0)]
    public void MissedDeadline_RecordsActualOpeningAndChargesOnce(int opening, int expectedAfter)
    {
        WorkdayProgress progress = Create(new CredibilityRules(100, 25, 35, 10), opening);
        var deadlines = new IncidentDeadlineService(context.Deliveries, context.Scheduler, progress);
        var exact = new GameTime(0, 630);
        var late = new GameTime(exact.TotalMilliseconds + 1);
        int changes = 0;
        int overdueNotices = 0;
        progress.Changed += () =>
        {
            changes++;
            Assert.That(progress.Credibility, Is.EqualTo(expectedAfter));
            Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
            Assert.That(progress.MissedDeadlines[0].CredibilityBefore, Is.EqualTo(opening));
            Assert.That(progress.MissedDeadlines[0].CredibilityAfter, Is.EqualTo(expectedAfter));
        };
        deadlines.IncidentOverdue += incident => overdueNotices++;

        context.Scheduler.AdvanceTo(exact);
        Assert.That(progress.Credibility, Is.EqualTo(opening));
        Assert.That(progress.MissedDeadlines, Is.Empty);
        Assert.That(changes, Is.Zero);
        ScheduledEvent callback = context.Scheduler.PeekNext();
        Assert.That(callback.DueTime.TotalMilliseconds, Is.EqualTo(late.TotalMilliseconds));

        context.Scheduler.AdvanceTo(late);
        callback.Callback();
        callback.Callback();

        Assert.That(changes, Is.EqualTo(1));
        Assert.That(overdueNotices, Is.EqualTo(1));
        Assert.That(progress.Credibility, Is.EqualTo(expectedAfter));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
        Assert.That(progress.MissedDeadlines[0].InstanceId, Is.EqualTo("D0I1"));
        Assert.That(progress.MissedDeadlines[0].RecordedAt.TotalMilliseconds,
            Is.EqualTo(late.TotalMilliseconds));
        Assert.That(progress.History, Is.Empty);
    }
}
