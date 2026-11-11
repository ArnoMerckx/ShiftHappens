using System;
using System.Collections.Generic;
using NUnit.Framework;

public class DelegationServiceTests
{
    private sealed class Context
    {
        public DayData Day;
        public GameClock Clock;
        public EventScheduler Scheduler;
        public DelegationService Service;
        public bool AcceptsWork = true;

        public Context(DayData day)
        {
            Day = day;
            Clock = new GameClock(day.ScheduleData.Settings.OpensAt, 40);
            Scheduler = new EventScheduler(Clock);
            Service = new DelegationService(day, Scheduler, () => AcceptsWork);
        }

        public RequestInstance Delivered(string id = "D3R1")
        {
            Assert.That(Service.TryGetDelivered(id, out var instance), Is.True);
            return instance;
        }
    }

    private static DayRequestPlan Plan(string id = "D3R1", GameTime? cutoff = null,
        int delivery = 595, bool fixedWindow = true, bool allowSelfHelp = false)
    {
        return new DayRequestPlan(id,
            new RequestData("pc_help", "PC help", "Arrange help.", "alice",
                EmployeeCapability.ComputerSupport, allowSelfHelp),
            new RequestProfileData("desktop", EmployeeCapability.ComputerSupport,
                new List<RequestSuitabilityEntry>
                {
                    new RequestSuitabilityEntry("dave", 0), new RequestSuitabilityEntry("steve", 1)
                }, new List<string> { "alice", "bob" }),
            new GameTime(3, delivery),
            fixedWindow ? (TimeRange?)new TimeRange(3, 630, 645) : null, cutoff);
    }

    private static DayData Day(List<DayRequestPlan> plans = null, bool daveBlocked = false)
    {
        var records = new List<ApprovedScheduleRecord>();
        if (daveBlocked)
            records.Add(new ApprovedScheduleRecord("inspection", "dave", "printer",
                new TimeRange(3, 630, 645), 100, true, true));
        var availability = new List<EmployeeAvailability>();
        foreach (string id in new[] { "alice", "bob", "dave", "steve" })
            availability.Add(new EmployeeAvailability(id, new TimeRange(3, 540, 1020)));
        var schedule = new DayScheduleData(
            new WorkdaySettings("service_day", "Service day", 3, 540, 1020, 40),
            new List<string> { "alice", "bob", "dave", "steve" }, records,
            new List<ApprovedScheduleRecord>(), new List<Claim>(), ordinaryAvailability: availability);
        return new DayData(schedule, new List<EvidenceDefinition>(), new List<DayIncidentPlan>(),
            plans ?? new List<DayRequestPlan> { Plan() });
    }

    private static void AssertRejected(DelegationSubmissionResult result)
    {
        Assert.That(result.IsAccepted, Is.False);
        Assert.That(result.Decision, Is.Null);
        Assert.That(string.IsNullOrWhiteSpace(result.Reason), Is.False);
    }

    [Test]
    public void Constructor_RejectsMissingDependenciesAndNonOpeningOrStoppedScheduler()
    {
        var day = Day();
        var clock = new GameClock(day.ScheduleData.Settings.OpensAt);
        var scheduler = new EventScheduler(clock);
        Assert.Throws<ArgumentNullException>(() => new DelegationService(null, scheduler, () => true));
        Assert.Throws<ArgumentNullException>(() => new DelegationService(day, null, () => true));
        Assert.Throws<ArgumentNullException>(() => new DelegationService(day, scheduler, null));
        scheduler.AdvanceTo(new GameTime(3, 541));
        Assert.Throws<ArgumentException>(() => new DelegationService(day, scheduler, () => true));
        var stopped = new EventScheduler(new GameClock(day.ScheduleData.Settings.OpensAt));
        stopped.Stop();
        Assert.Throws<ArgumentException>(() => new DelegationService(day, stopped, () => true));
    }

    [Test]
    public void HistoryViews_AreNonNullReadOnlyAndReflectPublishedState()
    {
        var context = new Context(Day());
        var delivered = context.Service.DeliveredRequests;
        var expiries = context.Service.Expiries;
        Assert.That(delivered, Is.Not.Null.And.Empty);
        Assert.That(expiries, Is.Not.Null.And.Empty);
        Assert.Throws<NotSupportedException>(() => ((IList<RequestInstance>)delivered).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<RequestExpiryEntry>)expiries).Clear());
        context.Service.RequestExpired += entry =>
        {
            Assert.That(expiries.Count, Is.EqualTo(1));
            Assert.That(expiries[0], Is.SameAs(entry));
            Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Expired));
        };
        context.Scheduler.AdvanceTo(new GameTime(3, 700));
        Assert.That(delivered.Count, Is.EqualTo(1));
        Assert.That(expiries.Count, Is.EqualTo(1));
    }

    [Test]
    public void Delivery_UsesExactTimeAndStoresBeforeNotifying_AndDuplicateCallbacksDoNothing()
    {
        var context = new Context(Day());
        ScheduledEvent delivery = context.Scheduler.PeekNext();
        int notices = 0;
        context.Service.RequestDelivered += instance =>
        {
            notices++;
            Assert.That(context.Delivered(), Is.SameAs(instance));
            Assert.That(context.Service.DeliveredRequests.Count, Is.EqualTo(1));
            Assert.That(instance.Status, Is.EqualTo(RequestStatus.Open));
        };
        delivery.Callback();
        context.Scheduler.AdvanceTo(new GameTime(3, 594, 59, 999));
        Assert.That(context.Service.TryGetDelivered("D3R1", out var absent), Is.False);
        Assert.That(absent, Is.Null);
        context.Scheduler.AdvanceTo(new GameTime(3, 595));
        delivery.Callback();
        Assert.That(notices, Is.EqualTo(1));
        Assert.That(context.Service.DeliveredRequests.Count, Is.EqualTo(1));
    }

    [Test]
    public void DeliverySubscriber_CanImmediatelySubmitAtDeliveryEqualToCutoff()
    {
        var context = new Context(Day(new List<DayRequestPlan> { Plan(delivery: 630) }));
        DelegationSubmissionResult result = null;
        context.Service.RequestDelivered += instance => result = context.Service.TrySubmit(instance.InstanceId, "steve");
        context.Scheduler.AdvanceTo(new GameTime(3, 631));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsAccepted, Is.True);
        Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Resolved));
        Assert.That(context.Service.Expiries, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("unknown")]
    [TestCase("d3r1")]
    public void InvalidRequestIds_RejectWithoutChangesOrNotifications(string id)
    {
        var context = new Context(Day());
        context.Scheduler.AdvanceTo(new GameTime(3, 600));
        int decisions = 0;
        context.Service.DecisionResolved += decision => decisions++;
        Assert.That(context.Service.TryGetDelivered(id, out var missing), Is.False);
        Assert.That(missing, Is.Null);
        AssertRejected(context.Service.TrySubmit(id, "steve"));
        Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Delivered().Decision, Is.Null);
        Assert.That(context.Service.Expiries, Is.Empty);
        Assert.That(decisions, Is.Zero);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("unknown")]
    [TestCase("Steve")]
    [TestCase("alice")]
    public void InvalidHelperOrForbiddenSelfHelp_RejectsWithoutConsumingRequest(string helper)
    {
        var context = new Context(Day());
        context.Scheduler.AdvanceTo(new GameTime(3, 600));
        int decisions = 0;
        context.Service.DecisionResolved += decision => decisions++;
        AssertRejected(context.Service.TrySubmit("D3R1", helper));
        Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Delivered().Decision, Is.Null);
        Assert.That(context.Service.Expiries, Is.Empty);
        Assert.That(decisions, Is.Zero);
        Assert.That(context.Service.TrySubmit("D3R1", "steve").IsAccepted, Is.True);
    }

    [Test]
    public void UndeliveredAndClosedGateCommands_RejectWithoutCreatingRuntimeState()
    {
        var context = new Context(Day());
        AssertRejected(context.Service.TrySubmit("D3R1", "steve"));
        Assert.That(context.Service.DeliveredRequests, Is.Empty);
        context.AcceptsWork = false;
        context.Scheduler.AdvanceTo(new GameTime(3, 600));
        AssertRejected(context.Service.TrySubmit("D3R1", "steve"));
        Assert.That(context.Service.DeliveredRequests, Is.Empty);
        context.Scheduler.AdvanceTo(new GameTime(3, 700));
        Assert.That(context.Service.Expiries, Is.Empty);
    }

    [TestCase("dave", false, DelegationOutcome.BestAvailable)]
    [TestCase("steve", false, DelegationOutcome.CapableAlternative)]
    [TestCase("bob", false, DelegationOutcome.Unsuitable)]
    [TestCase("dave", true, DelegationOutcome.Unavailable)]
    public void EveryValidOutcome_CommitsOnceBeforeEvent_AndReentrantSubmitRejects(
        string helper, bool daveBlocked, DelegationOutcome outcome)
    {
        var context = new Context(Day(daveBlocked: daveBlocked));
        var at = new GameTime(3, 600, 12, 345);
        context.Scheduler.AdvanceTo(at);
        int decisions = 0;
        context.Service.DecisionResolved += decision =>
        {
            decisions++;
            Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Resolved));
            Assert.That(context.Delivered().Decision, Is.SameAs(decision));
            AssertRejected(context.Service.TrySubmit("D3R1", "steve"));
        };
        var result = context.Service.TrySubmit("D3R1", helper);
        Assert.That(result.IsAccepted, Is.True);
        Assert.That(result.Decision.Outcome, Is.EqualTo(outcome));
        Assert.That(result.Decision.HelperId, Is.EqualTo(helper));
        Assert.That(result.Decision.SubmittedAt.TotalMilliseconds, Is.EqualTo(at.TotalMilliseconds));
        Assert.That(result.Reason, Is.EqualTo(result.Decision.Explanation));
        AssertRejected(context.Service.TrySubmit("D3R1", "bob"));
        context.Scheduler.AdvanceTo(new GameTime(3, 700));
        Assert.That(context.Service.Expiries, Is.Empty);
        Assert.That(decisions, Is.EqualTo(1));
        Assert.That(context.Delivered().Decision, Is.SameAs(result.Decision));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExactCutoffAccepted_OneMillisecondLaterExpiresOnce(bool earlier)
    {
        var cutoff = earlier ? new GameTime(3, 620, 12, 345) : new GameTime(3, 630);
        var plan = Plan(cutoff: earlier ? (GameTime?)cutoff : null);
        var accepted = new Context(Day(new List<DayRequestPlan> { plan }));
        accepted.Scheduler.AdvanceTo(cutoff);
        Assert.That(accepted.Delivered().Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(accepted.Service.TrySubmit("D3R1", "steve").IsAccepted, Is.True);

        var expired = new Context(Day(new List<DayRequestPlan> { plan }));
        expired.Scheduler.AdvanceTo(plan.DeliveryAt);
        ScheduledEvent expiryCallback = expired.Scheduler.PeekNext();
        int notices = 0;
        expired.Service.RequestExpired += entry =>
        {
            notices++;
            Assert.That(expired.Service.Expiries[0], Is.SameAs(entry));
            AssertRejected(expired.Service.TrySubmit("D3R1", "steve"));
        };
        expiryCallback.Callback();
        expired.Scheduler.AdvanceTo(cutoff);
        Assert.That(expired.Service.Expiries, Is.Empty);
        var expiresAt = new GameTime(cutoff.TotalMilliseconds + 1);
        expired.Scheduler.AdvanceTo(expiresAt);
        expiryCallback.Callback();
        expired.Scheduler.AdvanceTo(new GameTime(3, 1000));
        AssertRejected(expired.Service.TrySubmit("D3R1", "steve"));
        Assert.That(notices, Is.EqualTo(1));
        Assert.That(expired.Delivered().Decision, Is.Null);
        var record = expired.Service.Expiries[0];
        Assert.That(record.RequestInstanceId, Is.EqualTo("D3R1"));
        Assert.That(record.RequestId, Is.EqualTo("pc_help"));
        Assert.That(record.RequesterId, Is.EqualTo("alice"));
        Assert.That(record.ExpiresAt.TotalMilliseconds, Is.EqualTo(expiresAt.TotalMilliseconds));
        Assert.That(record.RecordedAt.TotalMilliseconds, Is.EqualTo(expiresAt.TotalMilliseconds));
    }

    [Test]
    public void LargeJump_ProcessesRepeatedTemplatesAsSeparateInstancesAtTheirOwnTimes()
    {
        var first = Plan();
        var second = new DayRequestPlan("D3R2", first.Request, first.Profile,
            new GameTime(3, 700), new TimeRange(3, 750, 760));
        var context = new Context(Day(new List<DayRequestPlan> { second, first }));
        var sequence = new List<string>();
        context.Service.RequestDelivered += instance => sequence.Add("delivered:" + instance.InstanceId);
        context.Service.RequestExpired += entry => sequence.Add("expired:" + entry.RequestInstanceId);
        context.Scheduler.AdvanceTo(new GameTime(3, 1000));
        CollectionAssert.AreEqual(new[] { "delivered:D3R1", "expired:D3R1", "delivered:D3R2", "expired:D3R2" }, sequence);
        Assert.That(context.Service.Expiries[0].RecordedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(3, 630).TotalMilliseconds + 1));
        Assert.That(context.Service.Expiries[1].RecordedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(3, 750).TotalMilliseconds + 1));
    }

    [Test]
    public void SameHelperCanResolveOverlappingRequests_WithoutOccupancyOrSharedSessionState()
    {
        var day = Day(new List<DayRequestPlan> { Plan(), Plan("D3R2") });
        var context = new Context(day);
        var independent = new Context(day);
        context.Scheduler.AdvanceTo(new GameTime(3, 600));
        independent.Scheduler.AdvanceTo(new GameTime(3, 600));
        Assert.That(context.Service.TrySubmit("D3R1", "steve").IsAccepted, Is.True);
        Assert.That(context.Service.TrySubmit("D3R2", "steve").IsAccepted, Is.True);
        Assert.That(independent.Delivered().Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(independent.Delivered().Decision, Is.Null);
        Assert.That(independent.Delivered(), Is.Not.SameAs(context.Delivered()));
    }

    [Test]
    public void GateClosingAfterDelivery_RejectsSubmissionWithoutExpiringItEarly()
    {
        var context = new Context(Day());
        context.Scheduler.AdvanceTo(new GameTime(3, 600));
        context.AcceptsWork = false;
        AssertRejected(context.Service.TrySubmit("D3R1", "steve"));
        Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Service.Expiries, Is.Empty);
    }

    [Test]
    public void StoppedScheduler_PreventsSubmissionAndSavedExpiryCallback()
    {
        var context = new Context(Day());
        context.Scheduler.AdvanceTo(new GameTime(3, 600));
        var expiry = context.Scheduler.PeekNext();
        context.Scheduler.Stop();
        AssertRejected(context.Service.TrySubmit("D3R1", "steve"));
        context.Clock.AdvanceTo(new GameTime(3, 700));
        expiry.Callback();
        Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(context.Service.Expiries, Is.Empty);
    }

    [Test]
    public void PausedScheduler_DoesNotDeliverOrExpireUntilResumed()
    {
        var context = new Context(Day());
        context.Clock.SetPaused(true);
        context.Scheduler.AdvanceTo(new GameTime(3, 700));
        Assert.That(context.Service.DeliveredRequests, Is.Empty);
        context.Clock.SetPaused(false);
        context.Scheduler.AdvanceTo(new GameTime(3, 600));
        context.Clock.SetPaused(true);
        context.Scheduler.AdvanceTo(new GameTime(3, 700));
        Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Open));
        context.Clock.SetPaused(false);
        context.Scheduler.AdvanceTo(new GameTime(3, 700));
        Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Expired));
        Assert.That(context.Service.Expiries.Count, Is.EqualTo(1));
    }

    [Test]
    public void EndOfShift_SubmissionUsesExactClosingEvenIfSuppliedGateRemainsOpen()
    {
        var day = Day(new List<DayRequestPlan> { Plan(fixedWindow: false) });
        var lastInstant = new Context(day);
        lastInstant.Scheduler.AdvanceTo(new GameTime(day.ScheduleData.Settings.ClosesAt.TotalMilliseconds - 1));
        Assert.That(lastInstant.Service.TrySubmit("D3R1", "steve").IsAccepted, Is.True);
        var atClosing = new Context(day);
        atClosing.Scheduler.AdvanceTo(day.ScheduleData.Settings.ClosesAt);
        AssertRejected(atClosing.Service.TrySubmit("D3R1", "steve"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EndOfShift_ExpiresBeforeClosingRestrictionRegardlessOfRegistrationOrder(bool closingFirst)
    {
        var day = Day(new List<DayRequestPlan> { Plan(fixedWindow: false) });
        var closing = day.ScheduleData.Settings.ClosesAt;
        var clock = new GameClock(day.ScheduleData.Settings.OpensAt);
        var scheduler = new EventScheduler(clock);
        DelegationService service = null;
        var order = new List<string>();
        Action registerClosing = () => scheduler.Schedule(new ScheduledEvent(
            "closing_restriction", closing, ScheduledEventPriority.WorkdayClosing, () =>
            {
                order.Add("closing");
                Assert.That(service.Expiries.Count, Is.EqualTo(1));
                scheduler.RestrictToPriority(ScheduledEventPriority.HrReview);
            }));
        if (closingFirst) registerClosing();
        service = new DelegationService(day, scheduler,
            () => scheduler.Now.TotalMilliseconds < closing.TotalMilliseconds);
        if (!closingFirst) registerClosing();
        service.RequestExpired += entry =>
        {
            order.Add("expiry");
            Assert.That(scheduler.Now.TotalMilliseconds, Is.EqualTo(closing.TotalMilliseconds));
            Assert.That(entry.RecordedAt.TotalMilliseconds, Is.EqualTo(closing.TotalMilliseconds));
            Assert.That(entry.ExpiresAt.TotalMilliseconds, Is.EqualTo(closing.TotalMilliseconds));
            AssertRejected(service.TrySubmit("D3R1", "steve"));
        };
        scheduler.AdvanceTo(new GameTime(closing.TotalMilliseconds - 1));
        Assert.That(service.Expiries, Is.Empty);
        scheduler.AdvanceTo(closing);
        CollectionAssert.AreEqual(new[] { "expiry", "closing" }, order);
        Assert.That(service.TryGetDelivered("D3R1", out var instance), Is.True);
        Assert.That(instance.Status, Is.EqualTo(RequestStatus.Expired));
        scheduler.AdvanceTo(new GameTime(closing.TotalMilliseconds + 1));
        Assert.That(service.Expiries.Count, Is.EqualTo(1));
    }

    [Test]
    public void ExplicitlyAllowedSelfHelp_IsAnAcceptedConsequentialChoice()
    {
        var context = new Context(Day(new List<DayRequestPlan> { Plan(allowSelfHelp: true) }));
        context.Scheduler.AdvanceTo(new GameTime(3, 600));
        var result = context.Service.TrySubmit("D3R1", "alice");
        Assert.That(result.IsAccepted, Is.True);
        Assert.That(result.Decision.Outcome, Is.EqualTo(DelegationOutcome.Unsuitable));
        Assert.That(context.Delivered().Status, Is.EqualTo(RequestStatus.Resolved));
    }

    [Test]
    public void SubmissionResult_RejectsMalformedConstruction()
    {
        Assert.Throws<ArgumentNullException>(() => DelegationSubmissionResult.Accepted(null));
        foreach (string reason in new[] { null, "", " " })
            Assert.Throws<ArgumentException>(() => DelegationSubmissionResult.Rejected(reason));
    }

    [Test]
    public void ExpiryRecord_RequiresExpiredInstanceAndNonEarlyTimestamp()
    {
        var plan = Plan();
        var instance = new RequestInstance(plan, new GameTime(3, 1020));
        Assert.Throws<ArgumentNullException>(() => new RequestExpiryEntry(null, instance.ExpiresAt));
        Assert.Throws<ArgumentException>(() => new RequestExpiryEntry(instance, instance.ExpiresAt));
        Assert.That(instance.TryExpire(instance.ExpiresAt), Is.True);
        Assert.Throws<ArgumentException>(() => new RequestExpiryEntry(instance,
            new GameTime(instance.ExpiresAt.TotalMilliseconds - 1)));
    }

    [Test]
    public void EmptyRequestDay_RemainsUsableWithEmptyHistories()
    {
        var context = new Context(Day(new List<DayRequestPlan>()));
        context.Scheduler.AdvanceTo(new GameTime(3, 1000));
        Assert.That(context.Service.DeliveredRequests, Is.Empty);
        Assert.That(context.Service.Expiries, Is.Empty);
        AssertRejected(context.Service.TrySubmit("D3R1", "steve"));
    }
}
