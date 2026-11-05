public sealed class DayIncidentPlan
{
    private readonly IncidentInstance prototype;

    public IncidentScheduleEntry Schedule => prototype.Schedule;

    public DayIncidentPlan(IncidentDefinition definition, IncidentScheduleEntry schedule)
    {
        prototype = new IncidentInstance(definition, schedule);
    }

    public IncidentInstance CreateInstance()
    {
        return new IncidentInstance(prototype);
    }
}