using System;
using System.Collections.Generic;

public sealed class HrEvaluationResult
{
    public IncidentReport Report { get; }
    public HrVerdict Verdict { get; }
    public string Summary { get; }
    public IReadOnlyList<HrRuleResult> RuleResults { get; }

    public HrEvaluationResult(IncidentReport report, List<HrRuleResult> ruleResults)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        if (ruleResults == null)
            throw new ArgumentNullException(nameof(ruleResults));

        var results = new List<HrRuleResult>();
        var ruleIds = new HashSet<string>(StringComparer.Ordinal);

        bool hasSupport = false;
        bool hasBlocker = false;

        foreach (var result in ruleResults)
        {
            if (result == null)
                throw new ArgumentException("Rule results cannot contain null.", nameof(ruleResults));

            if (!ruleIds.Add(result.RuleId))
                throw new ArgumentException($"Duplicate rule result ID: {result.RuleId}", nameof(ruleResults));

            results.Add(result);

            if (result.Outcome == HrRuleOutcome.Support)
                hasSupport = true;

            if (result.Outcome == HrRuleOutcome.Blocker)
                hasBlocker = true;
        }

        Report = report;
        RuleResults = results.AsReadOnly();

        if (hasBlocker)
        {
            Verdict = HrVerdict.Rejected;
            Summary = "Rejected: a hard contradiction blocks the accusation.";
        }
        else if (hasSupport)
        {
            Verdict = HrVerdict.Approved;
            Summary =
                "Approved: submitted evidence provides support " +
                "and no hard contradiction applies.";
        }
        else
        {
            Verdict = HrVerdict.Rejected;
            Summary = "Rejected: the submitted evidence provides no relevant support.";
        }
    }
}