using System;

public sealed class DelegationSubmissionResult
{
    public bool IsAccepted => Decision != null;
    public string Reason { get; }
    public DelegationDecision Decision { get; }

    private DelegationSubmissionResult(string reason, DelegationDecision decision)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A submission reason is required.", nameof(reason));

        Reason = reason;
        Decision = decision;
    }

    public static DelegationSubmissionResult Accepted(DelegationDecision decision)
    {
        if (decision == null)
            throw new ArgumentNullException(nameof(decision));

        return new DelegationSubmissionResult(decision.Explanation, decision);
    }

    public static DelegationSubmissionResult Rejected(string reason)
    {
        return new DelegationSubmissionResult(reason, null);
    }
}