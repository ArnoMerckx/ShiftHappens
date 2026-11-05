using System;
using UnityEngine;

[Serializable]
public sealed class DayIncidentEntry
{
    [SerializeField] private string instanceId;
    [SerializeField] private IncidentDefinition incident;

    [SerializeField, Range(0, 1439)]
    private int occurrenceStartMinute;

    [SerializeField, Range(1, 1440)]
    private int occurrenceEndMinute;

    [SerializeField, Range(0, 1439)]
    private int deliveryMinute;

    [SerializeField, Range(0, 1439)]
    private int deadlineMinute;

    public IncidentDefinition Definition => incident;

    public IncidentScheduleEntry ToSchedule(int dayIndex)
    {
        if (incident == null)
        {
            throw new InvalidOperationException(
                "A day incident entry requires an incident reference.");
        }

        return new IncidentScheduleEntry(instanceId, incident.IncidentId, new TimeRange(dayIndex, occurrenceStartMinute, occurrenceEndMinute), deliveryMinute, deadlineMinute);
    }
}