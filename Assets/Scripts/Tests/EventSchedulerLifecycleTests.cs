using System;
using System.Collections.Generic;
using NUnit.Framework;

public class EventSchedulerLifecycleTests
{
    private GameClock clock;
    private EventScheduler scheduler;

    [SetUp]
    public void SetUp()
    {
        clock = new GameClock(default(GameTime), 1);
        scheduler = new EventScheduler(clock);
    }

    private void Schedule(string id, long at, Action callback,
        ScheduledEventPriority priority = ScheduledEventPriority.HrReview)
    {
        scheduler.Schedule(new ScheduledEvent(id, new GameTime(at), priority, callback));
    }

    [Test]
    public void Stop_ClearsQueueAndIsIdempotent()
    {
        Schedule("pending", 100, () => { });
        scheduler.Stop();
        scheduler.Stop();
        Assert.That(scheduler.IsStopped, Is.True);
        Assert.That(scheduler.PendingCount, Is.Zero);
        Assert.That(scheduler.PeekNext(), Is.Null);
        Assert.That(scheduler.Cancel("pending"), Is.False);
        Assert.That(clock.IsPaused, Is.False);
    }

    [Test]
    public void StopInsideCallback_PreventsSameTimeLaterWorkAndFinalTargetJump()
    {
        var calls = new List<string>();
        int notifications = 0;
        Schedule("stop", 100, () => { calls.Add("stop"); scheduler.Stop(); });
        Schedule("same_time", 100, () => calls.Add("same"));
        Schedule("later", 200, () => calls.Add("later"));
        scheduler.EventDispatched += item =>
        {
            notifications++;
            Assert.That(item.EventId, Is.EqualTo("stop"));
            Assert.That(scheduler.IsStopped, Is.True);
            Assert.That(scheduler.PendingCount, Is.Zero);
        };

        scheduler.AdvanceTo(new GameTime(500L));

        Assert.That(calls, Is.EqualTo(new[] { "stop" }));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(100L));
        Assert.That(notifications, Is.EqualTo(1));
    }

    [Test]
    public void StoppedScheduler_RejectsNewWork()
    {
        scheduler.Stop();
        Assert.Throws<InvalidOperationException>(() => Schedule("new", 100, () => { }));
    }

    [Test]
    public void StoppedScheduler_IgnoresAdvancementEvenAfterUnpausing()
    {
        scheduler.AdvanceTo(new GameTime(100L));
        scheduler.Stop();
        clock.SetPaused(true);
        clock.SetPaused(false);
        scheduler.AdvanceRealSeconds(100);
        scheduler.AdvanceTo(new GameTime(500L));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(100L));
        Assert.That(scheduler.IsStopped, Is.True);
    }

    [Test]
    public void StoppedScheduler_StillRejectsBackwardTarget()
    {
        scheduler.AdvanceTo(new GameTime(100L));
        scheduler.Stop();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => scheduler.AdvanceTo(new GameTime(99L)));
    }

    [Test]
    public void Restriction_RejectsUndefinedPriority()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => scheduler.RestrictToPriority((ScheduledEventPriority)999));
        Schedule("still_allowed", 100, () => { }, ScheduledEventPriority.Delivery);
    }

    [Test]
    public void Restriction_RetainsOnlyMatchingQueuedWorkAndAllowsMoreOfSamePriority()
    {
        var calls = new List<string>();
        foreach (ScheduledEventPriority priority in Enum.GetValues(typeof(ScheduledEventPriority)))
            Schedule(priority.ToString(), 100, () => calls.Add(priority.ToString()), priority);

        scheduler.RestrictToPriority(ScheduledEventPriority.HrReview);
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));
        Schedule("more_hr", 200, () => calls.Add("more"));
        scheduler.AdvanceTo(new GameTime(300L));

        Assert.That(calls, Is.EqualTo(new[] { "HrReview", "more" }));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(300L));
    }

    [TestCase(ScheduledEventPriority.Delivery)]
    [TestCase(ScheduledEventPriority.TimerExpiry)]
    [TestCase(ScheduledEventPriority.Overdue)]
    [TestCase(ScheduledEventPriority.WorkdayClosing)]
    public void Restriction_RejectsOtherPrioritiesWithoutReservingFailedId(
        ScheduledEventPriority priority)
    {
        scheduler.RestrictToPriority(ScheduledEventPriority.HrReview);
        Assert.Throws<InvalidOperationException>(() => Schedule("new", 100, () => { }, priority));
        Schedule("new", 100, () => { });
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));
    }

    [Test]
    public void SameRestriction_IsIdempotent()
    {
        Schedule("review", 100, () => { });
        scheduler.RestrictToPriority(ScheduledEventPriority.HrReview);
        scheduler.RestrictToPriority(ScheduledEventPriority.HrReview);
        Assert.That(scheduler.PendingCount, Is.EqualTo(1));
    }

    [Test]
    public void Restriction_CannotBeChanged()
    {
        scheduler.RestrictToPriority(ScheduledEventPriority.HrReview);
        Assert.Throws<InvalidOperationException>(
            () => scheduler.RestrictToPriority(ScheduledEventPriority.Delivery));
        Schedule("review", 100, () => { });
    }

    [Test]
    public void Restriction_CannotConfigureStoppedScheduler()
    {
        scheduler.Stop();
        Assert.Throws<InvalidOperationException>(
            () => scheduler.RestrictToPriority(ScheduledEventPriority.HrReview));
    }

    [Test]
    public void PostDispatch_ObservesCompletedCallbackAtItsDueTime()
    {
        var order = new List<string>();
        var scheduled = new ScheduledEvent("event", new GameTime(100L),
            ScheduledEventPriority.HrReview, () => order.Add("callback"));
        scheduler.Schedule(scheduled);
        scheduler.EventDispatched += item =>
        {
            Assert.That(item, Is.SameAs(scheduled));
            Assert.That(order, Is.EqualTo(new[] { "callback" }));
            Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(100L));
            order.Add("notification");
        };

        scheduler.AdvanceTo(new GameTime(200L));
        Assert.That(order, Is.EqualTo(new[] { "callback", "notification" }));
    }

    [Test]
    public void CallbackException_DoesNotPublishSuccessfulDispatchAndReleasesGuard()
    {
        var observed = new List<string>();
        Schedule("fails", 100, () => { throw new InvalidOperationException("Test callback failure."); });
        Schedule("later", 200, () => { });
        scheduler.EventDispatched += item => observed.Add(item.EventId);

        Assert.Throws<InvalidOperationException>(() => scheduler.AdvanceTo(new GameTime(300L)));
        Assert.That(observed, Is.Empty);
        scheduler.AdvanceTo(new GameTime(300L));
        Assert.That(observed, Is.EqualTo(new[] { "later" }));
    }

    [Test]
    public void PostDispatchObserver_CanStopBeforeNextEvent()
    {
        int callbacks = 0;
        Schedule("first", 100, () => callbacks++);
        Schedule("second", 100, () => callbacks++);
        scheduler.EventDispatched += item => scheduler.Stop();

        scheduler.AdvanceTo(new GameTime(500L));

        Assert.That(callbacks, Is.EqualTo(1));
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(100L));
        Assert.That(scheduler.PendingCount, Is.Zero);
    }
}
