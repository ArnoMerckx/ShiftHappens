using System;

public sealed class ReviewHistoryEntry
{
    public IncidentReport Report { get; }
    public HrEvaluationResult Decision { get; }
    public GameTime SubmittedAt { get; }
    public GameTime ReviewedAt { get; }
    public int CredibilityBefore { get; }
    public int CredibilityAfter { get; }

    public ReviewHistoryEntry(QueuedHrReview review, HrEvaluationResult decision, GameTime reviewedAt, int credibilityBefore, int credibilityAfter)
    {
        if (review == null)
            throw new ArgumentNullException(nameof(review));

        if (decision == null)
            throw new ArgumentNullException(nameof(decision));

        if (!ReferenceEquals(decision.Report, review.Report))
            throw new ArgumentException("The decision must belong to the queued report.", nameof(decision));

        if (reviewedAt.TotalMilliseconds < review.ReviewAt.TotalMilliseconds)
        {
            throw new ArgumentException("A review cannot complete before its due time.", nameof(reviewedAt));
        }

        if (credibilityBefore < 0)
            throw new ArgumentOutOfRangeException(nameof(credibilityBefore));

        if (credibilityAfter < 0 || credibilityAfter > credibilityBefore)
            throw new ArgumentOutOfRangeException(nameof(credibilityAfter));

        Report = review.Report;
        Decision = decision;
        SubmittedAt = review.SubmittedAt;
        ReviewedAt = reviewedAt;
        CredibilityBefore = credibilityBefore;
        CredibilityAfter = credibilityAfter;
    }
}