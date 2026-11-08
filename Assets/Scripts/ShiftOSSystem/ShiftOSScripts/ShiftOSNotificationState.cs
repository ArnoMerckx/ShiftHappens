using System;
using System.Collections.Generic;

public sealed class ShiftOSNotificationState
{
    private readonly Queue<ShiftOSNotification> pending = new Queue<ShiftOSNotification>();

    public int PendingCount => pending.Count;

    public event Action Published;

    public void Publish(ShiftOSNotificationKind kind, string title, string body)
    {
        var notification = new ShiftOSNotification(kind, title, body);

        pending.Enqueue(notification);
        Published?.Invoke();
    }

    public bool TryTakeNext(out ShiftOSNotification notification)
    {
        if (pending.Count == 0)
        {
            notification = null;
            return false;
        }

        notification = pending.Dequeue();
        return true;
    }
}