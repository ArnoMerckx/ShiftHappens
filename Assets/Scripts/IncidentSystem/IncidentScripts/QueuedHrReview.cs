using System;

public sealed class QueuedHrReview
{
    public IncidentInstance Incident { get; }
    public IncidentReport Report { get; }
    public GameTime SubmittedAt { get; }
    public GameTime ReviewAt { get; }

    public QueuedHrReview(IncidentInstance incident, IncidentReport report, GameTime submittedAt)
    {
        if (incident == null)
            throw new ArgumentNullException(nameof(incident));

        if (report == null)
            throw new ArgumentNullException(nameof(report));

        if (report.IncidentInstanceId != incident.InstanceId)
        {
            throw new ArgumentException("The report must belong to this incident.", nameof(report));
        }

        if (report.SubmittedDayIndex != submittedAt.DayIndex || report.SubmittedMinute != submittedAt.MinuteOfDay)
        {
            throw new ArgumentException("The report timestamp must match the precise submission time.", nameof(submittedAt));
        }

        ReviewAt = new GameTime(submittedAt.TotalMilliseconds + (long)incident.ReviewDelayMinutes * GameTime.MillisecondsPerMinute);

        Incident = incident;
        Report = report;
        SubmittedAt = submittedAt;
    }
}