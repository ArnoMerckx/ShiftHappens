using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ActivitySupportRuleTests
{
    private WhereaboutsClaimDefinition assignment;
    private EvidenceDefinition document;
    private List<ApprovedScheduleRecord> approvedRecords;
    private List<Claim> knownClaims;

    private static EvidenceDefinition LoadDocument(string id)
    {
        var result = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(
            $"Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents/{id}.asset");

        Assert.That(result, Is.Not.Null);
        return result;
    }

    private static void Edit(
        UnityEngine.Object target,
        Action<SerializedObject> change)
    {
        var data = new SerializedObject(target);
        change(data);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [SetUp]
    public void SetUp()
    {
        var original =
            AssetDatabase.LoadAssetAtPath<WhereaboutsClaimDefinition>(
                "Assets/Scripts/EvidenceSystem/EvidenceObjects/Claims/C_DAVE_PRINTER.asset");

        Assert.That(original, Is.Not.Null);

        assignment = UnityEngine.Object.Instantiate(original);
        document = UnityEngine.Object.Instantiate(
            LoadDocument("E_DAVE_PRINTER"));

        Edit(document, data =>
            data.FindProperty("claimDefinitions")
                .GetArrayElementAtIndex(0).objectReferenceValue = assignment);

        approvedRecords = new List<ApprovedScheduleRecord>();
        knownClaims = new List<Claim>();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(document);
        UnityEngine.Object.DestroyImmediate(assignment);
    }

    private EvaluationContext BuildContext(List<string> selected = null)
    {
        var database = new EvidenceDatabase(new List<EvidenceDefinition>
        {
            document,
            LoadDocument("E_OFFICE_SOCIAL"),
            LoadDocument("E_SUPPLIES_RECEIPT")
        });

        var service = new EvidenceService(database);

        var ids = selected ?? new List<string> { "E_DAVE_PRINTER" };

        foreach (var id in ids)
            service.MarkRead(id, 0, 675);

        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");

        Assert.That(definition, Is.Not.Null);

        var incident = new IncidentInstance(
            definition,
            new IncidentScheduleEntry(
                "D0_I2", "I2", new TimeRange(0, 635, 640), 675, 720));

        var report = new IncidentReport("D0_I2", "dave", ids, 0, 680);

        var validator = new ReportSubmissionValidator(
            new List<string> { "bob", "alice", "dave", "steve", "karen" },
            service);

        Assert.That(
            incident.TrySubmit(report, validator, 0, 680).IsValid,
            Is.True);

        return new EvaluationContext(
            incident,
            new HrKnowledge(approvedRecords, knownClaims),
            database);
    }

    private void AssertOutcome(
        HrRuleOutcome expected, List<string> selected = null)
    {
        var context = BuildContext(selected);
        var result = new ActivitySupportRule().Evaluate(context);

        Assert.That(result.RuleId, Is.EqualTo("activity_support"));
        Assert.That(result.Outcome, Is.EqualTo(expected));
        Assert.That(string.IsNullOrWhiteSpace(result.Reason), Is.False);
        Assert.That(
            context.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(
            context.Incident.SubmittedReport, Is.SameAs(context.Report));
    }

    [TestCase("Dave", "PrinterArea", 0, 630, 645, HrRuleOutcome.Support)]
    [TestCase("Alice", "PrinterArea", 0, 630, 645, HrRuleOutcome.Neutral)]
    [TestCase("Dave", "ITOffice", 0, 630, 645, HrRuleOutcome.Neutral)]
    [TestCase("Dave", "PrinterArea", 1, 630, 645, HrRuleOutcome.Neutral)]
    [TestCase("Dave", "PrinterArea", 0, 630, 635, HrRuleOutcome.Neutral)]
    [TestCase("Dave", "PrinterArea", 0, 640, 645, HrRuleOutcome.Neutral)]
    [TestCase("Dave", "PrinterArea", 0, 636, 638, HrRuleOutcome.Support)]
    public void Assignment_MustMatchRelevantEmployeeLocationAndTime(
        string employeeName, string locationName,
        int day, int start, int end, HrRuleOutcome expected)
    {
        var employee = AssetDatabase.LoadAssetAtPath<EmployeeDefinition>(
            $"Assets/Scripts/CompanySystem/CompanyObjects/Employees/{employeeName}.asset");

        var location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(
            $"Assets/Scripts/CompanySystem/CompanyObjects/Locations/{locationName}.asset");

        Assert.That(employee, Is.Not.Null);
        Assert.That(location, Is.Not.Null);

        Edit(assignment, data =>
        {
            data.FindProperty("employee").objectReferenceValue = employee;
            data.FindProperty("location").objectReferenceValue = location;
            data.FindProperty("dayIndex").intValue = day;
            data.FindProperty("startMinute").intValue = start;
            data.FindProperty("endMinute").intValue = end;
        });

        AssertOutcome(expected);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClaimedPresenceRequirement_IsNotRequiredForSupport(
        bool requiresPresence)
    {
        Edit(assignment, data =>
            data.FindProperty("requiresPresence").boolValue =
                requiresPresence);

        AssertOutcome(HrRuleOutcome.Support);
    }

    [TestCase(635, 637, 635, 637, HrRuleOutcome.Neutral)]
    [TestCase(635, 640, 635, 637, HrRuleOutcome.Support)]
    [TestCase(637, 640, 635, 637, HrRuleOutcome.Support)]
    [TestCase(634, 638, 635, 637, HrRuleOutcome.Support)]
    [TestCase(630, 645, 635, 640, HrRuleOutcome.Neutral)]
    public void AssignmentAndOpportunity_MustShareUnexcludedTime(
        int claimStart, int claimEnd,
        int absenceStart, int absenceEnd, HrRuleOutcome expected)
    {
        Edit(assignment, data =>
        {
            data.FindProperty("startMinute").intValue = claimStart;
            data.FindProperty("endMinute").intValue = claimEnd;
        });

        approvedRecords.Add(new ApprovedScheduleRecord(
            "absence", "dave", "offsite",
            new TimeRange(0, absenceStart, absenceEnd), 100, true));

        AssertOutcome(expected);
    }

    [TestCase(true, HrRuleOutcome.Support)]
    [TestCase(false, HrRuleOutcome.Neutral)]
    public void ApprovedPriority_IsRespected(
        bool assignmentWins, HrRuleOutcome expected)
    {
        approvedRecords.Add(new ApprovedScheduleRecord(
            "absence", "dave", "offsite",
            new TimeRange(0, 630, 645),
            assignmentWins ? 10 : 100, true));

        approvedRecords.Add(new ApprovedScheduleRecord(
            "inspection", "dave", "printer_area",
            new TimeRange(0, 630, 645),
            assignmentWins ? 100 : 10, false));

        AssertOutcome(expected);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HrKnowledge_DoesNotReplaceSubmittedSupport(bool useSocial)
    {
        knownClaims.Add(assignment.ToClaim());

        approvedRecords.Add(new ApprovedScheduleRecord(
            "inspection", "dave", "printer_area",
            new TimeRange(0, 630, 645), 100, false));

        var selected = new List<string>();

        if (useSocial)
            selected.Add("E_OFFICE_SOCIAL");

        AssertOutcome(HrRuleOutcome.Neutral, selected);
    }

    [Test]
    public void CustodyClaim_IsNotActivitySupport()
    {
        AssertOutcome(
            HrRuleOutcome.Neutral,
            new List<string> { "E_SUPPLIES_RECEIPT" });
    }

    [Test]
    public void NullContext_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ActivitySupportRule().Evaluate(null));
    }
}