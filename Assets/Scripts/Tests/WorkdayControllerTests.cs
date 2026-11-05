using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class WorkdayControllerTests
{
    private HrReviewTestContext context;
    private WorkdayProgress progress;
    private WorkdayController controller;

    [SetUp]
    public void SetUp()
    {
        UseDay();
    }

    private void UseDay(DayData data = null, CredibilityRules rules = null)
    {
        context = new HrReviewTestContext(data);
        context.AttachProcessor();
        progress = new WorkdayProgress(
            rules ?? new CredibilityRules(), context.Processor, context.Scheduler);
        new IncidentDeadlineService(context.Deliveries, context.Scheduler, progress);
        controller = new WorkdayController(context.Day.ScheduleData.Settings,
            context.Scheduler, context.Deliveries, context.Submissions, progress);
    }

    private static DayData EmptyDay()
    {
        var schedule = new DayScheduleData(
            new WorkdaySettings("empty", "Empty day", 0, 540, 1020, 20),
            new List<string>(), new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>());
        return new DayData(schedule, new List<EvidenceDefinition>(), new List<DayIncidentPlan>());
    }

    private static IncidentDefinition LoadIncident(string filename)
    {
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/" + filename + ".asset");
        Assert.That(definition, Is.Not.Null);
        return definition;
    }

    private DayData LateDay(int count = 1)
    {
        var definition = LoadIncident("I2_PrinterDamage");
        var plans = new List<DayIncidentPlan>();
        for (int i = 0; i < count; i++)
        {
            plans.Add(new DayIncidentPlan(definition, new IncidentScheduleEntry(
                "late_" + i, "I2", new TimeRange(0, 635, 640), 1000, 1019)));
        }
        return new DayData(context.Day.ScheduleData,
            new List<EvidenceDefinition>(context.Day.EvidenceDatabase.All), plans);
    }

    private QueuedHrReview SubmitLate(
        int index = 0, string document = "E_DAVE_PRINTER", int second = 0, int millisecond = 0)
    {
        return context.Submit("late_" + index, "dave", new List<string> { document },
            new GameTime(0, 1018, second, millisecond));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void Constructor_RejectsNullDependencyBeforeChangingScheduler(int input)
    {
        int before = context.Scheduler.PendingCount;
        Assert.Throws<ArgumentNullException>(() => new WorkdayController(
            input == 0 ? null : context.Day.ScheduleData.Settings,
            input == 1 ? null : context.Scheduler,
            input == 2 ? null : context.Deliveries,
            input == 3 ? null : context.Submissions,
            input == 4 ? null : progress));
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(before));
    }

    [TestCase(-1, false)]
    [TestCase(1, false)]
    [TestCase(0, true)]
    public void Constructor_RequiresRunningSchedulerAtExactOpening(int offset, bool stopped)
    {
        var scheduler = new EventScheduler(new GameClock(new GameTime(
            context.Day.ScheduleData.Settings.OpensAt.TotalMilliseconds + offset)));
        if (stopped) scheduler.Stop();
        Assert.Throws<ArgumentException>(() => new WorkdayController(
            context.Day.ScheduleData.Settings, scheduler,
            context.Deliveries, context.Submissions, progress));
        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [Test]
    public void InitialState_IsWorkingWithNoSummary()
    {
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Working));
        Assert.That(controller.CanAcceptWork, Is.True);
        Assert.That(controller.IsTerminal, Is.False);
        Assert.That(controller.Summary, Is.Null);
    }

    [Test]
    public void EarlyClosingCallbackAndTerminalReplay_DoNotChangeState()
    {
        UseDay(EmptyDay());
        ScheduledEvent closing = context.Scheduler.PeekNext();
        int changes = 0;
        controller.StateChanged += () => changes++;
        closing.Callback();
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Working));
        Assert.That(controller.Summary, Is.Null);

        context.Scheduler.AdvanceTo(context.Day.ScheduleData.Settings.ClosesAt);
        closing.Callback();
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void EmptyDay_CompletesAtClosingAndPublishesCommittedTerminalState()
    {
        UseDay(EmptyDay());
        int changes = 0;
        controller.StateChanged += () =>
        {
            changes++;
            Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
            Assert.That(controller.Summary, Is.Not.Null);
            Assert.That(context.Scheduler.IsStopped, Is.True);
            Assert.That(controller.CanAcceptWork, Is.False);
            Assert.That(context.Submissions.TrySubmit("unknown", "dave",
                new List<string>()).IsValid, Is.False);
        };

        context.Scheduler.AdvanceTo(new GameTime(0, 1100));

        Assert.That(changes, Is.EqualTo(1));
        Assert.That(context.Clock.Now.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 1020).TotalMilliseconds));
        Assert.That(controller.Summary.EndedAt.TotalMilliseconds,
            Is.EqualTo(context.Clock.Now.TotalMilliseconds));
        Assert.That(controller.Summary.Incidents, Is.Empty);
    }

    [Test]
    public void DefaultThreeMissedDeadlines_FireBeforeClosingAtFirstThresholdCrossing()
    {
        context.Scheduler.AdvanceTo(new GameTime(0, 1100));

        Assert.That(controller.State, Is.EqualTo(WorkdayState.Fired));
        Assert.That(controller.Summary.Credibility, Is.Zero);
        Assert.That(controller.Summary.MissedDeadlines.Count, Is.EqualTo(3));
        Assert.That(controller.Summary.Incidents.Count, Is.EqualTo(3));
        Assert.That(controller.Summary.EndedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 900, 0, 1).TotalMilliseconds));
        Assert.That(context.Clock.Now.TotalMilliseconds,
            Is.EqualTo(controller.Summary.EndedAt.TotalMilliseconds));
    }

    [Test]
    public void EarlyFiring_BlocksOtherwiseEligibleReportAndDoesNotRevealFutureIncident()
    {
        var plans = new List<DayIncidentPlan>
        {
            context.Day.Incidents[0],
            new DayIncidentPlan(LoadIncident("I2_PrinterDamage"), new IncidentScheduleEntry(
                "early_printer", "I2", new TimeRange(0, 600, 605), 610, 720)),
            context.Day.Incidents[2]
        };
        UseDay(new DayData(context.Day.ScheduleData,
            new List<EvidenceDefinition>(context.Day.EvidenceDatabase.All), plans),
            new CredibilityRules(35, 25, 35));
        context.Scheduler.AdvanceTo(new GameTime(0, 1100));

        Assert.That(controller.State, Is.EqualTo(WorkdayState.Fired));
        Assert.That(controller.Summary.Incidents.Count, Is.EqualTo(2));
        Assert.Throws<KeyNotFoundException>(() => context.Deliveries.GetDelivered("D0I3"));
        IncidentInstance eligible = context.Deliveries.GetDelivered("early_printer");
        Assert.That(eligible.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(context.Clock.Now.TotalMilliseconds,
            Is.LessThan(new GameTime(0, eligible.DeadlineMinute).TotalMilliseconds));
        Assert.That(context.Submissions.TrySubmit("early_printer", "dave",
            new List<string>()).IsValid, Is.False);
        Assert.That(eligible.SubmittedReport, Is.Null);

        WorkdaySummary summary = controller.Summary;
        context.Clock.SetPaused(true);
        context.Clock.SetPaused(false);
        context.Scheduler.AdvanceRealSeconds(100);
        context.Scheduler.AdvanceTo(new GameTime(1, 0));
        Assert.That(controller.Summary, Is.SameAs(summary));
        Assert.That(context.Clock.Now.TotalMilliseconds, Is.EqualTo(summary.EndedAt.TotalMilliseconds));
    }

    [Test]
    public void Closing_WaitsForPreciseReviewTimeAndDropsNonHrWork()
    {
        UseDay(LateDay());
        var states = new List<WorkdayState>();
        controller.StateChanged += () => states.Add(controller.State);
        QueuedHrReview job = SubmitLate(second: 30, millisecond: 250);
        bool timerFired = false;
        context.Scheduler.Schedule(new ScheduledEvent("test_timer", new GameTime(0, 1030),
            ScheduledEventPriority.TimerExpiry, () => timerFired = true));

        context.Scheduler.AdvanceTo(new GameTime(0, 1020));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Closing));
        Assert.That(controller.Summary, Is.Null);
        Assert.That(controller.CanAcceptWork, Is.False);
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(context.Submissions.TrySubmit("late_0", "dave",
            new List<string>()).IsValid, Is.False);
        Assert.Throws<InvalidOperationException>(() => context.Scheduler.Schedule(
            new ScheduledEvent("new_timer", new GameTime(0, 1021),
                ScheduledEventPriority.TimerExpiry, () => { })));

        context.Scheduler.AdvanceTo(new GameTime(job.ReviewAt.TotalMilliseconds - 1));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Closing));
        context.Scheduler.AdvanceTo(new GameTime(0, 1100));

        Assert.That(states, Is.EqualTo(new[] { WorkdayState.Closing, WorkdayState.Completed }));
        Assert.That(timerFired, Is.False);
        Assert.That(controller.Summary.History.Count, Is.EqualTo(1));
        Assert.That(controller.Summary.Credibility, Is.EqualTo(100));
        Assert.That(controller.Summary.EndedAt.TotalMilliseconds, Is.EqualTo(job.ReviewAt.TotalMilliseconds));
        Assert.That(context.Clock.Now.TotalMilliseconds, Is.EqualTo(job.ReviewAt.TotalMilliseconds));
        Assert.That(context.Scheduler.IsStopped, Is.True);
    }

    [Test]
    public void ReviewAtClosing_SeesTimestampGateBeforeClosingCallbackRuns()
    {
        UseDay(LateDay());
        QueuedHrReview job = context.Submit("late_0", "dave",
            new List<string> { "E_DAVE_PRINTER" }, new GameTime(0, 1005));
        Assert.That(job.ReviewAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 1020).TotalMilliseconds));
        bool observed = false;
        context.Processor.ReviewCompleted += (review, result) =>
        {
            observed = true;
            Assert.That(controller.State, Is.EqualTo(WorkdayState.Working));
            Assert.That(controller.CanAcceptWork, Is.False);
            var blocked = context.Submissions.TrySubmit("unknown", "dave", new List<string>());
            Assert.That(blocked.IsValid, Is.False);
            Assert.That(blocked.Reason, Does.Contain("not accepting"));
        };

        context.Scheduler.AdvanceTo(new GameTime(0, 1100));
        Assert.That(observed, Is.True);
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(controller.Summary.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void FiringDuringClosing_StopsImmediatelyAndPreservesOtherPendingReport()
    {
        UseDay(LateDay(2), new CredibilityRules(25, 25, 35));
        var states = new List<WorkdayState>();
        controller.StateChanged += () => states.Add(controller.State);
        QueuedHrReview first = SubmitLate(0, "E_OFFICE_SOCIAL");
        QueuedHrReview second = SubmitLate(1, "E_DAVE_PRINTER", second: 1);

        context.Scheduler.AdvanceTo(new GameTime(0, 1100));

        Assert.That(states, Is.EqualTo(new[] { WorkdayState.Closing, WorkdayState.Fired }));
        Assert.That(controller.Summary.EndedAt.TotalMilliseconds,
            Is.EqualTo(first.ReviewAt.TotalMilliseconds));
        Assert.That(controller.Summary.History.Count, Is.EqualTo(1));
        Assert.That(controller.Summary.Incidents[1].Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(controller.Summary.Incidents[1].Report, Is.SameAs(second.Report));
        Assert.That(controller.Summary.Incidents[1].Decision, Is.Null);
        Assert.That(context.Submissions.PendingReviews.Count, Is.EqualTo(1));
        Assert.That(second.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(context.Scheduler.PendingCount, Is.Zero);

        context.Scheduler.AdvanceTo(second.ReviewAt);
        Assert.That(second.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(progress.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void StaleJobRemoval_CompletesClosingWithoutProgressNotification()
    {
        UseDay(LateDay());
        QueuedHrReview job = SubmitLate();
        context.Scheduler.AdvanceTo(new GameTime(0, 1020));
        var decision = new HrEvaluator().Evaluate(new EvaluationContext(
            job.Incident, context.Day.ScheduleData.HrKnowledge, context.Day.EvidenceDatabase));
        Assert.That(job.Incident.TryApplyDecision(decision), Is.True);
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Closing));

        context.Scheduler.AdvanceTo(job.ReviewAt);

        Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(context.Submissions.PendingReviews, Is.Empty);
        Assert.That(controller.Summary.Incidents[0].Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(controller.Summary.History, Is.Empty);
    }

    [Test]
    public void DeliberatePause_DefersClosingUntilResumed()
    {
        UseDay(EmptyDay());
        context.Clock.SetPaused(true);
        context.Scheduler.AdvanceTo(new GameTime(0, 1020));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Working));
        Assert.That(controller.Summary, Is.Null);
        context.Clock.SetPaused(false);
        context.Scheduler.AdvanceTo(new GameTime(0, 1020));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
    }

    [Test]
    public void DuplicateController_CannotReplaceSubmissionGate()
    {
        UseDay(EmptyDay());
        int before = context.Scheduler.PendingCount;
        Assert.Throws<InvalidOperationException>(() => new WorkdayController(
            context.Day.ScheduleData.Settings, context.Scheduler,
            context.Deliveries, context.Submissions, progress));
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(before));
        Assert.That(controller.CanAcceptWork, Is.True);
        context.Scheduler.AdvanceTo(new GameTime(0, 1020));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
    }

    [Test]
    public void TerminalNotificationException_PreservesStoppedStateAndSingleSummary()
    {
        UseDay(EmptyDay());
        var failure = new InvalidOperationException("Test terminal notification failure.");
        int changes = 0;
        controller.StateChanged += () => { changes++; throw failure; };

        var caught = Assert.Throws<InvalidOperationException>(
            () => context.Scheduler.AdvanceTo(new GameTime(0, 1100)));

        Assert.That(caught, Is.SameAs(failure));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(context.Scheduler.IsStopped, Is.True);
        WorkdaySummary summary = controller.Summary;
        context.Scheduler.AdvanceRealSeconds(100);
        context.Scheduler.AdvanceTo(new GameTime(0, 1100));
        Assert.That(controller.Summary, Is.SameAs(summary));
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void SeparateSessions_DoNotShareStateGateOrSummary()
    {
        DayData data = EmptyDay();
        UseDay(data);
        var second = new HrReviewTestContext(data);
        second.AttachProcessor();
        var secondProgress = new WorkdayProgress(new CredibilityRules(),
            second.Processor, second.Scheduler);
        var secondController = new WorkdayController(data.ScheduleData.Settings,
            second.Scheduler, second.Deliveries, second.Submissions, secondProgress);

        context.Scheduler.AdvanceTo(new GameTime(0, 1020));

        Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(secondController.State, Is.EqualTo(WorkdayState.Working));
        Assert.That(secondController.CanAcceptWork, Is.True);
        Assert.That(secondController.Summary, Is.Null);
        Assert.That(second.Scheduler.IsStopped, Is.False);
    }
}
