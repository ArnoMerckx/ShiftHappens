using System;

public sealed class HrRuleResult
{
    public string RuleId { get; }
    public HrRuleOutcome Outcome { get; }
    public string Reason { get; }

    public HrRuleResult(string ruleId, HrRuleOutcome outcome, string reason)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("Rule ID is required.", nameof(ruleId));

        if (!Enum.IsDefined(typeof(HrRuleOutcome), outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rule explanation is required.", nameof(reason));

        RuleId = ruleId;
        Outcome = outcome;
        Reason = reason;
    }
}