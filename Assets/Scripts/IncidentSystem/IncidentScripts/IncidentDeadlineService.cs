using System;
using System.Collections.Generic;

public sealed class IncidentDeadlineService
{
    private readonly EventScheduler scheduler;
    private readonly WorkdayProgress progress;

    private readonly HashSet<string> scheduledIncidents = new HashSet<string>(StringComparer.Ordinal);

    public event Action<IncidentInstance> IncidentOverdue;

    public IncidentDeadlineService(DayDeliveryService deliveries, EventScheduler scheduler, WorkdayProgress progress)
    {
        if (deliveries == null)
            throw new ArgumentNullException(nameof(deliveries));

        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));

        if (progress == null)
            throw new ArgumentNullException(nameof(progress));

        foreach (var incident in deliveries.DeliveredIncidents)
        {
            if (incident.Status == IncidentStatus.Open && GetOverdueAt(incident).TotalMilliseconds < scheduler.Now.TotalMilliseconds)
            {
                throw new InvalidOperationException("Attach deadline processing before open incidents become overdue.");
            }
        }

        this.scheduler = scheduler;
        this.progress = progress;

        foreach (var incident in deliveries.DeliveredIncidents)
            ScheduleDeadline(incident);

        deliveries.IncidentDelivered += ScheduleDeadline;
    }

    private static GameTime GetOverdueAt(IncidentInstance incident)
    {
        var deadline = new GameTime(incident.DayIndex, incident.DeadlineMinute);

        return new GameTime(deadline.TotalMilliseconds + 1);
    }

    private void ScheduleDeadline(IncidentInstance incident)
    {
        if (incident.Status != IncidentStatus.Open || scheduledIncidents.Contains(incident.InstanceId))
        {
            return;
        }

        scheduler.Schedule(new ScheduledEvent($"overdue:{incident.InstanceId}", GetOverdueAt(incident), ScheduledEventPriority.Overdue, () => CheckDeadline(incident)));

        scheduledIncidents.Add(incident.InstanceId);
    }

    private void CheckDeadline(IncidentInstance incident)
    {
        if (!incident.TryMarkOverdue(scheduler.Now))
            return;

        if (progress.RecordMissedDeadline(incident))
            IncidentOverdue?.Invoke(incident);
    }
}