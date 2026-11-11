using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class IncidentTruthTests
{
    private const string MondayPath = "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset";
    private DayDefinition monday;
    private DayData data;
    private readonly List<UnityEngine.Object> temporaryObjects = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        monday = AssetDatabase.LoadAssetAtPath<DayDefinition>(MondayPath);
        Assert.That(monday, Is.Not.Null);
        data = monday.GetData();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = temporaryObjects.Count - 1; i >= 0; i--)
            if (temporaryObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(temporaryObjects[i]);
        temporaryObjects.Clear();
    }

    private T Copy<T>(T original) where T : UnityEngine.Object
    {
        T clone = UnityEngine.Object.Instantiate(original);
        temporaryObjects.Add(clone);
        return clone;
    }

    private static IncidentDefinition Definition(DayDefinition day, int index)
    {
        var serialized = new SerializedObject(day);
        var definition = serialized.FindProperty("incidents").GetArrayElementAtIndex(index)
            .FindPropertyRelative("incident").objectReferenceValue as IncidentDefinition;
        Assert.That(definition, Is.Not.Null);
        return definition;
    }

    private static string ActualCulprit(DayData day, string instanceId)
    {
        var method = typeof(DayData).GetMethod("GetActualCulpritId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var lookup = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), day, method);
        return lookup(instanceId);
    }

    private DayData WithTruth(int index, string culprit)
    {
        var plans = new List<DayIncidentPlan>(data.Incidents);
        plans[index] = new DayIncidentPlan(Definition(monday, index), plans[index].Schedule, culprit);
        return new DayData(data.ScheduleData,
            new List<EvidenceDefinition>(data.EvidenceDatabase.All), plans,
            new List<DayRequestPlan>(data.Requests));
    }

    [TestCase("D0I1", "bob")]
    [TestCase("D0I2", "dave")]
    [TestCase("D0I3", "karen")]
    public void ActualMonday_HasAcceptedPrivateTruth(string id, string expected)
    {
        Assert.That(ActualCulprit(data, id), Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("not_on_roster")]
    [TestCase("Bob")]
    public void DayValidation_RejectsMissingOrUnknownCulprit(string culprit)
    {
        Assert.Throws<ArgumentException>(() => WithTruth(0, culprit));
    }

    [TestCase("")]
    [TestCase(" ")]
    public void Plan_RejectsExplicitBlankCulprit(string culprit)
    {
        Assert.Throws<ArgumentException>(() =>
            new DayIncidentPlan(Definition(monday, 0), data.Incidents[0].Schedule, culprit));
    }

    [Test]
    public void Lookup_RejectsInvalidIdentityInsteadOfReturningFallbackTruth()
    {
        Assert.Throws<ArgumentException>(() => ActualCulprit(data, null));
        Assert.Throws<ArgumentException>(() => ActualCulprit(data, " "));
        Assert.Throws<KeyNotFoundException>(() => ActualCulprit(data, "d0i1"));
        Assert.Throws<KeyNotFoundException>(() => ActualCulprit(data, "missing"));
    }

    [Test]
    public void AuthoredMissingReferenceAndEmployeeOutsideRoster_AreRejected()
    {
        var day = Copy(monday);
        var serialized = new SerializedObject(day);
        var culprit = serialized.FindProperty("incidents").GetArrayElementAtIndex(0)
            .FindPropertyRelative("actualCulprit");
        culprit.objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.Throws<InvalidOperationException>(() => day.GetData());

        var bob = AssetDatabase.LoadAssetAtPath<EmployeeDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Employees/Bob.asset");
        Assert.That(bob, Is.Not.Null);
        var outside = Copy(bob);
        var employee = new SerializedObject(outside);
        employee.FindProperty("employeeId").stringValue = "outside_roster";
        employee.ApplyModifiedPropertiesWithoutUndo();
        serialized = new SerializedObject(day);
        serialized.FindProperty("incidents").GetArrayElementAtIndex(0)
            .FindPropertyRelative("actualCulprit").objectReferenceValue = outside;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.Throws<ArgumentException>(() => day.GetData());
        Assert.That(ActualCulprit(monday.GetData(), "D0I1"), Is.EqualTo("bob"));
    }

    [Test]
    public void SameTemplateOnDifferentDayCanHaveDifferentTruthWithoutChangingFirstPlan()
    {
        DayIncidentPlan first = data.Incidents[0];
        var old = first.Schedule;
        var settings = new WorkdaySettings("test_tuesday", "Test Tuesday", 1, 540, 1020, 40);
        var schedule = new DayScheduleData(settings,
            new List<string>(data.ScheduleData.EmployeeIds),
            new List<ApprovedScheduleRecord>(), new List<ApprovedScheduleRecord>(), new List<Claim>());
        var next = new DayIncidentPlan(Definition(monday, 0),
            new IncidentScheduleEntry("D1I1", old.IncidentId,
                new TimeRange(1, old.Occurrence.StartMinute, old.Occurrence.EndMinute),
                old.DeliveryMinute, old.DeadlineMinute), "alice");
        var nextDay = new DayData(schedule, new List<EvidenceDefinition>(),
            new List<DayIncidentPlan> { next });
        Assert.That(ActualCulprit(nextDay, "D1I1"), Is.EqualTo("alice"));
        Assert.That(ActualCulprit(data, "D0I1"), Is.EqualTo("bob"));
        Assert.That(next.CreateInstance().DayIndex, Is.EqualTo(1));
    }

    private static HrEvaluationResult Evaluate(DayData day, int index, string suspect, string evidenceId)
    {
        IncidentInstance incident = day.Incidents[index].CreateInstance();
        var evidence = new EvidenceService(day.EvidenceDatabase);
        Assert.That(evidence.MarkDiscovered(evidenceId, incident.DayIndex, incident.DeliveryMinute), Is.True);
        var report = new IncidentReport(incident.InstanceId, suspect,
            new List<string> { evidenceId }, incident.DayIndex, incident.DeliveryMinute);
        var validator = new ReportSubmissionValidator(new List<string>(day.ScheduleData.EmployeeIds), evidence);
        Assert.That(incident.TrySubmit(report, validator, incident.DayIndex, incident.DeliveryMinute).IsValid, Is.True);
        return new HrEvaluator().Evaluate(new EvaluationContext(
            incident, day.ScheduleData.HrKnowledge, day.EvidenceDatabase));
    }

    [TestCase(0, "bob", "E_SCHEDULE", HrVerdict.Approved)]
    [TestCase(0, "alice", "E_SCHEDULE", HrVerdict.Rejected)]
    [TestCase(1, "dave", "E_DAVE_PRINTER", HrVerdict.Approved)]
    [TestCase(1, "bob", "E_SCHEDULE", HrVerdict.Rejected)]
    [TestCase(1, "dave", "E_OFFICE_SOCIAL", HrVerdict.Rejected)]
    [TestCase(2, "karen", "E_KAREN_COLLECTION", HrVerdict.Approved)]
    [TestCase(2, "steve", "E_SUPPLIES_RECEIPT", HrVerdict.Approved)]
    [TestCase(2, "steve", "E_SCHEDULE", HrVerdict.Rejected)]
    public void ChangingOnlyTruth_LeavesAllEightHrVerdictsAndEveryReasonUnchanged(
        int index, string suspect, string evidenceId, HrVerdict expected)
    {
        string other = suspect == "bob" ? "alice" : "bob";
        DayData trueBlame = WithTruth(index, suspect);
        DayData falseBlame = WithTruth(index, other);
        string id = data.Incidents[index].Schedule.InstanceId;
        Assert.That(ActualCulprit(trueBlame, id), Is.EqualTo(suspect));
        Assert.That(ActualCulprit(falseBlame, id), Is.EqualTo(other));
        Assert.That(trueBlame.ScheduleData, Is.SameAs(falseBlame.ScheduleData));
        Assert.That(trueBlame.Incidents[index].Schedule, Is.SameAs(falseBlame.Incidents[index].Schedule));

        HrEvaluationResult first = Evaluate(trueBlame, index, suspect, evidenceId);
        HrEvaluationResult second = Evaluate(falseBlame, index, suspect, evidenceId);
        Assert.That(first.Verdict, Is.EqualTo(expected));
        Assert.That(second.Verdict, Is.EqualTo(first.Verdict));
        Assert.That(second.Summary, Is.EqualTo(first.Summary));
        Assert.That(second.RuleResults.Count, Is.EqualTo(first.RuleResults.Count));
        for (int i = 0; i < first.RuleResults.Count; i++)
        {
            Assert.That(second.RuleResults[i].RuleId, Is.EqualTo(first.RuleResults[i].RuleId));
            Assert.That(second.RuleResults[i].Outcome, Is.EqualTo(first.RuleResults[i].Outcome));
            Assert.That(second.RuleResults[i].Reason, Is.EqualTo(first.RuleResults[i].Reason));
        }
        Assert.That(ActualCulprit(data, "D0I3"), Is.EqualTo("karen"));
    }

    [Test]
    public void HrInputsAndPublicProjections_ExposeNoCulpritOrTruthContainer()
    {
        Assert.That(typeof(DayIncidentPlan).GetProperty("ActualCulpritId"), Is.Null);
        Assert.That(typeof(DayData).GetMethod("GetActualCulpritId"), Is.Null);
        var types = new[] { typeof(IncidentInstance), typeof(EvaluationContext), typeof(HrKnowledge),
            typeof(IncidentNoticeViewData), typeof(ReportOverviewViewData), typeof(EmployeeViewData),
            typeof(RequestOverviewViewData), typeof(IncidentReport), typeof(HrEvaluationResult) };
        foreach (Type type in types)
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.That(property.Name.IndexOf("culprit", StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0), type.Name + "." + property.Name);
                Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(DayData)), type.Name);
                Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(DayIncidentPlan)), type.Name);
                Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(DayDefinition)), type.Name);
            }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                Assert.That(field.Name.IndexOf("culprit", StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0), type.Name + "." + field.Name);
        }
    }
}
