using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DayDeliveryServiceTests
{
    private DayData day;
    private GameClock clock;
    private EventScheduler scheduler;
    private readonly List<UnityEngine.Object> temporaryObjects =
        new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        var definition = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
        Assert.That(definition, Is.Not.Null);
        UseDay(definition.GetData());
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var temporary in temporaryObjects)
        {
            if (temporary != null)
                UnityEngine.Object.DestroyImmediate(temporary);
        }

        temporaryObjects.Clear();
    }

    private void UseDay(DayData data)
    {
        day = data;
        clock = new GameClock(
            data.ScheduleData.Settings.OpensAt,
            data.ScheduleData.Settings.GameSecondsPerRealSecond);
        scheduler = new EventScheduler(clock);
    }

    private void UseEvidenceDay(
        List<EvidenceDefinition> documents, int dayIndex = 0)
    {
        var schedule = new DayScheduleData(
            new WorkdaySettings("test_day", "Test day", dayIndex, 540, 1020, 20),
            new List<string>(), new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>());

        UseDay(new DayData(schedule, documents, new List<DayIncidentPlan>()));
    }

    private EvidenceDefinition MakeDocument(
        string id, int availableDay, int availableMinute,
        EvidenceSource source = EvidenceSource.Physical)
    {
        var document = ScriptableObject.CreateInstance<EvidenceDefinition>();
        temporaryObjects.Add(document);
        var serialized = new SerializedObject(document);
        serialized.FindProperty("evidenceId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = "Test document";
        serialized.FindProperty("bodyText").stringValue = "Test document content.";
        serialized.FindProperty("source").intValue = (int)source;
        serialized.FindProperty("availableDayIndex").intValue = availableDay;
        serialized.FindProperty("availableMinute").intValue = availableMinute;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return document;
    }

    private DayDeliveryService CreateService()
    {
        return new DayDeliveryService(day, scheduler);
    }

    private static GameTime At(int minute)
    {
        return new GameTime(0, minute);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Constructor_RejectsNullInputWithoutScheduling(bool missingDay)
    {
        Assert.Throws<ArgumentNullException>(() => new DayDeliveryService(
            missingDay ? null : day,
            missingDay ? scheduler : null));
        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void Constructor_RequiresExactOpeningInstant(int offsetMilliseconds)
    {
        clock = new GameClock(new GameTime(
            day.ScheduleData.Settings.OpensAt.TotalMilliseconds
            + offsetMilliseconds));
        scheduler = new EventScheduler(clock);

        Assert.Throws<ArgumentException>(() => CreateService());
        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [Test]
    public void Construction_RegistersWorkWithoutExposingFutureIncidents()
    {
        DayDeliveryService service = CreateService();

        Assert.That(scheduler.PendingCount, Is.EqualTo(10));
        Assert.That(service.DeliveredIncidents, Is.Empty);
        Assert.That(clock.Now.TotalMilliseconds,
            Is.EqualTo(day.ScheduleData.Settings.OpensAt.TotalMilliseconds));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void GetDelivered_RejectsBlankId(string id)
    {
        DayDeliveryService service = CreateService();
        Assert.Throws<ArgumentException>(() => service.GetDelivered(id));
    }

    [TestCase("D0I1")]
    [TestCase("unknown")]
    public void UndeliveredOrUnknownIncident_IsNotExposed(string id)
    {
        DayDeliveryService service = CreateService();
        Assert.Throws<KeyNotFoundException>(() => service.GetDelivered(id));
    }

    [Test]
    public void DeliveredLookup_IsCaseSensitive()
    {
        DayDeliveryService service = CreateService();
        scheduler.AdvanceTo(At(590));

        Assert.That(service.GetDelivered("D0I1").InstanceId, Is.EqualTo("D0I1"));
        Assert.Throws<KeyNotFoundException>(() => service.GetDelivered("d0i1"));
    }

    [TestCase("D0I1", 590)]
    [TestCase("D0I2", 675)]
    [TestCase("D0I3", 840)]
    public void Incident_AppearsAtExactDeliveryAndIsStoredBeforeNotification(
        string instanceId, int deliveryMinute)
    {
        DayDeliveryService service = CreateService();
        var observed = new List<string>();
        service.IncidentDelivered += instance =>
        {
            observed.Add(instance.InstanceId);
            Assert.That(service.GetDelivered(instance.InstanceId), Is.SameAs(instance));
            Assert.That(clock.Now.TotalMilliseconds,
                Is.EqualTo(At(instance.DeliveryMinute).TotalMilliseconds));
            Assert.That(instance.Status, Is.EqualTo(IncidentStatus.Open));
            Assert.That(instance.SubmittedReport, Is.Null);
            Assert.That(instance.Decision, Is.Null);
        };

        scheduler.AdvanceTo(new GameTime(At(deliveryMinute).TotalMilliseconds - 1));
        Assert.Throws<KeyNotFoundException>(() => service.GetDelivered(instanceId));

        scheduler.AdvanceTo(At(deliveryMinute));
        IncidentInstance delivered = service.GetDelivered(instanceId);
        Assert.That(delivered.InstanceId, Is.EqualTo(instanceId));
        Assert.That(observed.FindAll(id => id == instanceId).Count, Is.EqualTo(1));

        scheduler.AdvanceTo(At(deliveryMinute));
        scheduler.AdvanceTo(day.ScheduleData.Settings.ClosesAt);
        Assert.That(observed.FindAll(id => id == instanceId).Count, Is.EqualTo(1));
        Assert.That(service.GetDelivered(instanceId), Is.SameAs(delivered));
    }

    [Test]
    public void Opening_AnnouncesAllMondayEvidenceOnceIncludingReceipt()
    {
        DayDeliveryService service = CreateService();
        var observed = new List<string>();
        service.EvidenceAvailable += id =>
        {
            observed.Add(id);
            Assert.That(clock.Now.TotalMilliseconds,
                Is.EqualTo(day.ScheduleData.Settings.OpensAt.TotalMilliseconds));
        };

        scheduler.AdvanceTo(clock.Now);
        scheduler.AdvanceRealSeconds(0);

        Assert.That(observed, Is.EquivalentTo(new[]
        {
            "E_SCHEDULE", "E_BOB_DENTIST", "E_ALICE_MEETING", "E_DAVE_PRINTER",
            "E_KAREN_COLLECTION", "E_OFFICE_SOCIAL", "E_SUPPLIES_RECEIPT"
        }));
        Assert.That(service.DeliveredIncidents, Is.Empty);
    }

    [TestCase(EvidenceSource.InWatch)]
    [TestCase(EvidenceSource.Excess)]
    [TestCase(EvidenceSource.ShareLess)]
    [TestCase(EvidenceSource.Physical)]
    public void EvidenceAvailability_IsTimedWithoutDiscoveryOrKnowledgeGrants(
        EvidenceSource source)
    {
        UseEvidenceDay(new List<EvidenceDefinition>
        {
            MakeDocument("timed", 0, 600, source)
        });
        DayDeliveryService service = CreateService();
        var evidenceState = new EvidenceService(day.EvidenceDatabase);
        int notifications = 0;
        service.EvidenceAvailable += id =>
        {
            notifications++;
            Assert.That(id, Is.EqualTo("timed"));
            Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(At(600).TotalMilliseconds));
            Assert.That(evidenceState.IsAvailable(id, clock.Now.DayIndex,
                clock.Now.MinuteOfDay), Is.True);
        };

        scheduler.AdvanceTo(new GameTime(At(600).TotalMilliseconds - 1));
        Assert.That(notifications, Is.Zero);
        Assert.That(evidenceState.IsAvailable("timed", 0, 599), Is.False);

        scheduler.AdvanceTo(At(600));
        scheduler.AdvanceTo(day.ScheduleData.Settings.ClosesAt);

        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(evidenceState.IsDiscovered("timed"), Is.False);
        Assert.That(evidenceState.IsRead("timed"), Is.False);
        Assert.That(evidenceState.CanAttach("timed", 0, 600), Is.False);
        Assert.That(day.ScheduleData.WorldApprovedRecords, Is.Empty);
        Assert.That(day.ScheduleData.HrKnowledge.ApprovedRecords, Is.Empty);
        Assert.That(day.ScheduleData.HrKnowledge.KnownClaims, Is.Empty);
    }

    [TestCase(0, 500, 0)]
    [TestCase(0, 1000, 1)]
    public void EvidenceAvailableBeforeWorkday_IsAnnouncedAtOpening(
        int availableDay, int availableMinute, int workday)
    {
        UseEvidenceDay(new List<EvidenceDefinition>
        {
            MakeDocument("earlier", availableDay, availableMinute)
        }, workday);
        DayDeliveryService service = CreateService();
        var observed = new List<long>();
        service.EvidenceAvailable += id => observed.Add(clock.Now.TotalMilliseconds);

        Assert.That(observed, Is.Empty);
        scheduler.AdvanceTo(clock.Now);
        scheduler.AdvanceTo(clock.Now);

        Assert.That(observed, Is.EqualTo(new[]
        {
            day.ScheduleData.Settings.OpensAt.TotalMilliseconds
        }));
    }

    [TestCase(0, 1020)]
    [TestCase(0, 1100)]
    [TestCase(1, 540)]
    public void EvidenceFirstAvailableAtOrAfterClosing_IsNotScheduled(
        int availableDay, int availableMinute)
    {
        UseEvidenceDay(new List<EvidenceDefinition>
        {
            MakeDocument("later", availableDay, availableMinute)
        });
        DayDeliveryService service = CreateService();
        int notifications = 0;
        service.EvidenceAvailable += id => notifications++;

        Assert.That(scheduler.PendingCount, Is.Zero);
        scheduler.AdvanceTo(new GameTime(1, 600));

        Assert.That(notifications, Is.Zero);
        Assert.That(service.DeliveredIncidents, Is.Empty);
    }

    [Test]
    public void SameTime_EvidenceAvailabilityPrecedesIncidentNotice()
    {
        var documents = new List<EvidenceDefinition>(day.EvidenceDatabase.All);
        int index = documents.FindIndex(document => document.EvidenceId == "E_OFFICE_SOCIAL");
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        documents[index] = MakeDocument("arrival", 0, 590, EvidenceSource.InWatch);
        UseDay(new DayData(day.ScheduleData, documents,
            new List<DayIncidentPlan>(day.Incidents)));

        DayDeliveryService service = CreateService();
        var observed = new List<string>();
        service.EvidenceAvailable += id =>
        {
            if (id == "arrival") observed.Add("evidence");
        };
        service.IncidentDelivered += instance => observed.Add(instance.InstanceId);

        scheduler.AdvanceTo(At(590));

        Assert.That(observed, Is.EqualTo(new[] { "evidence", "D0I1" }));
    }

    [Test]
    public void PausingFromNotice_StopsLaterDeliveriesUntilResumed()
    {
        DayDeliveryService service = CreateService();
        var observed = new List<string>();
        service.IncidentDelivered += instance =>
        {
            observed.Add(instance.InstanceId);
            if (instance.InstanceId == "D0I1") clock.SetPaused(true);
        };

        scheduler.AdvanceTo(day.ScheduleData.Settings.ClosesAt);

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(At(590).TotalMilliseconds));
        Assert.That(service.DeliveredIncidents.Count, Is.EqualTo(1));
        Assert.Throws<KeyNotFoundException>(() => service.GetDelivered("D0I2"));

        clock.SetPaused(false);
        scheduler.AdvanceTo(day.ScheduleData.Settings.ClosesAt);
        Assert.That(observed, Is.EqualTo(new[] { "D0I1", "D0I2", "D0I3" }));
    }

    [Test]
    public void SeparateSessions_HaveIndependentDeliveryAndIncidentInstances()
    {
        DayDeliveryService first = CreateService();
        scheduler.AdvanceTo(At(590));

        var secondClock = new GameClock(day.ScheduleData.Settings.OpensAt);
        var secondScheduler = new EventScheduler(secondClock);
        var second = new DayDeliveryService(day, secondScheduler);

        Assert.That(second.DeliveredIncidents, Is.Empty);
        Assert.Throws<KeyNotFoundException>(() => second.GetDelivered("D0I1"));

        secondScheduler.AdvanceTo(At(590));
        Assert.That(second.GetDelivered("D0I1"), Is.Not.SameAs(first.GetDelivered("D0I1")));
        Assert.That(second.GetDelivered("D0I1").Status, Is.EqualTo(IncidentStatus.Open));
    }

    [Test]
    public void DeliveredCollection_IsReadOnlyAndReflectsNewDeliveries()
    {
        DayDeliveryService service = CreateService();
        IReadOnlyList<IncidentInstance> view = service.DeliveredIncidents;
        Assert.That(view, Is.Empty);

        scheduler.AdvanceTo(At(590));

        Assert.That(view.Count, Is.EqualTo(1));
        Assert.That(service.DeliveredIncidents, Is.SameAs(view));
        Assert.Throws<NotSupportedException>(
            () => ((IList<IncidentInstance>)view).Clear());
        Assert.That(service.GetDelivered("D0I1"), Is.SameAs(view[0]));
    }

    [Test]
    public void FullDayJump_DeliversEachIncidentOnceAtItsOwnTime()
    {
        DayDeliveryService service = CreateService();
        var ids = new List<string>();
        var times = new List<long>();
        service.IncidentDelivered += instance =>
        {
            ids.Add(instance.InstanceId);
            times.Add(clock.Now.TotalMilliseconds);
        };

        scheduler.AdvanceTo(day.ScheduleData.Settings.ClosesAt);
        scheduler.AdvanceTo(day.ScheduleData.Settings.ClosesAt);

        Assert.That(ids, Is.EqualTo(new[] { "D0I1", "D0I2", "D0I3" }));
        Assert.That(times, Is.EqualTo(new[]
        {
            At(590).TotalMilliseconds,
            At(675).TotalMilliseconds,
            At(840).TotalMilliseconds
        }));
        Assert.That(service.DeliveredIncidents.Count, Is.EqualTo(3));
        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [Test]
    public void EmptyDay_CanAdvanceWithoutSubscribersOrDeliveries()
    {
        UseEvidenceDay(new List<EvidenceDefinition>());
        DayDeliveryService service = CreateService();

        scheduler.AdvanceTo(day.ScheduleData.Settings.ClosesAt);

        Assert.That(service.DeliveredIncidents, Is.Empty);
        Assert.That(scheduler.PendingCount, Is.Zero);
        Assert.That(clock.Now.TotalMilliseconds,
            Is.EqualTo(day.ScheduleData.Settings.ClosesAt.TotalMilliseconds));
    }
}
