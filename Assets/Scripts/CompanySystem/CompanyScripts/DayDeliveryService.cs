using System;
using System.Collections.Generic;

public sealed class DayDeliveryService
{
    private readonly List<IncidentInstance> delivered =
        new List<IncidentInstance>();

    private readonly Dictionary<string, IncidentInstance> deliveredById = new Dictionary<string, IncidentInstance>(StringComparer.Ordinal);

    private readonly HashSet<string> notifiedEvidenceIds = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyList<IncidentInstance> DeliveredIncidents { get; }

    public event Action<IncidentInstance> IncidentDelivered;
    public event Action<string> EvidenceAvailable;

    public DayDeliveryService(DayData day, EventScheduler scheduler)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));

        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));

        WorkdaySettings settings = day.ScheduleData.Settings;

        if (scheduler.Now.TotalMilliseconds != settings.OpensAt.TotalMilliseconds)
        {
            throw new ArgumentException("Delivery must be configured at workday opening.", nameof(scheduler));
        }

        DeliveredIncidents = delivered.AsReadOnly();

        foreach (var document in day.EvidenceDatabase.All)
        {
            string evidenceId = document.EvidenceId;

            var availableAt = new GameTime(document.AvailableDayIndex, document.AvailableMinute);

            if (availableAt.TotalMilliseconds < settings.OpensAt.TotalMilliseconds)
            {
                availableAt = settings.OpensAt;
            }

            if (availableAt.TotalMilliseconds >= settings.ClosesAt.TotalMilliseconds)
            {
                continue;
            }

            scheduler.Schedule(new ScheduledEvent($"delivery:{settings.DayId}:evidence:{evidenceId}", availableAt, ScheduledEventPriority.Delivery, () => NotifyEvidenceAvailable(evidenceId)));
        }

        foreach (var plan in day.Incidents)
        {
            IncidentScheduleEntry schedule = plan.Schedule;

            scheduler.Schedule(new ScheduledEvent($"delivery:{settings.DayId}:incident:{schedule.InstanceId}", new GameTime(schedule.DayIndex, schedule.DeliveryMinute), ScheduledEventPriority.Delivery, () => DeliverIncident(plan)));
        }
    }

    public IncidentInstance GetDelivered(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Incident instance ID is required.", nameof(instanceId));
        }

        if (!deliveredById.TryGetValue(instanceId, out var instance))
        {
            throw new KeyNotFoundException($"No delivered incident with ID: {instanceId}");
        }

        return instance;
    }

    private void NotifyEvidenceAvailable(string evidenceId)
    {
        if (notifiedEvidenceIds.Add(evidenceId))
            EvidenceAvailable?.Invoke(evidenceId);
    }

    private void DeliverIncident(DayIncidentPlan plan)
    {
        if (deliveredById.ContainsKey(plan.Schedule.InstanceId))
            return;

        IncidentInstance instance = plan.CreateInstance();

        deliveredById.Add(instance.InstanceId, instance);
        delivered.Add(instance);

        IncidentDelivered?.Invoke(instance);
    }
}