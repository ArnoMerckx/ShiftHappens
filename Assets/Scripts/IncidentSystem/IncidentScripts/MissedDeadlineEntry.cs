using System;

public sealed class MissedDeadlineEntry
{
    public string InstanceId { get; }
    public string IncidentId { get; }
    public GameTime DeadlineAt { get; }
    public GameTime RecordedAt { get; }
    public int CredibilityBefore { get; }
    public int CredibilityAfter { get; }

    public MissedDeadlineEntry(IncidentInstance incident, GameTime recordedAt, int credibilityBefore, int credibilityAfter)
    {
        if (incident == null)
            throw new ArgumentNullException(nameof(incident));

        if (incident.Status != IncidentStatus.Overdue)
            throw new ArgumentException("A missed deadline requires an overdue incident.", nameof(incident));

        var deadline = new GameTime(incident.DayIndex, incident.DeadlineMinute);

        if (recordedAt.TotalMilliseconds <= deadline.TotalMilliseconds)
            throw new ArgumentException("A missed deadline must be recorded after the deadline.", nameof(recordedAt));

        if (credibilityBefore < 0)
            throw new ArgumentOutOfRangeException(nameof(credibilityBefore));

        if (credibilityAfter < 0 || credibilityAfter > credibilityBefore)
            throw new ArgumentOutOfRangeException(nameof(credibilityAfter));

        InstanceId = incident.InstanceId;
        IncidentId = incident.IncidentId;
        DeadlineAt = deadline;
        RecordedAt = recordedAt;
        CredibilityBefore = credibilityBefore;
        CredibilityAfter = credibilityAfter;
    }
}