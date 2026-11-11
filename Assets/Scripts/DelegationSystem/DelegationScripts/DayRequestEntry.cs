using System;
using UnityEngine;

[Serializable]
public sealed class DayRequestEntry
{
    [SerializeField] private string instanceId;
    [SerializeField] private RequestDefinition request;
    [SerializeField] private RequestProfileDefinition profile;

    [SerializeField, Range(0, 1439)]
    private int deliveryMinute;

    [SerializeField] private bool hasFixedWindow;

    [SerializeField, Range(0, 1439)]
    private int helpStartMinute;

    [SerializeField, Range(1, 1440)]
    private int helpEndMinute;

    [SerializeField] private bool useEarlierCutoff;

    [SerializeField, Range(0, 1439)]
    private int cutoffMinute;

    public DayRequestPlan ToPlan(int dayIndex)
    {
        if (request == null || profile == null)
            throw new InvalidOperationException("A day request requires request and profile references.");

        TimeRange? window = null;
        GameTime? cutoff = null;

        if (hasFixedWindow)
        {
            window = new TimeRange(dayIndex, helpStartMinute, helpEndMinute);

            if (useEarlierCutoff)
                cutoff = new GameTime(dayIndex, cutoffMinute);
        }

        return new DayRequestPlan(instanceId, request.ToData(), profile.ToData(), new GameTime(dayIndex, deliveryMinute), window, cutoff);
    }
}