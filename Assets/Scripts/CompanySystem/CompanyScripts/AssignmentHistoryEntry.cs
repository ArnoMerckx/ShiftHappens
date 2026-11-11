using System;

public sealed class AssignmentHistoryEntry
{
    public string DayId { get; }
    public DelegationDecision Decision { get; }

    public string RequestInstanceId => Decision.RequestInstanceId;
    public string HelperId => Decision.HelperId;
    public DelegationOutcome Outcome => Decision.Outcome;
    public GameTime SubmittedAt => Decision.SubmittedAt;
    public int DayIndex => SubmittedAt.DayIndex;

    public bool IsSuitableArranged => Outcome == DelegationOutcome.BestAvailable || Outcome == DelegationOutcome.CapableAlternative;

    public AssignmentHistoryEntry(string dayId, DelegationDecision decision)
    {
        if (string.IsNullOrWhiteSpace(dayId))
            throw new ArgumentException("A day ID is required.", nameof(dayId));

        if (decision == null)
            throw new ArgumentNullException(nameof(decision));

        DayId = dayId;
        Decision = decision;
    }
}