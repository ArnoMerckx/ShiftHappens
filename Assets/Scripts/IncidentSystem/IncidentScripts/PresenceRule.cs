using System;
using System.Collections.Generic;

public sealed class PresenceRule : IHrRule
{
    public string RuleId => "presence";

    public HrRuleResult Evaluate(EvaluationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var presence = ScheduleResolver.GetPresence(context.Report.SuspectId, context.Incident.LocationId, context.Incident.Occurrence, new List<ApprovedScheduleRecord>(context.HrKnowledge.ApprovedRecords));

        if (presence == PresenceStatus.Impossible)
        {
            return new HrRuleResult(
                RuleId,
                HrRuleOutcome.Blocker,
                "HR's approved records exclude the suspect from the " +
                "incident location throughout the incident window.");
        }

        if (presence == PresenceStatus.Possible)
        {
            return new HrRuleResult(
                RuleId,
                HrRuleOutcome.Neutral,
                "HR's approved records allow opportunity at the incident " +
                "location, but opportunity alone is not submitted support.");
        }

        return new HrRuleResult(
            RuleId,
            HrRuleOutcome.Neutral,
            "HR's approved records do not establish presence or exclude " +
            "the suspect throughout the incident window. Presence remains unknown.");
    }
}