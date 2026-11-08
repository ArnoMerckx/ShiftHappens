using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ShiftOSNotificationStateTests
{
    [TestCase(ShiftOSNotificationKind.TimerExpired)]
    [TestCase(ShiftOSNotificationKind.HrDecision)]
    public void Message_RetainsItsValidatedContent(ShiftOSNotificationKind kind)
    {
        var message = new ShiftOSNotification(kind, "Title", "Body");
        Assert.That(message.Kind, Is.EqualTo(kind));
        Assert.That(message.Title, Is.EqualTo("Title"));
        Assert.That(message.Body, Is.EqualTo("Body"));
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void UnknownKind_IsRejected(int kind)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShiftOSNotification((ShiftOSNotificationKind)kind, "Title", "Body"));
    }

    [TestCase(null, "Body")]
    [TestCase("", "Body")]
    [TestCase(" ", "Body")]
    [TestCase("Title", null)]
    [TestCase("Title", "")]
    [TestCase("Title", " ")]
    public void InvalidMessage_DoesNotChangeQueueOrNotify(string title, string body)
    {
        var state = new ShiftOSNotificationState();
        state.Publish(ShiftOSNotificationKind.TimerExpired, "Existing", "Keep me");
        int calls = 0;
        state.Published += () => calls++;

        Assert.Throws<ArgumentException>(() =>
            state.Publish(ShiftOSNotificationKind.HrDecision, title, body));
        Assert.That(state.PendingCount, Is.EqualTo(1));
        Assert.That(calls, Is.Zero);
        Assert.That(state.TryTakeNext(out ShiftOSNotification kept), Is.True);
        Assert.That(kept.Title, Is.EqualTo("Existing"));
    }

    [Test]
    public void EmptyQueue_ReturnsFalseAndNullRepeatedly()
    {
        var state = new ShiftOSNotificationState();
        for (int i = 0; i < 3; i++)
        {
            Assert.That(state.TryTakeNext(out ShiftOSNotification message), Is.False);
            Assert.That(message, Is.Null);
            Assert.That(state.PendingCount, Is.Zero);
        }
    }

    [Test]
    public void MessagesPublishedWithoutAView_AreConsumedOnceInArrivalOrder()
    {
        var state = new ShiftOSNotificationState();
        state.Publish(ShiftOSNotificationKind.TimerExpired, "First", "Timer");
        state.Publish(ShiftOSNotificationKind.HrDecision, "Second", "HR");
        state.Publish(ShiftOSNotificationKind.TimerExpired, "Third", "Timer again");
        Assert.That(state.PendingCount, Is.EqualTo(3));

        var titles = new List<string>();
        while (state.TryTakeNext(out ShiftOSNotification message))
            titles.Add(message.Title);

        CollectionAssert.AreEqual(new[] { "First", "Second", "Third" }, titles);
        Assert.That(state.PendingCount, Is.Zero);
        Assert.That(state.TryTakeNext(out _), Is.False);
    }

    [Test]
    public void PublishedCallback_CanImmediatelyConsumeTheStoredMessage()
    {
        var state = new ShiftOSNotificationState();
        ShiftOSNotification received = null;
        int calls = 0;
        state.Published += () =>
        {
            calls++;
            Assert.That(state.PendingCount, Is.EqualTo(1));
            Assert.That(state.TryTakeNext(out received), Is.True);
        };

        state.Publish(ShiftOSNotificationKind.TimerExpired, "TickTock", "Finished");
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(received.Title, Is.EqualTo("TickTock"));
        Assert.That(state.PendingCount, Is.Zero);
    }

    [Test]
    public void ReattachedListener_DoesNotReplayConsumedMessages()
    {
        var state = new ShiftOSNotificationState();
        int calls = 0;
        Action handler = () => calls++;
        state.Published += handler;
        state.Publish(ShiftOSNotificationKind.TimerExpired, "First", "Done");
        Assert.That(state.TryTakeNext(out _), Is.True);
        state.Published -= handler;
        state.Publish(ShiftOSNotificationKind.HrDecision, "While hidden", "Approved");
        state.Published += handler;

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(state.TryTakeNext(out ShiftOSNotification pending), Is.True);
        Assert.That(pending.Title, Is.EqualTo("While hidden"));
        Assert.That(state.TryTakeNext(out _), Is.False);
        state.Publish(ShiftOSNotificationKind.TimerExpired, "New", "Done");
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void SubscriberFailure_DoesNotRemoveTheQueuedMessage()
    {
        var state = new ShiftOSNotificationState();
        state.Published += () => throw new InvalidOperationException("Subscriber");
        Assert.Throws<InvalidOperationException>(() =>
            state.Publish(ShiftOSNotificationKind.TimerExpired, "Kept", "Finished"));
        Assert.That(state.PendingCount, Is.EqualTo(1));
        Assert.That(state.TryTakeNext(out ShiftOSNotification message), Is.True);
        Assert.That(message.Title, Is.EqualTo("Kept"));
    }

    [Test]
    public void SeparateQueues_DoNotShareMessages()
    {
        var first = new ShiftOSNotificationState();
        var second = new ShiftOSNotificationState();
        first.Publish(ShiftOSNotificationKind.TimerExpired, "First", "Only here");
        Assert.That(second.PendingCount, Is.Zero);
        Assert.That(second.TryTakeNext(out _), Is.False);
        Assert.That(first.PendingCount, Is.EqualTo(1));
    }
}
