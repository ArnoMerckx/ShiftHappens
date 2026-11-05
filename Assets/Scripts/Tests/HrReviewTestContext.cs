using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

internal sealed class HrReviewTestContext
{
    public DayData Day { get; }
    public GameClock Clock { get; }
    public EventScheduler Scheduler { get; }
    public DayDeliveryService Deliveries { get; }
    public EvidenceService Evidence { get; }
    public ReportSubmissionService Submissions { get; }
    public HrReviewProcessor Processor { get; private set; }

    public HrReviewTestContext(DayData data = null)
    {
        if (data == null)
        {
            var definition = AssetDatabase.LoadAssetAtPath<DayDefinition>(
                "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
            Assert.That(definition, Is.Not.Null);
            data = definition.GetData();
        }

        Day = data;
        Clock = new GameClock(data.ScheduleData.Settings.OpensAt, 20);
        Scheduler = new EventScheduler(Clock);
        Deliveries = new DayDeliveryService(data, Scheduler);
        Evidence = new EvidenceService(data.EvidenceDatabase);
        Submissions = new ReportSubmissionService(Scheduler, Deliveries,
            new ReportSubmissionValidator(
                new List<string>(data.ScheduleData.EmployeeIds), Evidence));
    }

    public HrReviewProcessor AttachProcessor(
        HrKnowledge knowledge = null, EvidenceDatabase database = null)
    {
        Assert.That(Processor, Is.Null, "Attach only one processor per test session.");
        Processor = new HrReviewProcessor(Submissions, Scheduler,
            knowledge ?? Day.ScheduleData.HrKnowledge,
            database ?? Day.EvidenceDatabase);
        return Processor;
    }

    public QueuedHrReview Submit(
        string instanceId, string suspect, List<string> documents, GameTime at)
    {
        Scheduler.AdvanceTo(at);
        foreach (string document in documents)
        {
            if (!Evidence.IsDiscovered(document))
            {
                Assert.That(Evidence.MarkDiscovered(
                    document, Clock.Now.DayIndex, Clock.Now.MinuteOfDay), Is.True);
            }
        }

        ReportValidationResult result = Submissions.TrySubmit(instanceId, suspect, documents);
        Assert.That(result.IsValid, Is.True, result.Reason);
        return Submissions.PendingReviews[Submissions.PendingReviews.Count - 1];
    }
}
