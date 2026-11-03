using System;
using System.Collections.Generic;

public sealed class HrEvaluator
{
    private readonly List<IHrRule> rules = new List<IHrRule>
    {
        new PresenceRule(),
        new ActivitySupportRule(),
        new CustodySupportRule()
    };

    public HrEvaluationResult Evaluate(EvaluationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var results = new List<HrRuleResult>();

        foreach (var rule in rules)
        {
            var result = rule.Evaluate(context);

            if (result == null || result.RuleId != rule.RuleId)
            {
                throw new InvalidOperationException($"Rule {rule.RuleId} returned an invalid result.");
            }

            results.Add(result);
        }

        return new HrEvaluationResult(context.Report, results);
    }
}