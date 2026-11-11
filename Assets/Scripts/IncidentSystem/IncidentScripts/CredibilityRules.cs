using System;

public sealed class CredibilityRules
{
    public int StartingCredibility { get; }
    public int RejectedReportPenalty { get; }
    public int MissedDeadlinePenalty { get; }
    public int FiringThreshold { get; }
    public int MaximumCredibility { get; }


    public CredibilityRules(int startingCredibility = 100, int rejectedReportPenalty = 25, int missedDeadlinePenalty = 35, int firingThreshold = 0, int maximumCredibility = 100)
    {
        if (maximumCredibility <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCredibility));

        if (startingCredibility <= 0 || startingCredibility > maximumCredibility)
            throw new ArgumentOutOfRangeException(nameof(startingCredibility));

        if (rejectedReportPenalty < 0)
            throw new ArgumentOutOfRangeException(nameof(rejectedReportPenalty));

        if (missedDeadlinePenalty < 0)
            throw new ArgumentOutOfRangeException(nameof(missedDeadlinePenalty));

        if (firingThreshold < 0 || firingThreshold >= startingCredibility)
            throw new ArgumentOutOfRangeException(nameof(firingThreshold));

        StartingCredibility = startingCredibility;
        RejectedReportPenalty = rejectedReportPenalty;
        MissedDeadlinePenalty = missedDeadlinePenalty;
        FiringThreshold = firingThreshold;
        MaximumCredibility = maximumCredibility;
    }

    public int ApplyDelta(int current, int delta)
    {
        if (current < 0 || current > MaximumCredibility)
            throw new ArgumentOutOfRangeException(nameof(current));

        long changed = (long)current + delta;

        return (int)Math.Max(0L, Math.Min((long)MaximumCredibility, changed));
    }
}