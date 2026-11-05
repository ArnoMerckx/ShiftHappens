using System;
using System.Collections.Generic;

public sealed class WorkdaySummary
{
    public WorkdaySettings Settings { get; }
    public WorkdayState FinalState { get; }
    public GameTime EndedAt { get; }
    public int Credibility { get; }

    public IReadOnlyList<IncidentSummary> Incidents { get; }
    public IReadOnlyList<ReviewHistoryEntry> History { get; }
    public IReadOnlyList<MissedDeadlineEntry> MissedDeadlines { get; }

    public WorkdaySummary(WorkdaySettings settings, WorkdayState finalState, GameTime endedAt, int credibility, List<IncidentInstance> incidents, List<ReviewHistoryEntry> history, List<MissedDeadlineEntry> missedDeadlines)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (finalState != WorkdayState.Completed && finalState != WorkdayState.Fired)
        {
            throw new ArgumentException("A summary requires a terminal workday state.", nameof(finalState));
        }

        if (endedAt.TotalMilliseconds < settings.OpensAt.TotalMilliseconds || (finalState == WorkdayState.Completed && endedAt.TotalMilliseconds < settings.ClosesAt.TotalMilliseconds))
        {
            throw new ArgumentOutOfRangeException(nameof(endedAt));
        }

        if (credibility < 0)
            throw new ArgumentOutOfRangeException(nameof(credibility));

        if (incidents == null)
            throw new ArgumentNullException(nameof(incidents));

        if (history == null)
            throw new ArgumentNullException(nameof(history));

        if (missedDeadlines == null)
            throw new ArgumentNullException(nameof(missedDeadlines));

        var incidentCopy = new List<IncidentSummary>();
        var incidentIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var incident in incidents)
        {
            if (incident == null)
                throw new ArgumentException("Summary incidents cannot contain null.", nameof(incidents));

            var snapshot = new IncidentSummary(incident);

            if (snapshot.DayIndex != settings.DayIndex || !incidentIds.Add(snapshot.InstanceId))
            {
                throw new ArgumentException("Summary incidents require unique IDs and the matching day.", nameof(incidents));
            }

            if (finalState == WorkdayState.Completed && (snapshot.Status == IncidentStatus.Open || snapshot.Status == IncidentStatus.Pending))
            {
                throw new ArgumentException("A completed workday cannot contain unresolved incidents.", nameof(incidents));
            }

            incidentCopy.Add(snapshot);
        }

        var historyCopy = new List<ReviewHistoryEntry>();
        var missedCopy = new List<MissedDeadlineEntry>();
        var outcomeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in history)
        {
            if (entry == null || !incidentIds.Contains(entry.Report.IncidentInstanceId) || !outcomeIds.Add(entry.Report.IncidentInstanceId) || entry.ReviewedAt.TotalMilliseconds > endedAt.TotalMilliseconds)
            {
                throw new ArgumentException("Review history requires unique delivered outcomes recorded by day end.", nameof(history));
            }

            historyCopy.Add(entry);
        }

        foreach (var entry in missedDeadlines)
        {
            if (entry == null || !incidentIds.Contains(entry.InstanceId) || !outcomeIds.Add(entry.InstanceId) || entry.RecordedAt.TotalMilliseconds > endedAt.TotalMilliseconds)
            {
                throw new ArgumentException("Missed deadlines require unique delivered outcomes recorded by day end.", nameof(missedDeadlines));
            }

            missedCopy.Add(entry);
        }

        Settings = settings;
        FinalState = finalState;
        EndedAt = endedAt;
        Credibility = credibility;

        Incidents = incidentCopy.AsReadOnly();
        History = historyCopy.AsReadOnly();
        MissedDeadlines = missedCopy.AsReadOnly();
    }
}