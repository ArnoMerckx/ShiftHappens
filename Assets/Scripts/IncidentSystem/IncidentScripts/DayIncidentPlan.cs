using System;
public sealed class DayIncidentPlan
{
    private readonly IncidentInstance prototype;
    private readonly string actualCulpritId;

    internal string ActualCulpritId => actualCulpritId;

    public IncidentScheduleEntry Schedule => prototype.Schedule;

    public DayIncidentPlan(IncidentDefinition definition, IncidentScheduleEntry schedule, string actualCulpritId = null)
    {
        if (actualCulpritId != null && string.IsNullOrWhiteSpace(actualCulpritId))
        {
            throw new ArgumentException("A provided culprit ID cannot be blank.", nameof(actualCulpritId));
        }

        prototype = new IncidentInstance(definition, schedule);
        this.actualCulpritId = actualCulpritId;
    }

    public IncidentInstance CreateInstance()
    {
        return new IncidentInstance(prototype);
    }
}