using NUnit.Framework;

public class WorkdayNotificationTests
{
    [Test]
    public void TimerExpiry_IsQueuedWithoutAViewAndDoesNotReplay()
    {
        var context = new WorkdaySessionTestContext();
        WorkdaySession session = context.Session;
        Assert.That(session.Timer.TryStart(60), Is.True);
        context.AdvanceTo(540, 59);
        Assert.That(session.Notifications.PendingCount, Is.Zero);
        context.AdvanceTo(541);
        Assert.That(session.Notifications.PendingCount, Is.EqualTo(1));
        Assert.That(session.Notifications.TryTakeNext(out ShiftOSNotification message), Is.True);
        Assert.That(message.Kind, Is.EqualTo(ShiftOSNotificationKind.TimerExpired));
        Assert.That(message.Title, Is.EqualTo("TickTock"));
        context.AdvanceTo(542);
        Assert.That(session.Notifications.PendingCount, Is.Zero);
        Assert.That(session.Notifications.TryTakeNext(out _), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Reset_CancelsNotificationForRunningOrPausedTimer(bool pause)
    {
        var context = new WorkdaySessionTestContext();
        Assert.That(context.Session.Timer.TryStart(60), Is.True);
        if (pause)
            Assert.That(context.Session.Timer.TryPause(), Is.True);
        Assert.That(context.Session.Timer.TryReset(), Is.True);
        context.AdvanceTo(542);
        Assert.That(context.Session.Notifications.PendingCount, Is.Zero);
    }

    [Test]
    public void SharedGamePause_DefersNotificationUntilResumed()
    {
        var context = new WorkdaySessionTestContext();
        WorkdaySession session = context.Session;
        Assert.That(session.Timer.TryStart(60), Is.True);
        session.SetPaused(true);
        session.AdvanceRealSeconds(100);
        Assert.That(session.Now.MinuteOfDay, Is.EqualTo(540));
        Assert.That(session.Notifications.PendingCount, Is.Zero);
        session.SetPaused(false);
        context.AdvanceTo(541);
        Assert.That(session.Notifications.PendingCount, Is.EqualTo(1));
    }

    [TestCase("bob", HrVerdict.Approved)]
    [TestCase("alice", HrVerdict.Rejected)]
    public void HrDecision_IsPublishedAfterDelayWithTheActualVerdict(
        string suspect, HrVerdict verdict)
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        IncidentInstance incident = context.Submit("D0I1", suspect, "E_SCHEDULE");
        Assert.That(context.Session.Notifications.PendingCount, Is.Zero);
        context.AdvanceTo(599, 59);
        Assert.That(context.Session.Notifications.PendingCount, Is.Zero);
        context.AdvanceTo(600);

        Assert.That(incident.Decision.Verdict, Is.EqualTo(verdict));
        Assert.That(context.Session.Notifications.PendingCount, Is.EqualTo(1));
        Assert.That(context.Session.Notifications.TryTakeNext(out ShiftOSNotification message), Is.True);
        Assert.That(message.Kind, Is.EqualTo(ShiftOSNotificationKind.HrDecision));
        Assert.That(message.Title, Does.Contain(incident.DisplayName));
        Assert.That(message.Body, Does.Contain(verdict.ToString().ToUpperInvariant()));
        Assert.That(context.Session.IsEvidenceDiscovered("E_SUPPLIES_RECEIPT"), Is.False);
        context.AdvanceTo(601);
        Assert.That(context.Session.Notifications.PendingCount, Is.Zero);
    }

    [Test]
    public void SimultaneousHrAndTimerEvents_AreBothRetainedInSchedulerOrder()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        context.Submit("D0I1", "bob", "E_SCHEDULE");
        Assert.That(context.Session.Timer.TryStart(600), Is.True);
        context.AdvanceTo(600);

        ShiftOSNotificationState state = context.Session.Notifications;
        Assert.That(state.PendingCount, Is.EqualTo(2));
        Assert.That(state.TryTakeNext(out ShiftOSNotification first), Is.True);
        Assert.That(state.TryTakeNext(out ShiftOSNotification second), Is.True);
        Assert.That(first.Kind, Is.EqualTo(ShiftOSNotificationKind.HrDecision));
        Assert.That(second.Kind, Is.EqualTo(ShiftOSNotificationKind.TimerExpired));
        Assert.That(state.TryTakeNext(out _), Is.False);
        context.AdvanceTo(601);
        Assert.That(state.PendingCount, Is.Zero);
    }

    [Test]
    public void SeparateWorkdays_DoNotShareTheirNotificationQueue()
    {
        var first = new WorkdaySessionTestContext();
        var second = new WorkdaySessionTestContext();
        Assert.That(first.Session.Timer.TryStart(60), Is.True);
        first.AdvanceTo(541);
        Assert.That(first.Session.Notifications.PendingCount, Is.EqualTo(1));
        Assert.That(second.Session.Notifications.PendingCount, Is.Zero);
        Assert.That(second.Session.Timer.State, Is.EqualTo(DurationTimerState.Idle));
    }
}
