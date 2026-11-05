using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class IncidentDeadlineServiceTests
{
    private HrReviewTestContext context;
    private WorkdayProgress progress;

    [SetUp]
    public void SetUp()
    {
        UseDay();
    }

    private void UseDay(DayData data = null, CredibilityRules rules = null)
    {
        context = new HrReviewTestContext(data);
        context.AttachProcessor();
        progress = new WorkdayProgress(
            rules ?? new CredibilityRules(), context.Processor, context.Scheduler);
    }

    private void UseSingleIncident(int index, CredibilityRules rules = null)
    {
        DayData original = context.Day;
        UseDay(new DayData(original.ScheduleData,
            new List<EvidenceDefinition>(original.EvidenceDatabase.All),
            new List<DayIncidentPlan> { original.Incidents[index] }), rules);
    }

    private IncidentDeadlineService Create()
    {
        return new IncidentDeadlineService(context.Deliveries, context.Scheduler, progress);
    }

    private static GameTime Late(int deadlineMinute)
    {
        return new GameTime(0, deadlineMinute, 0, 1);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Constructor_RejectsNullDependency(int input)
    {
        Assert.Throws<ArgumentNullException>(() => new IncidentDeadlineService(
            input == 0 ? null : context.Deliveries,
            input == 1 ? null : context.Scheduler,
            input == 2 ? null : progress));
    }

    [Test]
    public void Construction_WaitsForActualIncidentDelivery()
    {
        int before = context.Scheduler.PendingCount;
        Create();
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(before));
        Assert.That(context.Deliveries.DeliveredIncidents, Is.Empty);
        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.MissedDeadlines, Is.Empty);
    }

    [TestCase(0, "D0I1", 630)]
    [TestCase(1, "D0I2", 720)]
    [TestCase(2, "D0I3", 900)]
    public void Deadline_RemainsOpenAtEqualityAndChargesOnceAtFirstLateMillisecond(
        int index, string instanceId, int deadline)
    {
        UseSingleIncident(index);
        IncidentDeadlineService service = Create();
        int notices = 0;
        int changes = 0;
        progress.Changed += () => changes++;
        service.IncidentOverdue += incident =>
        {
            notices++;
            Assert.That(incident.InstanceId, Is.EqualTo(instanceId));
            Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Overdue));
            Assert.That(progress.Credibility, Is.EqualTo(65));
            Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
            Assert.That(context.Scheduler.Now.TotalMilliseconds,
                Is.EqualTo(Late(deadline).TotalMilliseconds));
        };

        context.Scheduler.AdvanceTo(new GameTime(0, deadline));
        IncidentInstance instance = context.Deliveries.GetDelivered(instanceId);
        Assert.That(instance.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.MissedDeadlines, Is.Empty);

        context.Scheduler.AdvanceTo(Late(deadline));
        context.Scheduler.AdvanceTo(Late(deadline));
        context.Scheduler.AdvanceTo(context.Day.ScheduleData.Settings.ClosesAt);

        Assert.That(notices, Is.EqualTo(1));
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(progress.MissedDeadlines[0].InstanceId, Is.EqualTo(instanceId));
        Assert.That(progress.MissedDeadlines[0].RecordedAt.TotalMilliseconds,
            Is.EqualTo(Late(deadline).TotalMilliseconds));
        Assert.That(progress.History, Is.Empty);
        Assert.That(instance.SubmittedReport, Is.Null);
        Assert.That(instance.Decision, Is.Null);
    }

    [Test]
    public void FullDayJump_RecordsEachMissOnceAndClampsChosenPenaltiesAtZero()
    {
        var service = Create();
        var ids = new List<string>();
        service.IncidentOverdue += incident => ids.Add(incident.InstanceId);

        context.Scheduler.AdvanceTo(context.Day.ScheduleData.Settings.ClosesAt);
        context.Scheduler.AdvanceTo(context.Day.ScheduleData.Settings.ClosesAt);

        Assert.That(ids, Is.EqualTo(new[] { "D0I1", "D0I2", "D0I3" }));
        Assert.That(progress.Credibility, Is.Zero);
        Assert.That(progress.HasReachedFiringThreshold, Is.True);
        Assert.That(progress.History, Is.Empty);
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(3));
        int[] before = { 100, 65, 30 };
        int[] after = { 65, 30, 0 };
        int[] deadlines = { 630, 720, 900 };
        for (int i = 0; i < 3; i++)
        {
            MissedDeadlineEntry entry = progress.MissedDeadlines[i];
            Assert.That(entry.CredibilityBefore, Is.EqualTo(before[i]));
            Assert.That(entry.CredibilityAfter, Is.EqualTo(after[i]));
            Assert.That(entry.RecordedAt.TotalMilliseconds,
                Is.EqualTo(Late(deadlines[i]).TotalMilliseconds));
        }
    }

    [TestCase("E_DAVE_PRINTER", HrVerdict.Approved, 100)]
    [TestCase("E_OFFICE_SOCIAL", HrVerdict.Rejected, 75)]
    public void ReportAtExactDeadline_IsReviewedLaterWithoutMissedPenalty(
        string document, HrVerdict verdict, int score)
    {
        UseSingleIncident(1);
        IncidentDeadlineService service = Create();
        int overdueNotices = 0;
        service.IncidentOverdue += incident => overdueNotices++;
        QueuedHrReview job = context.Submit("D0I2", "dave",
            new List<string> { document }, new GameTime(0, 720));

        context.Scheduler.AdvanceTo(Late(720));
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.MissedDeadlines, Is.Empty);

        context.Scheduler.AdvanceTo(job.ReviewAt);
        context.Scheduler.AdvanceTo(context.Day.ScheduleData.Settings.ClosesAt);

        Assert.That(job.Incident.Decision.Verdict, Is.EqualTo(verdict));
        Assert.That(progress.Credibility, Is.EqualTo(score));
        Assert.That(progress.History.Count, Is.EqualTo(1));
        Assert.That(progress.MissedDeadlines, Is.Empty);
        Assert.That(overdueNotices, Is.Zero);
    }

    [TestCase("E_DAVE_PRINTER", IncidentStatus.Approved, 100)]
    [TestCase("E_OFFICE_SOCIAL", IncidentStatus.Rejected, 75)]
    public void FinalizedReport_IsProtectedFromLaterDeadlineCallback(
        string document, IncidentStatus status, int score)
    {
        UseSingleIncident(1);
        Create();
        QueuedHrReview job = context.Submit("D0I2", "dave",
            new List<string> { document }, new GameTime(0, 675));
        context.Scheduler.AdvanceTo(job.ReviewAt);
        context.Scheduler.AdvanceTo(Late(720));

        Assert.That(job.Incident.Status, Is.EqualTo(status));
        Assert.That(progress.Credibility, Is.EqualTo(score));
        Assert.That(progress.MissedDeadlines, Is.Empty);
        Assert.That(progress.History.Count, Is.EqualTo(1));
    }

    [Test]
    public void SubmissionAfterOverdue_FailsWithoutInventingReportOrAnotherPenalty()
    {
        UseSingleIncident(1);
        Create();
        context.Scheduler.AdvanceTo(Late(720));
        Assert.That(context.Evidence.MarkDiscovered("E_DAVE_PRINTER", 0, 720), Is.True);

        var result = context.Submissions.TrySubmit("D0I2", "dave",
            new List<string> { "E_DAVE_PRINTER" });

        Assert.That(result.IsValid, Is.False);
        Assert.That(progress.Credibility, Is.EqualTo(65));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
        Assert.That(progress.History, Is.Empty);
        Assert.That(context.Submissions.PendingReviews, Is.Empty);
        Assert.That(context.Deliveries.GetDelivered("D0I2").SubmittedReport, Is.Null);
    }

    [Test]
    public void EarlyCallbackAndReplay_DoNotDuplicateTransitionPenaltyOrNotifications()
    {
        UseSingleIncident(0);
        var service = Create();
        int notices = 0;
        int changes = 0;
        service.IncidentOverdue += incident => notices++;
        progress.Changed += () => changes++;
        context.Scheduler.AdvanceTo(new GameTime(0, 590));
        ScheduledEvent callback = context.Scheduler.PeekNext();
        Assert.That(callback.DueTime.TotalMilliseconds, Is.EqualTo(Late(630).TotalMilliseconds));

        callback.Callback();
        context.Scheduler.AdvanceTo(new GameTime(0, 630));
        callback.Callback();
        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.MissedDeadlines, Is.Empty);

        context.Scheduler.AdvanceTo(Late(630));
        callback.Callback();
        callback.Callback();

        Assert.That(progress.Credibility, Is.EqualTo(65));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
        Assert.That(notices, Is.EqualTo(1));
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void MissedDeadlineView_IsReadOnlyAndReflectsRecordedMisses()
    {
        UseSingleIncident(0);
        Create();
        IReadOnlyList<MissedDeadlineEntry> view = progress.MissedDeadlines;
        Assert.That(view, Is.Empty);
        context.Scheduler.AdvanceTo(Late(630));

        Assert.That(view.Count, Is.EqualTo(1));
        Assert.That(progress.MissedDeadlines, Is.SameAs(view));
        Assert.Throws<NotSupportedException>(() => ((IList<MissedDeadlineEntry>)view).Clear());
    }

    [Test]
    public void GamePause_DefersOverdueProcessingUntilResume()
    {
        UseSingleIncident(1);
        Create();
        context.Scheduler.AdvanceTo(new GameTime(0, 720));
        context.Clock.SetPaused(true);
        context.Scheduler.AdvanceTo(Late(720));
        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.MissedDeadlines, Is.Empty);

        context.Clock.SetPaused(false);
        context.Scheduler.AdvanceTo(Late(720));
        Assert.That(progress.Credibility, Is.EqualTo(65));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
    }

    [Test]
    public void Attachment_HandlesAlreadyDeliveredOpenIncidentAndFutureDeliveries()
    {
        context.Scheduler.AdvanceTo(new GameTime(0, 590));
        Create();
        context.Scheduler.AdvanceTo(Late(630));
        Assert.That(progress.Credibility, Is.EqualTo(65));
        context.Scheduler.AdvanceTo(Late(720));

        Assert.That(progress.Credibility, Is.EqualTo(30));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(2));
    }

    [Test]
    public void AttachmentAtFirstLateInstant_SchedulesDueWorkWithoutImmediateMutation()
    {
        UseSingleIncident(1);
        context.Scheduler.AdvanceTo(Late(720));
        Create();
        Assert.That(progress.Credibility, Is.EqualTo(100));

        context.Scheduler.AdvanceTo(context.Clock.Now);
        Assert.That(progress.Credibility, Is.EqualTo(65));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
    }

    [Test]
    public void LateAttachmentWithOpenIncident_FailsBeforeSchedulingOrSubscribing()
    {
        context.Scheduler.AdvanceTo(new GameTime(0, 631));
        int before = context.Scheduler.PendingCount;
        Assert.Throws<InvalidOperationException>(() => Create());
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(before));

        context.Scheduler.AdvanceTo(new GameTime(0, 675));
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(1));
        Assert.That(context.Scheduler.PeekNext().DueTime.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 840).TotalMilliseconds));
        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.MissedDeadlines, Is.Empty);
    }

    [Test]
    public void AttachmentAfterDeadline_DoesNotExpirePendingReview()
    {
        UseSingleIncident(1);
        QueuedHrReview job = context.Submit("D0I2", "dave",
            new List<string> { "E_DAVE_PRINTER" }, new GameTime(0, 720));
        context.Scheduler.AdvanceTo(Late(720));
        int before = context.Scheduler.PendingCount;
        Create();
        Assert.That(context.Scheduler.PendingCount, Is.EqualTo(before));

        context.Scheduler.AdvanceTo(job.ReviewAt);
        Assert.That(job.Incident.Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(progress.Credibility, Is.EqualTo(100));
        Assert.That(progress.MissedDeadlines, Is.Empty);
    }

    [TestCase("E_DAVE_PRINTER", 100)]
    [TestCase("E_OFFICE_SOCIAL", 75)]
    public void AttachmentAfterFinalDecision_DoesNotScheduleMissedPenalty(string document, int score)
    {
        UseSingleIncident(1);
        QueuedHrReview job = context.Submit("D0I2", "dave",
            new List<string> { document }, new GameTime(0, 675));
        context.Scheduler.AdvanceTo(job.ReviewAt);
        context.Scheduler.AdvanceTo(new GameTime(0, 721));
        Create();

        Assert.That(context.Scheduler.PendingCount, Is.Zero);
        Assert.That(progress.Credibility, Is.EqualTo(score));
        Assert.That(progress.History.Count, Is.EqualTo(1));
        Assert.That(progress.MissedDeadlines, Is.Empty);
    }

    [TestCase(100, 0, 100, false)]
    [TestCase(10, 35, 0, true)]
    [TestCase(100, 12, 88, false)]
    public void MissedPenalty_UsesConfigurationAndClampsAtZero(
        int start, int penalty, int expected, bool reached)
    {
        UseSingleIncident(0, new CredibilityRules(start, 25, penalty));
        var service = Create();
        int notices = 0;
        service.IncidentOverdue += incident => notices++;
        context.Scheduler.AdvanceTo(Late(630));

        Assert.That(progress.Credibility, Is.EqualTo(expected));
        Assert.That(progress.HasReachedFiringThreshold, Is.EqualTo(reached));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
        Assert.That(progress.MissedDeadlines[0].CredibilityBefore, Is.EqualTo(start));
        Assert.That(progress.MissedDeadlines[0].CredibilityAfter, Is.EqualTo(expected));
        Assert.That(notices, Is.EqualTo(1));
    }

    [Test]
    public void SeparateSessions_DoNotShareMissedDeadlinesOrPenalties()
    {
        UseSingleIncident(0);
        Create();
        var second = new HrReviewTestContext(context.Day);
        second.AttachProcessor();
        var secondProgress = new WorkdayProgress(
            new CredibilityRules(), second.Processor, second.Scheduler);
        var secondDeadlines = new IncidentDeadlineService(
            second.Deliveries, second.Scheduler, secondProgress);

        context.Scheduler.AdvanceTo(Late(630));

        Assert.That(progress.Credibility, Is.EqualTo(65));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
        Assert.That(secondProgress.Credibility, Is.EqualTo(100));
        Assert.That(secondProgress.MissedDeadlines, Is.Empty);
        Assert.That(second.Deliveries.DeliveredIncidents, Is.Empty);
    }

    [Test]
    public void MissedDeadline_DoesNotChangeEvidenceDiscoveryOrHrKnowledge()
    {
        Create();
        HrKnowledge knowledge = context.Day.ScheduleData.HrKnowledge;
        context.Scheduler.AdvanceTo(Late(630));

        foreach (var document in context.Day.EvidenceDatabase.All)
        {
            Assert.That(context.Evidence.IsDiscovered(document.EvidenceId), Is.False);
            Assert.That(context.Evidence.IsRead(document.EvidenceId), Is.False);
        }
        Assert.That(context.Day.ScheduleData.HrKnowledge, Is.SameAs(knowledge));
        Assert.That(knowledge.ApprovedRecords.Count, Is.EqualTo(4));
        Assert.That(knowledge.KnownClaims.Count, Is.EqualTo(4));
        Assert.That(context.Day.ScheduleData.WorldApprovedRecords.Count, Is.EqualTo(4));
    }

    [Test]
    public void ChangedSubscriberException_PreservesSingleCommittedMissedPenalty()
    {
        UseSingleIncident(0);
        Create();
        context.Scheduler.AdvanceTo(new GameTime(0, 590));
        ScheduledEvent callback = context.Scheduler.PeekNext();
        var failure = new InvalidOperationException("Test missed-deadline notification failure.");
        int changes = 0;
        progress.Changed += () =>
        {
            changes++;
            throw failure;
        };

        var caught = Assert.Throws<InvalidOperationException>(
            () => context.Scheduler.AdvanceTo(Late(630)));

        Assert.That(caught, Is.SameAs(failure));
        Assert.That(progress.Credibility, Is.EqualTo(65));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
        Assert.That(context.Deliveries.GetDelivered("D0I1").Status,
            Is.EqualTo(IncidentStatus.Overdue));
        callback.Callback();
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void SimultaneousHrReview_PrecedesOverdueAndBothEffectsAreRecordedOnce()
    {
        var coffee = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I1_CoffeeSpill.asset");
        var printer = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
        Assert.That(coffee, Is.Not.Null);
        Assert.That(printer, Is.Not.Null);
        var plans = new List<DayIncidentPlan>
        {
            new DayIncidentPlan(coffee, new IncidentScheduleEntry(
                "D0I1", "I1", new TimeRange(0, 575, 580), 590, 630)),
            new DayIncidentPlan(printer, new IncidentScheduleEntry(
                "review_before_miss", "I2", new TimeRange(0, 590, 595), 600, 700))
        };
        UseDay(new DayData(context.Day.ScheduleData,
            new List<EvidenceDefinition>(context.Day.EvidenceDatabase.All), plans));
        var service = Create();
        var order = new List<string>();
        context.Processor.ReviewCompleted += (review, result) => order.Add("review");
        service.IncidentOverdue += incident => order.Add("overdue");
        QueuedHrReview job = context.Submit("review_before_miss", "dave",
            new List<string> { "E_OFFICE_SOCIAL" }, new GameTime(0, 615, 0, 1));
        Assert.That(job.ReviewAt.TotalMilliseconds, Is.EqualTo(Late(630).TotalMilliseconds));

        context.Scheduler.AdvanceTo(Late(630));

        Assert.That(order, Is.EqualTo(new[] { "review", "overdue" }));
        Assert.That(progress.Credibility, Is.EqualTo(40));
        Assert.That(progress.History.Count, Is.EqualTo(1));
        Assert.That(progress.MissedDeadlines.Count, Is.EqualTo(1));
        Assert.That(progress.History[0].CredibilityBefore, Is.EqualTo(100));
        Assert.That(progress.History[0].CredibilityAfter, Is.EqualTo(75));
        Assert.That(progress.MissedDeadlines[0].CredibilityBefore, Is.EqualTo(75));
        Assert.That(progress.MissedDeadlines[0].CredibilityAfter, Is.EqualTo(40));
    }
}
