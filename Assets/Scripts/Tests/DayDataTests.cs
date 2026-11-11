using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DayDataTests
{
    private DayScheduleData scheduleData;
    private List<EvidenceDefinition> evidence;
    private List<DayIncidentPlan> incidents;
    private IncidentDefinition definition;
    private LocationDefinition location;
    private readonly List<UnityEngine.Object> temporaryObjects =
        new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        var original = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I3MissingSupplies.asset");
        Assert.That(original, Is.Not.Null);
        Assert.That(original.Location, Is.Not.Null);

        definition = Copy(original);
        location = Copy(original.Location);
        Edit(definition, data =>
            data.FindProperty("location").objectReferenceValue = location);

        scheduleData = new DayScheduleData(
            new WorkdaySettings("monday", "Monday", 0, 540, 1020, 20),
            new List<string> { "steve" },
            new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(),
            new List<Claim>());

        evidence = new List<EvidenceDefinition>();
        incidents = new List<DayIncidentPlan> { Plan() };
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = temporaryObjects.Count - 1; i >= 0; i--)
        {
            if (temporaryObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(temporaryObjects[i]);
        }

        temporaryObjects.Clear();
    }

    private T Copy<T>(T original) where T : UnityEngine.Object
    {
        T clone = UnityEngine.Object.Instantiate(original);
        temporaryObjects.Add(clone);
        return clone;
    }

    private static void Edit(
        UnityEngine.Object target, Action<SerializedObject> change)
    {
        var data = new SerializedObject(target);
        change(data);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private DayIncidentPlan Plan(
        string id = "D0I3", int day = 0,
        int delivery = 840, int deadline = 900)
    {
        return new DayIncidentPlan(definition, new IncidentScheduleEntry(
            id, "I3", new TimeRange(day, 500, 510), delivery, deadline), "steve");
    }

    private DayData Create()
    {
        return new DayData(scheduleData, evidence, incidents);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void NullInput_IsRejected(int input)
    {
        if (input == 0) scheduleData = null;
        if (input == 1) evidence = null;
        if (input == 2) incidents = null;

        Assert.Throws<ArgumentNullException>(() => Create());
    }

    [Test]
    public void NullIncidentPlan_IsRejected()
    {
        incidents.Add(null);
        Assert.Throws<ArgumentException>(() => Create());
    }

    [Test]
    public void DuplicateInstanceId_IsRejected()
    {
        incidents.Add(Plan());
        Assert.Throws<ArgumentException>(() => Create());
    }

    [Test]
    public void IncidentFromAnotherDay_IsRejected()
    {
        incidents.Clear();
        incidents.Add(Plan(day: 1));
        Assert.Throws<ArgumentException>(() => Create());
    }

    [TestCase(539, 600)]
    [TestCase(1020, 1030)]
    [TestCase(600, 1020)]
    [TestCase(600, 1021)]
    public void DeliveryOrDeadlineOutsideWorkPolicy_IsRejected(
        int delivery, int deadline)
    {
        incidents.Clear();
        incidents.Add(Plan(delivery: delivery, deadline: deadline));
        Assert.Throws<ArgumentException>(() => Create());
    }

    [TestCase(540, 600)]
    [TestCase(1018, 1019)]
    public void OpeningAndLastPreClosingMinute_AreSupported(
        int delivery, int deadline)
    {
        incidents.Clear();
        incidents.Add(Plan(delivery: delivery, deadline: deadline));

        var data = Create();

        Assert.That(data.Incidents.Count, Is.EqualTo(1));
        Assert.That(data.Incidents[0].Schedule.DeliveryMinute,
            Is.EqualTo(delivery));
        Assert.That(data.Incidents[0].Schedule.DeadlineMinute,
            Is.EqualTo(deadline));
        Assert.That(data.Incidents[0].Schedule.Occurrence.StartMinute,
            Is.LessThan(data.ScheduleData.Settings.WorkHours.StartMinute));
    }

    [Test]
    public void Snapshot_CopiesIncidentAndEvidenceMembership()
    {
        var receipt = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(
            "Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents/E_SUPPLIES_RECEIPT.asset");
        Assert.That(receipt, Is.Not.Null);
        evidence.Add(receipt);

        DayIncidentPlan plan = incidents[0];
        var data = Create();
        evidence.Clear();
        incidents.Clear();

        Assert.That(data.ScheduleData, Is.SameAs(scheduleData));
        Assert.That(data.EvidenceDatabase.All.Count, Is.EqualTo(1));
        Assert.That(data.EvidenceDatabase.GetById("E_SUPPLIES_RECEIPT"),
            Is.SameAs(receipt));
        Assert.That(data.Incidents.Count, Is.EqualTo(1));
        Assert.That(data.Incidents[0], Is.SameAs(plan));
    }

    [Test]
    public void IncidentPlans_AreExposedReadOnly()
    {
        var data = Create();
        Assert.Throws<NotSupportedException>(
            () => ((IList<DayIncidentPlan>)data.Incidents).Clear());
    }

    [Test]
    public void DocumentClaimForUnregisteredEmployee_IsRejected()
    {
        var originalDocument = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(
            "Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents/E_BOB_DENTIST.asset");
        Assert.That(originalDocument, Is.Not.Null);
        evidence.Add(originalDocument);

        Assert.Throws<ArgumentException>(() => Create());
    }

    [Test]
    public void CreateInstance_StartsFreshAfterAnotherCopyIsFinalized()
    {
        DayIncidentPlan plan = incidents[0];
        IncidentInstance first = plan.CreateInstance();
        IncidentInstance second = plan.CreateInstance();
        var database = new EvidenceDatabase(new List<EvidenceDefinition>());
        var validator = new ReportSubmissionValidator(
            new List<string> { "steve" }, new EvidenceService(database));
        var report = new IncidentReport(
            "D0I3", "steve", new List<string>(), 0, 845);

        Assert.That(first.TrySubmit(report, validator, 0, 845).IsValid, Is.True);
        var decision = new HrEvaluator().Evaluate(new EvaluationContext(
            first, scheduleData.HrKnowledge, database));
        Assert.That(first.TryApplyDecision(decision), Is.True);
        Assert.That(first.Status, Is.EqualTo(IncidentStatus.Rejected));

        IncidentInstance third = plan.CreateInstance();
        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(third, Is.Not.SameAs(first));
        Assert.That(third, Is.Not.SameAs(second));

        foreach (var fresh in new[] { second, third })
        {
            Assert.That(fresh.Status, Is.EqualTo(IncidentStatus.Open));
            Assert.That(fresh.SubmittedReport, Is.Null);
            Assert.That(fresh.Decision, Is.Null);
            Assert.That(fresh.Schedule, Is.SameAs(plan.Schedule));
            Assert.That(fresh.RelatedItemId, Is.EqualTo("supplies_carton_01"));
        }
    }

    [Test]
    public void Plan_FreezesContentBeforeTemplateChangesOrDestruction()
    {
        DayIncidentPlan plan = incidents[0];
        string title = definition.DisplayName;
        string description = definition.Description;

        Edit(definition, data =>
        {
            data.FindProperty("incidentId").stringValue = "changed";
            data.FindProperty("displayName").stringValue = "Changed";
            data.FindProperty("description").stringValue = "Changed text";
            data.FindProperty("relatedItemId").stringValue = "changed_carton";
            data.FindProperty("reviewDelayMinutes").intValue = 99;
        });
        Edit(location, data =>
            data.FindProperty("locationId").stringValue = "changed_location");

        UnityEngine.Object.DestroyImmediate(definition);
        UnityEngine.Object.DestroyImmediate(location);

        IncidentInstance copy = plan.CreateInstance();
        Assert.That(copy.IncidentId, Is.EqualTo("I3"));
        Assert.That(copy.InstanceId, Is.EqualTo("D0I3"));
        Assert.That(copy.DisplayName, Is.EqualTo(title));
        Assert.That(copy.Description, Is.EqualTo(description));
        Assert.That(copy.LocationId, Is.EqualTo("supplies_area"));
        Assert.That(copy.RelatedItemId, Is.EqualTo("supplies_carton_01"));
        Assert.That(copy.ReviewDelayMinutes, Is.EqualTo(15));
        Assert.That(copy.Status, Is.EqualTo(IncidentStatus.Open));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Plan_RejectsNullConstructorInput(bool missingDefinition)
    {
        Assert.Throws<ArgumentNullException>(() => new DayIncidentPlan(
            missingDefinition ? null : definition,
            missingDefinition ? incidents[0].Schedule : null));
    }

    [Test]
    public void Plan_RejectsTemplateScheduleIdMismatch()
    {
        var wrongSchedule = new IncidentScheduleEntry(
            "wrong", "I1", new TimeRange(0, 500, 510), 840, 900);
        Assert.Throws<ArgumentException>(
            () => new DayIncidentPlan(definition, wrongSchedule));
    }
}
