using System;
using System.Collections.Generic;

public sealed class SwarmsReplyFeed
{
    private readonly WorkdaySettings settings;
    private readonly EventScheduler scheduler;
    private readonly WorkdayController workday;
    private readonly HashSet<string> employeeIds;

    private readonly HashSet<string> acceptedReplyIds = new HashSet<string>(StringComparer.Ordinal);

    private readonly Dictionary<string, SwarmsReplyData> pending = new Dictionary<string, SwarmsReplyData>(StringComparer.Ordinal);

    private readonly List<SwarmsReplyData> delivered = new List<SwarmsReplyData>();

    public IReadOnlyList<SwarmsReplyData> DeliveredReplies { get; }
    public int PendingCount => pending.Count;

    public event Action<SwarmsReplyData> ReplyDelivered;

    public SwarmsReplyFeed(DayData day, EventScheduler scheduler, WorkdayController workday)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));

        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

        this.workday = workday ?? throw new ArgumentNullException(nameof(workday));

        settings = day.ScheduleData.Settings;

        if (scheduler.IsStopped || scheduler.Now.TotalMilliseconds != settings.OpensAt.TotalMilliseconds || workday.State != WorkdayState.Working)
        {
            throw new ArgumentException("Create the reply feed at opening on a running workday.");
        }

        employeeIds = new HashSet<string>(day.ScheduleData.EmployeeIds, StringComparer.Ordinal);

        DeliveredReplies = delivered.AsReadOnly();

        workday.StateChanged += OnWorkdayStateChanged;
    }

    public bool TryQueue(SwarmsReplyData reply)
    {
        if (reply == null)
            throw new ArgumentNullException(nameof(reply));

        if (scheduler.IsStopped ||workday.State != WorkdayState.Working)
        {
            return false;
        }

        if (!string.Equals(reply.DayId, settings.DayId, StringComparison.Ordinal) || !employeeIds.Contains(reply.SenderEmployeeId) || reply.DueAt.TotalMilliseconds < scheduler.Now.TotalMilliseconds ||
            reply.DueAt.TotalMilliseconds > settings.ClosesAt.TotalMilliseconds || acceptedReplyIds.Contains(reply.ReplyId))
        {
            return false;
        }

        scheduler.Schedule(new ScheduledEvent(GetEventId(reply), reply.DueAt, ScheduledEventPriority.Delivery, () => Deliver(reply)));

        acceptedReplyIds.Add(reply.ReplyId);
        pending.Add(reply.ReplyId, reply);

        return true;
    }

    private void Deliver(SwarmsReplyData reply)
    {
        if (!pending.TryGetValue(reply.ReplyId, out SwarmsReplyData stored) || !ReferenceEquals(stored, reply))
        {
            return;
        }

        if (scheduler.IsStopped || workday.State != WorkdayState.Working || scheduler.Now.TotalMilliseconds < reply.DueAt.TotalMilliseconds || scheduler.Now.TotalMilliseconds > settings.ClosesAt.TotalMilliseconds)
        {
            return;
        }

        pending.Remove(reply.ReplyId);
        delivered.Add(reply);

        ReplyDelivered?.Invoke(reply);
    }

    private void OnWorkdayStateChanged()
    {
        if (workday.State == WorkdayState.Working)
            return;

        foreach (SwarmsReplyData reply in pending.Values)
            scheduler.Cancel(GetEventId(reply));

        pending.Clear();
    }

    private static string GetEventId(SwarmsReplyData reply)
    {
        return "swarms-reply:" + reply.ReplyId;
    }
}