using System;
using System.Collections.Generic;

public sealed class CustodySupportRule : IHrRule
{
    public string RuleId => "custody_support";

    public HrRuleResult Evaluate(EvaluationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var itemId = context.Incident.RelatedItemId;

        if (string.IsNullOrEmpty(itemId))
        {
            return new HrRuleResult(RuleId,HrRuleOutcome.Neutral,"This incident does not identify a specific item for custody evidence to support.");
        }

        var window = context.Incident.Occurrence;

        var approvedRecords = new List<ApprovedScheduleRecord>(context.HrKnowledge.ApprovedRecords);

        foreach (var claim in context.SubmittedClaims)
        {
            if (!(claim is CustodyClaim custody))
                continue;

            if (custody.EmployeeId != context.Report.SuspectId || custody.ItemId != itemId)
            {
                continue;
            }

            if (custody.DayIndex > window.DayIndex || (custody.DayIndex == window.DayIndex && custody.MinuteOfDay > window.StartMinute))
            {
                continue;
            }

            var presence = ScheduleResolver.GetPresence(context.Report.SuspectId, context.Incident.LocationId, window, approvedRecords);

            if (presence == PresenceStatus.Impossible)
                continue;

            return new HrRuleResult(
                RuleId,
                HrRuleOutcome.Support,
                "A submitted custody record establishes responsibility " +
                "for the exact item by the start of the incident window, " +
                "without a verified whole-window exclusion.");
        }

        return new HrRuleResult(
            RuleId,
            HrRuleOutcome.Neutral,
            "No submitted custody record provides earlier responsibility " +
            "for the exact item with unexcluded opportunity.");
    }
}