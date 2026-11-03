using System;

public sealed class IncidentScheduleEntry
{
    public string InstanceId { get; }
    public string IncidentId { get; }
    public TimeRange Occurrence { get; }
    public int DayIndex => Occurrence.DayIndex;
    public int DeliveryMinute { get; }
    public int DeadlineMinute { get; }

    public IncidentScheduleEntry(string instanceId, string incidentId, TimeRange occurrence, int deliveryMinute, int deadlineMinute)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("Instance ID is required.", nameof(instanceId));

        if (string.IsNullOrWhiteSpace(incidentId))
            throw new ArgumentException("Incident ID is required.", nameof(incidentId));

        if (occurrence.StartMinute >= occurrence.EndMinute)
            throw new ArgumentException("A nonempty occurrence window is required.", nameof(occurrence));

        if (deliveryMinute < 0 || deliveryMinute >= 1440)
            throw new ArgumentOutOfRangeException(nameof(deliveryMinute));

        if (deadlineMinute < 0 || deadlineMinute >= 1440)
            throw new ArgumentOutOfRangeException(nameof(deadlineMinute));

        if (deliveryMinute < occurrence.EndMinute)
            throw new ArgumentException("Delivery cannot precede the end of the occurrence window.", nameof(deliveryMinute));

        if (deadlineMinute <= deliveryMinute)
            throw new ArgumentException("The deadline must be later than delivery.", nameof(deadlineMinute));

        InstanceId = instanceId;
        IncidentId = incidentId;
        Occurrence = occurrence;
        DeliveryMinute = deliveryMinute;
        DeadlineMinute = deadlineMinute;
    }
}