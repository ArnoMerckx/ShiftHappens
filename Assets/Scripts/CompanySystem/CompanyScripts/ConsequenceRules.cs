using System;

public sealed class ConsequenceRules
{
    public int StartingTrust { get; }
    public int MaximumTrust { get; }

    public int BestAvailableCredibilityDelta { get; }
    public int CapableAlternativeCredibilityDelta { get; }
    public int UnsuitableCredibilityDelta { get; }
    public int UnavailableCredibilityDelta { get; }

    public int BestAvailableTrustDelta { get; }
    public int CapableAlternativeTrustDelta { get; }
    public int UnsuitableTrustDelta { get; }
    public int UnavailableTrustDelta { get; }

    public int UnansweredCredibilityDelta { get; }
    public int UnansweredTrustDelta { get; }
    public int WrongfulBlameTrustDelta { get; }

    public ConsequenceRules(
        int startingTrust = 50,
        int maximumTrust = 100,
        int bestAvailableCredibilityDelta = 5,
        int capableAlternativeCredibilityDelta = 2,
        int unsuitableCredibilityDelta = -5,
        int unavailableCredibilityDelta = -10,
        int bestAvailableTrustDelta = 5,
        int capableAlternativeTrustDelta = 2,
        int unsuitableTrustDelta = -5,
        int unavailableTrustDelta = -5,
        int unansweredCredibilityDelta = -10,
        int unansweredTrustDelta = -5,
        int wrongfulBlameTrustDelta = -10)
    {
        if (maximumTrust <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumTrust));

        if (startingTrust < 0 || startingTrust > maximumTrust)
            throw new ArgumentOutOfRangeException(nameof(startingTrust));

        StartingTrust = startingTrust;
        MaximumTrust = maximumTrust;

        BestAvailableCredibilityDelta = bestAvailableCredibilityDelta;
        CapableAlternativeCredibilityDelta = capableAlternativeCredibilityDelta;
        UnsuitableCredibilityDelta = unsuitableCredibilityDelta;
        UnavailableCredibilityDelta = unavailableCredibilityDelta;

        BestAvailableTrustDelta = bestAvailableTrustDelta;
        CapableAlternativeTrustDelta = capableAlternativeTrustDelta;
        UnsuitableTrustDelta = unsuitableTrustDelta;
        UnavailableTrustDelta = unavailableTrustDelta;

        UnansweredCredibilityDelta = unansweredCredibilityDelta;
        UnansweredTrustDelta = unansweredTrustDelta;
        WrongfulBlameTrustDelta = wrongfulBlameTrustDelta;
    }

    public int GetCredibilityDelta(DelegationOutcome outcome)
    {
        switch (outcome)
        {
            case DelegationOutcome.BestAvailable:
                return BestAvailableCredibilityDelta;
            case DelegationOutcome.CapableAlternative:
                return CapableAlternativeCredibilityDelta;
            case DelegationOutcome.Unsuitable:
                return UnsuitableCredibilityDelta;
            case DelegationOutcome.Unavailable:
                return UnavailableCredibilityDelta;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome));
        }
    }

    public int GetTrustDelta(DelegationOutcome outcome)
    {
        switch (outcome)
        {
            case DelegationOutcome.BestAvailable:
                return BestAvailableTrustDelta;
            case DelegationOutcome.CapableAlternative:
                return CapableAlternativeTrustDelta;
            case DelegationOutcome.Unsuitable:
                return UnsuitableTrustDelta;
            case DelegationOutcome.Unavailable:
                return UnavailableTrustDelta;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome));
        }
    }
}