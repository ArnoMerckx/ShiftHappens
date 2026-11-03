public interface IHrRule
{
    string RuleId { get; }

    HrRuleResult Evaluate(EvaluationContext context);
}