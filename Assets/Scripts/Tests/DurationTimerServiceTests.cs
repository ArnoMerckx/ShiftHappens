using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class DurationTimerServiceTests
{
    private HrReviewTestContext context;
    private WorkdayController controller;
    private DurationTimerService timer;
    private int alerts;

    [SetUp]
    public void SetUp()
    {
        UseDay(EmptyDay());
    }

    private static DayData EmptyDay()
    {
        var schedules = new DayScheduleData(
            new WorkdaySettings("timer_day", "Timer day", 0, 540, 1020, 20),
            new List<string>(), new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>());
        return new DayData(schedules, new List<EvidenceDefinition>(),
            new List<DayIncidentPlan>());
    }

    private void UseDay(DayData day, CredibilityRules rules = null)
    {
        context = new HrReviewTestContext(day);
        context.AttachProcessor();
        var progress = new WorkdayProgress(rules ?? new CredibilityRules(),
            context.Processor, context.Scheduler);
        new IncidentDeadlineService(context.Deliveries, context.Scheduler, progress);
        controller = new WorkdayController(day.ScheduleData.Settings,
            context.Scheduler, context.Deliveries, context.Submissions, progress);
        timer = new DurationTimerService(context.Scheduler, controller);
        alerts = 0;
        timer.Completed += () => alerts++;
    }

    private void Advance(long milliseconds)
    {
        context.Scheduler.AdvanceTo(new GameTime(
            context.Scheduler.Now.TotalMilliseconds + milliseconds));
    }

    private DayData LateDay()
    {
        var monday = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
        Assert.That(monday, Is.Not.Null);
        DayData data = monday.GetData();
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
        Assert.That(definition, Is.Not.Null);
        return new DayData(data.ScheduleData,
            new List<EvidenceDefinition>(data.EvidenceDatabase.All),
            new List<DayIncidentPlan>
            {
                new DayIncidentPlan(definition, new IncidentScheduleEntry(
                    "timer_late", "I2", new TimeRange(0, 635, 640), 1000, 1019), "dave")
            });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Constructor_RejectsNullDependencies(bool missingScheduler)
    {
        int before = context.Scheduler.PendingCount;
        Assert.Throws<ArgumentNullException>(() => new DurationTimerService(
            missingScheduler ? null : context.Scheduler,
            missingScheduler ? controller : null));
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(before));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Start_RejectsNonpositiveInputWithoutRegisteringExpiry(int seconds)
    {
        int before = context.Scheduler.PendingCount;
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.TryStart(seconds));
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(before));
    }

    [Test]
    public void Expiry_CompletesAtExactMillisecondAndNotifiesOnce()
    {
        Assert.That(timer.TryStart(60), Is.True);
        ScheduledEvent expiry = context.Scheduler.PeekNext();
        Assert.That(expiry.Priority, Is.EqualTo(ScheduledEventPriority.TimerExpiry));
        long observedAt = -1;
        timer.Completed += () =>
        {
            observedAt = context.Scheduler.Now.TotalMilliseconds;
            Assert.That(timer.State, Is.EqualTo(DurationTimerState.Completed));
            Assert.That(timer.RemainingMilliseconds, Is.Zero);
        };
        Advance(59999);
        Assert.That(alerts, Is.Zero);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(1L));
        Advance(1);
        Assert.That(alerts, Is.EqualTo(1));
        Assert.That(observedAt, Is.EqualTo(expiry.DueTime.TotalMilliseconds));
        expiry.Callback();
        Advance(60000);
        Assert.That(alerts, Is.EqualTo(1));
    }

    [Test]
    public void LargeAdvance_DispatchesAtExpiryBeforeReachingTarget()
    {
        timer.TryStart(60);
        long opening = context.Scheduler.Now.TotalMilliseconds;
        long observedAt = -1;
        timer.Completed += () => observedAt = context.Scheduler.Now.TotalMilliseconds;
        Advance(300000);
        Assert.That(observedAt, Is.EqualTo(opening + 60000));
        Assert.That(context.Scheduler.Now.TotalMilliseconds, Is.EqualTo(opening + 300000));
        Assert.That(alerts, Is.EqualTo(1));
    }

    [Test]
    public void Completion_DoesNotRequireAViewOrSubscriber()
    {
        var unseen = new DurationTimerService(context.Scheduler, controller);
        unseen.TryStart(1);
        Advance(1000);
        Assert.That(unseen.State, Is.EqualTo(DurationTimerState.Completed));
        Assert.That(unseen.RemainingMilliseconds, Is.Zero);
    }

    [Test]
    public void Pause_CancelsOldExpiryAndResumeUsesPreciseRemainder()
    {
        timer.TryStart(60);
        ScheduledEvent old = context.Scheduler.PeekNext();
        Advance(12345);
        Assert.That(timer.TryPause(), Is.True);
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(1), "Only closing remains.");
        Advance(120000);
        old.Callback();
        Assert.That(alerts, Is.Zero);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(47655L));
        Assert.That(timer.TryResume(), Is.True);
        ScheduledEvent resumed = context.Scheduler.PeekNext();
        Assert.That(resumed.EventId, Is.Not.EqualTo(old.EventId));
        old.Callback();
        Advance(47654);
        Assert.That(alerts, Is.Zero);
        Advance(1);
        Assert.That(alerts, Is.EqualTo(1));
    }

    [Test]
    public void Reset_CancelsExpiryAndOldCallbackCannotCompleteNewRun()
    {
        timer.TryStart(1);
        ScheduledEvent old = context.Scheduler.PeekNext();
        Assert.That(timer.TryReset(), Is.True);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(1000L));
        Assert.That(timer.TryStart(60), Is.True);
        Advance(1000);
        old.Callback();
        Assert.That(alerts, Is.Zero);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Running));
        Advance(59000);
        Assert.That(alerts, Is.EqualTo(1));
    }

    [Test]
    public void ResetWhilePaused_DoesNotLeaveAnExpiry()
    {
        timer.TryStart(60);
        timer.TryPause();
        Assert.That(timer.TryReset(), Is.True);
        Advance(120000);
        Assert.That(alerts, Is.Zero);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(60000L));
    }

    [Test]
    public void ActiveStartAndInvalidTransitions_DoNotAddEvents()
    {
        Assert.That(timer.TryPause(), Is.False);
        Assert.That(timer.TryResume(), Is.False);
        timer.TryStart(60);
        int before = context.Scheduler.PendingCount;
        Assert.That(timer.TryStart(120), Is.False);
        Assert.That(timer.TryResume(), Is.False);
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(before));
        timer.TryPause();
        Assert.That(timer.TryStart(120), Is.False);
        Assert.That(timer.TryPause(), Is.False);
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(1));
    }

    [Test]
    public void CompletionSubscriber_CanImmediatelyStartAnotherDuration()
    {
        timer.Completed += () =>
        {
            if (alerts == 1)
                Assert.That(timer.TryStart(2), Is.True);
        };
        timer.TryStart(1);
        Advance(3000);
        Assert.That(alerts, Is.EqualTo(2));
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Completed));
    }

    [Test]
    public void SubscriberException_DoesNotUndoCompletionOrReplayAlert()
    {
        timer.Completed += () => throw new InvalidOperationException("View failure");
        timer.TryStart(1);
        ScheduledEvent expiry = context.Scheduler.PeekNext();
        Assert.Throws<InvalidOperationException>(() => Advance(1000));
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Completed));
        Assert.That(alerts, Is.EqualTo(1));
        Assert.DoesNotThrow(() => expiry.Callback());
        Advance(1000);
        Assert.That(alerts, Is.EqualTo(1));
    }

    [Test]
    public void GamePause_FreezesExpiryUntilSharedTimeAdvances()
    {
        timer.TryStart(20);
        context.Clock.SetPaused(true);
        context.Scheduler.AdvanceRealSeconds(100);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(20000L));
        Assert.That(alerts, Is.Zero);
        context.Clock.SetPaused(false);
        context.Scheduler.AdvanceRealSeconds(1);
        Assert.That(alerts, Is.EqualTo(1));
    }

    [Test]
    public void IndependentServices_DoNotCollideOrCancelEachOther()
    {
        var other = new DurationTimerService(context.Scheduler, controller);
        int otherAlerts = 0;
        other.Completed += () => otherAlerts++;
        timer.TryStart(1);
        other.TryStart(1);
        timer.TryPause();
        Advance(1000);
        Assert.That(alerts, Is.Zero);
        Assert.That(otherAlerts, Is.EqualTo(1));
        timer.TryResume();
        Advance(1000);
        Assert.That(alerts, Is.EqualTo(1));
        Assert.That(otherAlerts, Is.EqualTo(1));
    }

    [Test]
    public void ExistingTimer_CanExpireExactlyAtClosingBeforeClosingEvent()
    {
        context.Scheduler.AdvanceTo(new GameTime(0, 1019));
        timer.TryStart(60);
        WorkdayState? observed = null;
        timer.Completed += () => observed = controller.State;
        Advance(60000);
        Assert.That(alerts, Is.EqualTo(1));
        Assert.That(observed, Is.EqualTo(WorkdayState.Working));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Completed));
    }

    [Test]
    public void CommandsAtExactClosing_AreDeniedEvenBeforeClosingCallback()
    {
        context.Scheduler.AdvanceTo(new GameTime(0, 1019));
        timer.TryStart(120);
        context.Scheduler.Schedule(new ScheduledEvent("closing_command_probe",
            new GameTime(0, 1020), ScheduledEventPriority.Delivery, () =>
            {
                Assert.That(controller.State, Is.EqualTo(WorkdayState.Working));
                Assert.That(timer.TryPause(), Is.False);
                Assert.That(timer.TryResume(), Is.False);
                Assert.That(timer.TryReset(), Is.False);
                Assert.That(timer.TryStart(60), Is.False);
            }));
        Advance(60000);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Paused));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(60000L));
        Assert.That(alerts, Is.Zero);
    }

    [Test]
    public void Closing_CancelsFutureExpiryAndFreezesDuringHrDrain()
    {
        UseDay(LateDay());
        context.Submit("timer_late", "dave", new List<string> { "E_DAVE_PRINTER" },
            new GameTime(0, 1018));
        timer.TryStart(600);
        context.Scheduler.AdvanceTo(new GameTime(0, 1020));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Closing));
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Paused));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(480000L));
        Assert.That(timer.TryResume(), Is.False);
        context.Scheduler.AdvanceTo(new GameTime(0, 1100));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(context.Scheduler.Now.MinuteOfDay, Is.EqualTo(1033));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(480000L));
        Assert.That(alerts, Is.Zero);
    }

    [Test]
    public void FiringAtExpiry_HrPrioritySuppressesTimerAlert()
    {
        UseDay(LateDay(), new CredibilityRules(25, 25, 35, 0));
        context.Submit("timer_late", "dave", new List<string>(),
            new GameTime(0, 1001));
        timer.TryStart(15 * 60);
        context.Scheduler.AdvanceTo(new GameTime(0, 1017));
        Assert.That(controller.State, Is.EqualTo(WorkdayState.Fired));
        Assert.That(context.Scheduler.Now.MinuteOfDay, Is.EqualTo(1016));
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Paused));
        Assert.That(timer.RemainingMilliseconds, Is.Zero);
        Assert.That(alerts, Is.Zero);
        Assert.That(timer.TryStart(60), Is.False);
        Assert.That(timer.TryResume(), Is.False);
        Assert.That(timer.TryReset(), Is.False);
    }

    [Test]
    public void TerminalDay_DeniesCommandsAndPreservesIdleTimer()
    {
        context.Scheduler.AdvanceTo(new GameTime(0, 1020));
        Assert.That(timer.TryStart(60), Is.False);
        Assert.That(timer.TryPause(), Is.False);
        Assert.That(timer.TryResume(), Is.False);
        Assert.That(timer.TryReset(), Is.False);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(alerts, Is.Zero);
    }
}
