using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class WorkdaySummaryTests
{
    private HrReviewTestContext context;
    private IncidentDefinition definition;
    private QueuedHrReview review;
    private WorkdaySettings settings;
    private WorkdayState finalState;
    private GameTime endedAt;
    private int credibility;
    private List<IncidentInstance> incidents;
    private List<ReviewHistoryEntry> history;
    private List<MissedDeadlineEntry> missed;

    [SetUp]
    public void SetUp()
    {
        context = new HrReviewTestContext();
        context.AttachProcessor();
        var progress = new WorkdayProgress(new CredibilityRules(missedDeadlinePenalty: 0),
            context.Processor, context.Scheduler);
        new IncidentDeadlineService(context.Deliveries, context.Scheduler, progress);
        review = context.Submit("D0I2", "dave", new List<string>(), new GameTime(0, 675));
        context.Scheduler.AdvanceTo(context.Day.ScheduleData.Settings.ClosesAt);
        definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
        Assert.That(definition, Is.Not.Null);
        settings = context.Day.ScheduleData.Settings;
        finalState = WorkdayState.Completed;
        endedAt = context.Clock.Now;
        credibility = progress.Credibility;
        incidents = new List<IncidentInstance>(context.Deliveries.DeliveredIncidents);
        history = new List<ReviewHistoryEntry>(progress.History);
        missed = new List<MissedDeadlineEntry>(progress.MissedDeadlines);
    }

    private WorkdaySummary Create()
    {
        return new WorkdaySummary(settings, finalState, endedAt, credibility,
            incidents, history, missed);
    }

    private IncidentInstance NewIncident(string id, int day = 0)
    {
        return new IncidentInstance(definition, new IncidentScheduleEntry(
            id, "I2", new TimeRange(day, 635, 640), 675, 720));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void NullInput_IsRejected(int input)
    {
        if (input == 0) settings = null;
        if (input == 1) incidents = null;
        if (input == 2) history = null;
        if (input == 3) missed = null;
        Assert.Throws<ArgumentNullException>(() => Create());
    }

    [TestCase(WorkdayState.Working)]
    [TestCase(WorkdayState.Closing)]
    [TestCase((WorkdayState)999)]
    public void Summary_RequiresTerminalState(WorkdayState state)
    {
        finalState = state;
        Assert.Throws<ArgumentException>(() => Create());
    }

    [TestCase(true)]
    [TestCase(false)]
    public void EndingTime_RespectsOpeningAndCompletedClosingBounds(bool fired)
    {
        finalState = fired ? WorkdayState.Fired : WorkdayState.Completed;
        endedAt = new GameTime((fired ? settings.OpensAt : settings.ClosesAt)
            .TotalMilliseconds - 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => Create());
    }

    [Test]
    public void NegativeCredibility_IsRejected()
    {
        credibility = -1;
        Assert.Throws<ArgumentOutOfRangeException>(() => Create());
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void InvalidIncidentMembership_IsRejected(int problem)
    {
        if (problem == 0) incidents.Add(null);
        if (problem == 1) incidents.Add(incidents[0]);
        if (problem == 2) incidents.Add(NewIncident("wrong_day", 1));
        Assert.Throws<ArgumentException>(() => Create());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CompletedSummary_RejectsOpenOrPendingIncident(bool pending)
    {
        var unresolved = NewIncident("unresolved");
        if (pending)
        {
            var validator = new ReportSubmissionValidator(new List<string> { "dave" },
                new EvidenceService(new EvidenceDatabase(new List<EvidenceDefinition>())));
            Assert.That(unresolved.TrySubmit(new IncidentReport("unresolved", "dave",
                new List<string>(), 0, 675), validator, 0, 675).IsValid, Is.True);
        }
        incidents.Add(unresolved);
        Assert.Throws<ArgumentException>(() => Create());
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void InvalidReviewHistory_IsRejected(int problem)
    {
        if (problem == 0) history.Add(null);
        if (problem == 1) history.Add(history[0]);
        if (problem == 2)
        {
            var unknown = NewIncident("unknown_review");
            var report = new IncidentReport("unknown_review", "dave", new List<string>(), 0, 675);
            var job = new QueuedHrReview(unknown, report, new GameTime(0, 675));
            var decision = new HrEvaluationResult(report, new List<HrRuleResult>());
            history.Add(new ReviewHistoryEntry(job, decision, new GameTime(0, 690), 100, 75));
        }
        if (problem == 3)
        {
            history[0] = new ReviewHistoryEntry(review, review.Incident.Decision,
                new GameTime(0, 1021), 100, 75);
        }
        Assert.Throws<ArgumentException>(() => Create());
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void InvalidMissedHistory_IsRejected(int problem)
    {
        if (problem == 0) missed.Add(null);
        if (problem == 1) missed.Add(missed[0]);
        if (problem == 2)
        {
            var unknown = NewIncident("unknown_miss");
            var at = new GameTime(0, 720, 0, 1);
            Assert.That(unknown.TryMarkOverdue(at), Is.True);
            missed.Add(new MissedDeadlineEntry(unknown, at, 100, 100));
        }
        if (problem == 3)
        {
            missed[0] = new MissedDeadlineEntry(incidents[0], new GameTime(0, 1021), 100, 100);
        }
        Assert.Throws<ArgumentException>(() => Create());
    }

    [Test]
    public void SameInstanceCannotHaveBothReviewedAndMissedOutcome()
    {
        var duplicateIdentity = NewIncident("D0I2");
        var at = new GameTime(0, 720, 0, 1);
        Assert.That(duplicateIdentity.TryMarkOverdue(at), Is.True);
        missed.Add(new MissedDeadlineEntry(duplicateIdentity, at, 100, 100));
        Assert.Throws<ArgumentException>(() => Create());
    }

    [Test]
    public void Summary_CopiesCollectionsAndExposesReadOnlySnapshots()
    {
        ReviewHistoryEntry originalEntry = history[0];
        WorkdaySummary summary = Create();
        incidents.Clear();
        history.Clear();
        missed.Clear();

        Assert.That(summary.Settings, Is.SameAs(settings));
        Assert.That(summary.FinalState, Is.EqualTo(WorkdayState.Completed));
        Assert.That(summary.Credibility, Is.EqualTo(75));
        Assert.That(summary.EndedAt.TotalMilliseconds, Is.EqualTo(endedAt.TotalMilliseconds));
        Assert.That(summary.Incidents.Count, Is.EqualTo(3));
        Assert.That(summary.History.Count, Is.EqualTo(1));
        Assert.That(summary.MissedDeadlines.Count, Is.EqualTo(2));
        Assert.That(summary.History[0], Is.SameAs(originalEntry));
        Assert.Throws<NotSupportedException>(
            () => ((IList<IncidentSummary>)summary.Incidents).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<ReviewHistoryEntry>)summary.History).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<MissedDeadlineEntry>)summary.MissedDeadlines).Clear());
    }

    [Test]
    public void FiredSummary_FreezesOpenAndPendingStatesEvenIfLiveIncidentLaterChanges()
    {
        var separate = new HrReviewTestContext();
        QueuedHrReview job = separate.Submit("D0I2", "dave",
            new List<string>(), new GameTime(0, 675));
        var summary = new WorkdaySummary(separate.Day.ScheduleData.Settings,
            WorkdayState.Fired, separate.Clock.Now, 0,
            new List<IncidentInstance>(separate.Deliveries.DeliveredIncidents),
            new List<ReviewHistoryEntry>(), new List<MissedDeadlineEntry>());
        var snapshots = new List<IncidentSummary>(summary.Incidents);
        IncidentSummary pending = snapshots.Find(item => item.InstanceId == "D0I2");
        IncidentSummary open = snapshots.Find(item => item.InstanceId == "D0I1");
        Assert.That(pending, Is.Not.Null);
        Assert.That(open, Is.Not.Null);

        var decision = new HrEvaluationResult(job.Report, new List<HrRuleResult>());
        Assert.That(job.Incident.TryApplyDecision(decision), Is.True);

        Assert.That(pending.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(pending.Report, Is.SameAs(job.Report));
        Assert.That(pending.Decision, Is.Null);
        Assert.That(pending.DayIndex, Is.Zero);
        Assert.That(pending.IncidentId, Is.EqualTo("I2"));
        Assert.That(pending.DisplayName, Is.EqualTo(job.Incident.DisplayName));
        Assert.That(open.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(open.Report, Is.Null);
        Assert.That(summary.Credibility, Is.Zero);
        Assert.That(summary.History, Is.Empty);
    }

    [Test]
    public void IncidentSnapshot_RejectsNullInstance()
    {
        Assert.Throws<ArgumentNullException>(() => new IncidentSummary(null));
    }
}
