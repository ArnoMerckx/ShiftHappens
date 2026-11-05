using System;

public sealed class IncidentInstance
{
    public IncidentScheduleEntry Schedule { get; }

    public string InstanceId => Schedule.InstanceId;
    public string IncidentId => Schedule.IncidentId;
    public TimeRange Occurrence => Schedule.Occurrence;
    public int DayIndex => Schedule.DayIndex;
    public int DeliveryMinute => Schedule.DeliveryMinute;
    public int DeadlineMinute => Schedule.DeadlineMinute;

    public string DisplayName { get; }
    public string Description { get; }
    public string LocationId { get; }
    public string RelatedItemId { get; }
    public int ReviewDelayMinutes { get; }

    public IncidentStatus Status { get; private set; }

    public IncidentReport SubmittedReport { get; private set; }

    public HrEvaluationResult Decision { get; private set; }

    public IncidentInstance(IncidentDefinition definition, IncidentScheduleEntry schedule)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (schedule == null)
            throw new ArgumentNullException(nameof(schedule));

        if (definition.IncidentId != schedule.IncidentId)
            throw new ArgumentException("The template ID must match the schedule entry.", nameof(definition));

        if (string.IsNullOrWhiteSpace(definition.DisplayName) || string.IsNullOrWhiteSpace(definition.Description))
        {
            throw new ArgumentException("The incident template requires a title and description.", nameof(definition));
        }

        if (definition.Location == null || string.IsNullOrWhiteSpace(definition.Location.LocationId))
        {
            throw new ArgumentException("The incident template requires a valid location.", nameof(definition));
        }

        if (definition.ReviewDelayMinutes <= 0)
            throw new ArgumentException("Review delay must be positive.", nameof(definition));

        if (!string.IsNullOrEmpty(definition.RelatedItemId) && string.IsNullOrWhiteSpace(definition.RelatedItemId))
        {
            throw new ArgumentException("An optional item ID cannot contain only whitespace.", nameof(definition));
        }

        Schedule = schedule;
        DisplayName = definition.DisplayName;
        Description = definition.Description;
        LocationId = definition.Location.LocationId;
        RelatedItemId = definition.RelatedItemId ?? string.Empty;
        ReviewDelayMinutes = definition.ReviewDelayMinutes;

        Status = IncidentStatus.Open;
    }

    internal IncidentInstance(IncidentInstance source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        Schedule = source.Schedule;
        DisplayName = source.DisplayName;
        Description = source.Description;
        LocationId = source.LocationId;
        RelatedItemId = source.RelatedItemId;
        ReviewDelayMinutes = source.ReviewDelayMinutes;

        Status = IncidentStatus.Open;
    }

    public ReportValidationResult TrySubmit(IncidentReport report, ReportSubmissionValidator validator, int currentDayIndex, int currentMinute)
    {
        if (validator == null)
            throw new ArgumentNullException(nameof(validator));

        var validation = validator.Validate(this, report, currentDayIndex, currentMinute);

        if (!validation.IsValid)
            return validation;

        SubmittedReport = report;
        Status = IncidentStatus.Pending;

        return new ReportValidationResult(true, "Report submitted and awaiting HR review.");
    }

    public bool TryApplyDecision(HrEvaluationResult decision)
    {
        if (decision == null)
            throw new ArgumentNullException(nameof(decision));

        if (Status != IncidentStatus.Pending || !ReferenceEquals(decision.Report, SubmittedReport))
        {
            return false;
        }

        Decision = decision;

        Status = decision.Verdict == HrVerdict.Approved ? IncidentStatus.Approved : IncidentStatus.Rejected;

        return true;
    }

    public bool TryMarkOverdue(GameTime now)
    {
        if (Status != IncidentStatus.Open)
            return false;

        var deadline = new GameTime(DayIndex, DeadlineMinute);

        if (now.TotalMilliseconds <= deadline.TotalMilliseconds)
            return false;

        Status = IncidentStatus.Overdue;
        return true;
    }
}