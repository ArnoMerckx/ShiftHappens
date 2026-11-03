using System;
using System.Collections.Generic;

public sealed class ActivitySupportRule : IHrRule
{
    public string RuleId => "activity_support";

    public HrRuleResult Evaluate(EvaluationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var window = context.Incident.Occurrence;

        var approvedRecords = new List<ApprovedScheduleRecord>(context.HrKnowledge.ApprovedRecords);

        foreach (var claim in context.SubmittedClaims)
        {
            if (!(claim is WhereaboutsClaim assignment))
                continue;

            if (assignment.EmployeeId != context.Report.SuspectId || assignment.LocationId != context.Incident.LocationId || !assignment.Period.Overlaps(window))
            {
                continue;
            }

            var overlap = new TimeRange(window.DayIndex, Math.Max(window.StartMinute, assignment.Period.StartMinute), Math.Min(window.EndMinute, assignment.Period.EndMinute));

            var presence = ScheduleResolver.GetPresence(context.Report.SuspectId, context.Incident.LocationId, overlap, approvedRecords);

            if (presence == PresenceStatus.Impossible)
                continue;

            return new HrRuleResult(
                RuleId,
                HrRuleOutcome.Support,
                "A submitted assignment matches the suspect, location " +
                "and an unexcluded part of the incident window.");
        }

        return new HrRuleResult(
            RuleId,
            HrRuleOutcome.Neutral,
            "No submitted assignment provides relevant support during " +
            "an unexcluded part of the incident window.");
    }
}