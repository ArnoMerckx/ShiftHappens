using System;
using System.Collections.Generic;
using NUnit.Framework;

public class EventSchedulerAdvancementTests
{
    private GameClock clock;
    private EventScheduler scheduler;

    [SetUp]
    public void SetUp()
    {
        clock = new GameClock(default(GameTime), 1);
        scheduler = new EventScheduler(clock);
    }

    private void Schedule(
        string id,
        long due,
        Action callback,
        ScheduledEventPriority priority =
            ScheduledEventPriority.HrReview)
    {
        scheduler.Schedule(new ScheduledEvent(
            id, new GameTime(due), priority, callback));
    }

    [Test]
    public void MultiDayJump_VisitsEachDueInstantOnce()
    {
        var observed = new List<long>();

        Schedule("last", 172799999L,
            () => observed.Add(clock.Now.TotalMilliseconds));
        Schedule("first", 100L,
            () => observed.Add(clock.Now.TotalMilliseconds));
        Schedule("middle", 86400100L,
            () => observed.Add(clock.Now.TotalMilliseconds));

        scheduler.AdvanceTo(new GameTime(172800000L));
        scheduler.AdvanceTo(new GameTime(172800000L));

        Assert.That(observed, Is.EqualTo(new long[]
        {
            100L, 86400100L, 172799999L
        }));
        Assert.That(clock.Now.DayIndex, Is.EqualTo(2));
        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [Test]
    public void Callback_CanScheduleAtNowUsingPendingPriorityOrder()
    {
        var observed = new List<string>();

        Schedule("parent", 100, () =>
        {
            observed.Add("parent");

            Schedule("new-delivery", 100,
                () => observed.Add("delivery"),
                ScheduledEventPriority.Delivery);
        });

        Schedule("existing-review", 100,
            () => observed.Add("review"));
        Schedule("timer", 100,
            () => observed.Add("timer"),
            ScheduledEventPriority.TimerExpiry);

        scheduler.AdvanceTo(new GameTime(100L));

        Assert.That(observed, Is.EqualTo(new[]
        {
            "parent", "delivery", "review", "timer"
        }));
    }

    [Test]
    public void Callback_AddedEventsRespectCurrentJumpTarget()
    {
        var observed = new List<long>();

        Schedule("parent", 100, () =>
        {
            observed.Add(clock.Now.TotalMilliseconds);

            Schedule("inside", 200,
                () => observed.Add(clock.Now.TotalMilliseconds));
            Schedule("outside", 400,
                () => observed.Add(clock.Now.TotalMilliseconds));
        });

        Schedule("existing", 250,
            () => observed.Add(clock.Now.TotalMilliseconds));

        scheduler.AdvanceTo(new GameTime(300L));

        Assert.That(observed, Is.EqualTo(new long[] { 100, 200, 250 }));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(300L));
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));
        Assert.That(scheduler.PeekNext().EventId, Is.EqualTo("outside"));

        scheduler.AdvanceTo(new GameTime(400L));

        Assert.That(
            observed,
            Is.EqualTo(new long[] { 100, 200, 250, 400 }));
    }

    [Test]
    public void Callback_CanCancelAnotherDueEvent()
    {
        int canceledCalls = 0;

        Schedule("cancel-first", 100, () =>
        {
            Assert.That(scheduler.Cancel("cancel-me"), Is.True);
        });

        Schedule("cancel-me", 100, () => canceledCalls++);

        scheduler.AdvanceTo(new GameTime(200L));

        Assert.That(canceledCalls, Is.Zero);
        Assert.That(scheduler.PendingCount, Is.Zero);
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(200L));
    }

    [Test]
    public void CallbackPause_StopsJumpAndDiscardsRemainingElapsed()
    {
        int laterCalls = 0;

        Schedule("pause", 100, () => clock.SetPaused(true));
        Schedule("later", 100, () => laterCalls++);

        scheduler.AdvanceRealSeconds(1);

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(100L));
        Assert.That(laterCalls, Is.Zero);
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));

        clock.SetPaused(false);
        scheduler.AdvanceRealSeconds(0.05);

        Assert.That(laterCalls, Is.EqualTo(1));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(150L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NestedAdvancement_IsRejectedBeforeConsumingElapsed(
        bool useRealSeconds)
    {
        Schedule("parent", 1, () =>
        {
            if (useRealSeconds)
            {
                Assert.Throws<InvalidOperationException>(
                    () => scheduler.AdvanceRealSeconds(0.0005));
            }
            else
            {
                Assert.Throws<InvalidOperationException>(
                    () => scheduler.AdvanceTo(new GameTime(2L)));
            }
        });

        scheduler.AdvanceTo(new GameTime(2L));

        scheduler.AdvanceRealSeconds(0.0005);
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(2L));

        scheduler.AdvanceRealSeconds(0.0005);
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(3L));
    }

    [Test]
    public void CallbackException_DoesNotReplayAndReleasesGuard()
    {
        int failedCalls = 0;
        int laterCalls = 0;
        var failure = new InvalidOperationException("Test failure.");

        Schedule("fails", 100, () =>
        {
            failedCalls++;
            throw failure;
        });

        Schedule("later", 200, () => laterCalls++);

        var caught = Assert.Throws<InvalidOperationException>(
            () => scheduler.AdvanceTo(new GameTime(300L)));

        Assert.That(caught, Is.SameAs(failure));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(100L));
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));
        Assert.That(laterCalls, Is.Zero);

        Assert.Throws<ArgumentException>(
            () => Schedule("fails", 300, () => { }));

        scheduler.AdvanceTo(new GameTime(300L));

        Assert.That(failedCalls, Is.EqualTo(1));
        Assert.That(laterCalls, Is.EqualTo(1));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(300L));
        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [Test]
    public void BackwardTarget_IsRejectedWithoutChangingPendingWork()
    {
        scheduler.AdvanceTo(new GameTime(100L));

        int calls = 0;
        Schedule("later", 200, () => calls++);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => scheduler.AdvanceTo(new GameTime(99L)));

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(100L));
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));
        Assert.That(calls, Is.Zero);

        scheduler.AdvanceTo(new GameTime(200L));

        Assert.That(calls, Is.EqualTo(1));
    }

    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void InvalidElapsed_DoesNotDispatchOrLeaveGuardActive(
        double elapsed)
    {
        int calls = 0;
        Schedule("now", 0, () => calls++);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => scheduler.AdvanceRealSeconds(elapsed));

        Assert.That(clock.Now.TotalMilliseconds, Is.Zero);
        Assert.That(calls, Is.Zero);
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));

        scheduler.AdvanceRealSeconds(0);

        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void CallbackRateChange_AffectsNextElapsedConversion()
    {
        Schedule("change-rate", 100, () => clock.SetRate(2));

        scheduler.AdvanceRealSeconds(0.2);

        Assert.That(clock.GameSecondsPerRealSecond, Is.EqualTo(2));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(200L));

        scheduler.AdvanceRealSeconds(0.1);

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(400L));
    }

    [Test]
    public void FractionalElapsed_EventFiresWhenWholeMillisecondArrives()
    {
        int calls = 0;
        Schedule("one-millisecond", 1, () => calls++);

        scheduler.AdvanceRealSeconds(0.0005);

        Assert.That(calls, Is.Zero);
        Assert.That(clock.Now.TotalMilliseconds, Is.Zero);

        scheduler.AdvanceRealSeconds(0.0005);
        scheduler.AdvanceRealSeconds(0);

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(1L));
    }
}