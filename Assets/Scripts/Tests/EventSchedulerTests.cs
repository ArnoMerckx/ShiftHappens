using System;
using System.Collections.Generic;
using NUnit.Framework;

public class EventSchedulerTests
{
    private GameClock clock;
    private EventScheduler scheduler;

    [SetUp]
    public void SetUp()
    {
        clock = new GameClock(default(GameTime), 1);
        scheduler = new EventScheduler(clock);
    }

    private static ScheduledEvent MakeEvent(
        string id,
        long due,
        Action callback = null,
        ScheduledEventPriority priority =
            ScheduledEventPriority.HrReview)
    {
        return new ScheduledEvent(
            id,
            new GameTime(due),
            priority,
            callback ?? (() => { }));
    }

    [Test]
    public void Constructor_RejectsNullClock()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EventScheduler(null));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Event_RejectsBlankId(string id)
    {
        Assert.Throws<ArgumentException>(
            () => MakeEvent(id, 100));
    }

    [Test]
    public void Event_RejectsInvalidPriorityAndNullCallback()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MakeEvent(
                "invalid", 100,
                priority: (ScheduledEventPriority)999));

        Assert.Throws<ArgumentNullException>(
            () => new ScheduledEvent(
                "invalid",
                new GameTime(100L),
                ScheduledEventPriority.HrReview,
                null));
    }

    [Test]
    public void Schedule_RejectsNullEvent()
    {
        Assert.Throws<ArgumentNullException>(
            () => scheduler.Schedule(null));

        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [Test]
    public void PastRegistration_FailsWithoutReservingId()
    {
        scheduler.AdvanceTo(new GameTime(100L));

        Assert.Throws<ArgumentException>(
            () => scheduler.Schedule(MakeEvent("review", 99)));

        scheduler.Schedule(MakeEvent("review", 100));

        Assert.That(scheduler.PendingCount, Is.EqualTo(1));
    }

    [Test]
    public void EmptyQueue_AdvancesClock()
    {
        Assert.That(scheduler.PeekNext(), Is.Null);

        scheduler.AdvanceTo(new GameTime(500L));

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(500L));
        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [Test]
    public void DueBoundary_DispatchesExactlyOnce()
    {
        int calls = 0;
        scheduler.Schedule(MakeEvent("review", 100, () => calls++));

        scheduler.AdvanceTo(new GameTime(99L));

        Assert.That(calls, Is.Zero);
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));

        scheduler.AdvanceTo(new GameTime(100L));
        scheduler.AdvanceTo(new GameTime(100L));
        scheduler.AdvanceTo(new GameTime(200L));

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(scheduler.PendingCount, Is.Zero);
        Assert.That(scheduler.PeekNext(), Is.Null);
    }

    [Test]
    public void Dispatch_IsChronologicalAndCallbacksSeeDueTime()
    {
        var observed = new List<long>();

        scheduler.Schedule(MakeEvent(
            "late", 300,
            () => observed.Add(clock.Now.TotalMilliseconds),
            ScheduledEventPriority.Delivery));

        scheduler.Schedule(MakeEvent(
            "early", 100,
            () => observed.Add(clock.Now.TotalMilliseconds),
            ScheduledEventPriority.WorkdayClosing));

        scheduler.Schedule(MakeEvent(
            "middle", 200,
            () => observed.Add(clock.Now.TotalMilliseconds)));

        scheduler.AdvanceTo(new GameTime(400L));

        Assert.That(observed, Is.EqualTo(new long[] { 100, 200, 300 }));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(400L));
    }

    [Test]
    public void SameTime_UsesPriorityOrder()
    {
        var observed = new List<ScheduledEventPriority>();

        var reverseOrder = new[]
        {
            ScheduledEventPriority.WorkdayClosing,
            ScheduledEventPriority.Overdue,
            ScheduledEventPriority.TimerExpiry,
            ScheduledEventPriority.HrReview,
            ScheduledEventPriority.Delivery
        };

        foreach (ScheduledEventPriority priority in reverseOrder)
        {
            scheduler.Schedule(MakeEvent(
                priority.ToString(),
                100,
                () => observed.Add(priority),
                priority));
        }

        scheduler.AdvanceTo(new GameTime(100L));

        Assert.That(observed, Is.EqualTo(new[]
        {
            ScheduledEventPriority.Delivery,
            ScheduledEventPriority.HrReview,
            ScheduledEventPriority.TimerExpiry,
            ScheduledEventPriority.Overdue,
            ScheduledEventPriority.WorkdayClosing
        }));
    }

    [Test]
    public void EqualTimeAndPriority_PreserveRegistrationOrder()
    {
        var observed = new List<string>();

        scheduler.Schedule(MakeEvent(
            "z", 100, () => observed.Add("z")));
        scheduler.Schedule(MakeEvent(
            "a", 100, () => observed.Add("a")));
        scheduler.Schedule(MakeEvent(
            "m", 100, () => observed.Add("m")));

        Assert.That(scheduler.PeekNext().EventId, Is.EqualTo("z"));

        scheduler.AdvanceTo(new GameTime(100L));

        Assert.That(observed, Is.EqualTo(new[] { "z", "a", "m" }));
    }

    [Test]
    public void Cancel_RemovesOnlyRequestedPendingWork()
    {
        var observed = new List<string>();

        scheduler.Schedule(MakeEvent(
            "cancel", 100, () => observed.Add("cancel")));
        scheduler.Schedule(MakeEvent(
            "keep", 200, () => observed.Add("keep")));

        Assert.That(scheduler.Cancel("cancel"), Is.True);
        Assert.That(scheduler.Cancel("cancel"), Is.False);
        Assert.That(scheduler.Cancel("unknown"), Is.False);
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));
        Assert.That(scheduler.PeekNext().EventId, Is.EqualTo("keep"));

        scheduler.AdvanceTo(new GameTime(300L));

        Assert.That(observed, Is.EqualTo(new[] { "keep" }));
        Assert.That(scheduler.Cancel("keep"), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Cancel_RejectsBlankId(string id)
    {
        Assert.Throws<ArgumentException>(
            () => scheduler.Cancel(id));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void DuplicateId_IsRejectedAcrossEventLifetime(int state)
    {
        scheduler.Schedule(MakeEvent("review", 100));

        if (state == 1)
            scheduler.Cancel("review");
        else if (state == 2)
            scheduler.AdvanceTo(new GameTime(100L));

        Assert.Throws<ArgumentException>(
            () => scheduler.Schedule(MakeEvent("review", 200)));

        Assert.That(
            scheduler.PendingCount,
            Is.EqualTo(state == 0 ? 1 : 0));
    }

    [Test]
    public void EventIdentity_IsCaseSensitive()
    {
        scheduler.Schedule(MakeEvent("Review", 100));
        scheduler.Schedule(MakeEvent("review", 100));

        Assert.That(scheduler.Cancel("REVIEW"), Is.False);
        Assert.That(scheduler.Cancel("Review"), Is.True);
        Assert.That(scheduler.PeekNext().EventId, Is.EqualTo("review"));
    }

    [Test]
    public void RealElapsed_UsesSharedClockRate()
    {
        clock.SetRate(20);
        long observedTime = -1;

        scheduler.Schedule(MakeEvent(
            "review", 10000,
            () => observedTime = clock.Now.TotalMilliseconds));

        scheduler.AdvanceRealSeconds(1);

        Assert.That(observedTime, Is.EqualTo(10000L));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(20000L));
    }

    [Test]
    public void Pause_BlocksDispatchUntilResumed()
    {
        int calls = 0;
        scheduler.Schedule(MakeEvent("now", 0, () => calls++));

        clock.SetPaused(true);
        scheduler.AdvanceTo(new GameTime(100L));
        scheduler.AdvanceRealSeconds(1);

        Assert.That(calls, Is.Zero);
        Assert.That(clock.Now.TotalMilliseconds, Is.Zero);
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));

        clock.SetPaused(false);
        scheduler.AdvanceRealSeconds(0);

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(clock.Now.TotalMilliseconds, Is.Zero);
    }
}