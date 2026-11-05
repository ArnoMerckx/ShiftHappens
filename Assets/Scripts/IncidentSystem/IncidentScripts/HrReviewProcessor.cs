using System;
using System.Collections.Generic;

public sealed class HrReviewProcessor
{
    private readonly ReportSubmissionService submissions;
    private readonly EventScheduler scheduler;
    private readonly HrKnowledge hrKnowledge;
    private readonly EvidenceDatabase evidenceDatabase;
    private readonly HrEvaluator evaluator = new HrEvaluator();

    private readonly HashSet<QueuedHrReview> scheduledReviews = new HashSet<QueuedHrReview>();

    public event Action<QueuedHrReview, HrEvaluationResult> ReviewCompleted;

    public HrReviewProcessor(ReportSubmissionService submissions, EventScheduler scheduler, HrKnowledge hrKnowledge, EvidenceDatabase evidenceDatabase)
    {
        if (submissions == null)
            throw new ArgumentNullException(nameof(submissions));

        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));

        if (hrKnowledge == null)
            throw new ArgumentNullException(nameof(hrKnowledge));

        if (evidenceDatabase == null)
            throw new ArgumentNullException(nameof(evidenceDatabase));

        foreach (var review in submissions.PendingReviews)
        {
            if (review.ReviewAt.TotalMilliseconds < scheduler.Now.TotalMilliseconds)
            {
                throw new InvalidOperationException("Attach HR processing before queued reviews become overdue.");
            }
        }

        this.submissions = submissions;
        this.scheduler = scheduler;
        this.hrKnowledge = hrKnowledge;
        this.evidenceDatabase = evidenceDatabase;

        foreach (var review in submissions.PendingReviews)
            ScheduleReview(review);

        submissions.ReviewQueued += ScheduleReview;
    }

    private void ScheduleReview(QueuedHrReview review)
    {
        if (scheduledReviews.Contains(review))
            return;

        scheduler.Schedule(new ScheduledEvent($"hr_review:{review.Incident.InstanceId}", review.ReviewAt, ScheduledEventPriority.HrReview, () => ProcessReview(review)));

        scheduledReviews.Add(review);
    }

    private void ProcessReview(QueuedHrReview review)
    {
        if (!submissions.ContainsReview(review))
            return;

        if (scheduler.Now.TotalMilliseconds < review.ReviewAt.TotalMilliseconds)
        {
            return;
        }

        IncidentInstance incident = review.Incident;

        if (incident.Status != IncidentStatus.Pending || !ReferenceEquals(incident.SubmittedReport, review.Report))
        {
            submissions.RemoveReview(review);
            return;
        }

        var context = new EvaluationContext(incident, hrKnowledge, evidenceDatabase);

        HrEvaluationResult decision = evaluator.Evaluate(context);

        if (!incident.TryApplyDecision(decision))
            return;

        submissions.RemoveReview(review);
        ReviewCompleted?.Invoke(review, decision);
    }
}