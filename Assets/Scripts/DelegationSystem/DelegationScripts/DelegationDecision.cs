using System;

public sealed class DelegationDecision
{
    public string RequestInstanceId { get; }
    public string HelperId { get; }
    public GameTime SubmittedAt { get; }
    public DelegationOutcome Outcome { get; }
    public string Explanation { get; }

    public DelegationDecision(string requestInstanceId, string helperId, GameTime submittedAt, DelegationOutcome outcome, string explanation)
    {
        if (string.IsNullOrWhiteSpace(requestInstanceId))
            throw new ArgumentException("Request instance ID is required.", nameof(requestInstanceId));

        if (string.IsNullOrWhiteSpace(helperId))
            throw new ArgumentException("Helper ID is required.", nameof(helperId));

        if (!Enum.IsDefined(typeof(DelegationOutcome), outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));

        if (string.IsNullOrWhiteSpace(explanation))
            throw new ArgumentException("A decision explanation is required.", nameof(explanation));

        RequestInstanceId = requestInstanceId;
        HelperId = helperId;
        SubmittedAt = submittedAt;
        Outcome = outcome;
        Explanation = explanation;
    }
}