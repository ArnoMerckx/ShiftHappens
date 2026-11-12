using System;

public sealed class CoworkerReactionRules
{
    public int LowTrustThreshold { get; }
    public int RepeatedAssignmentCount { get; }

    public CoworkerReactionRules(int lowTrustThreshold = 40, int repeatedAssignmentCount = 2)
    {
        if (lowTrustThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(lowTrustThreshold));

        if (repeatedAssignmentCount < 2)
            throw new ArgumentOutOfRangeException(nameof(repeatedAssignmentCount));

        LowTrustThreshold = lowTrustThreshold;
        RepeatedAssignmentCount = repeatedAssignmentCount;
    }
}