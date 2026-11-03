using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CustodySupportRuleTests
{
    private CustodyClaimDefinition custody;
    private EvidenceDefinition receipt;
    private EvidenceDefinition mirror;
    private IncidentDefinition definition;
    private List<ApprovedScheduleRecord> approvedRecords;
    private List<Claim> knownClaims;
    private int incidentDay;

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
        var originalClaim =
            AssetDatabase.LoadAssetAtPath<CustodyClaimDefinition>(
                "Assets/Scripts/EvidenceSystem/EvidenceObjects/Claims/C_SUPPLIES_HANDOVER.asset");

        var originalIncident =
            AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
                "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I3MissingSupplies.asset");

        Assert.That(originalClaim, Is.Not.Null);
        Assert.That(originalIncident, Is.Not.Null);

        custody = UnityEngine.Object.Instantiate(originalClaim);
        definition = UnityEngine.Object.Instantiate(originalIncident);
        receipt = UnityEngine.Object.Instantiate(
            LoadDocument("E_SUPPLIES_RECEIPT"));

        Edit(receipt, data =>
            data.FindProperty("claimDefinitions")
                .GetArrayElementAtIndex(0).objectReferenceValue = custody);

        mirror = UnityEngine.Object.Instantiate(receipt);

        Edit(mirror, data =>
            data.FindProperty("evidenceId").stringValue = "E_RECEIPT_COPY");

        approvedRecords = new List<ApprovedScheduleRecord>();
        knownClaims = new List<Claim>();
        incidentDay = 0;
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(mirror);
        UnityEngine.Object.DestroyImmediate(receipt);
        UnityEngine.Object.DestroyImmediate(custody);
        UnityEngine.Object.DestroyImmediate(definition);
    }

    private EvaluationContext BuildContext(List<string> selected = null)
    {
        var database = new EvidenceDatabase(new List<EvidenceDefinition>
        {
            receipt,
            mirror,
            LoadDocument("E_OFFICE_SOCIAL"),
            LoadDocument("E_KAREN_COLLECTION")
        });

        var service = new EvidenceService(database);
        var ids = selected ?? new List<string> { "E_SUPPLIES_RECEIPT" };

        foreach (var id in ids)
            service.MarkRead(id, incidentDay, 840);

        string instanceId = $"D{incidentDay}_I3";

        var incident = new IncidentInstance(
            definition,
            new IncidentScheduleEntry(
                instanceId, "I3",
                new TimeRange(incidentDay, 815, 820), 840, 900));

        var report = new IncidentReport(
            instanceId, "steve", ids, incidentDay, 845);

        var validator = new ReportSubmissionValidator(
            new List<string> { "bob", "alice", "dave", "steve", "karen" },
            service);

        Assert.That(
            incident.TrySubmit(report, validator, incidentDay, 845).IsValid,
            Is.True);

        return new EvaluationContext(
            incident,
            new HrKnowledge(approvedRecords, knownClaims),
            database);
    }

    private static void AssertResult(
        EvaluationContext context, HrRuleOutcome expected)
    {
        var result = new CustodySupportRule().Evaluate(context);

        Assert.That(result.RuleId, Is.EqualTo("custody_support"));
        Assert.That(result.Outcome, Is.EqualTo(expected));
        Assert.That(string.IsNullOrWhiteSpace(result.Reason), Is.False);
        Assert.That(
            context.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(
            context.Incident.SubmittedReport, Is.SameAs(context.Report));
    }

    private void AssertOutcome(
        HrRuleOutcome expected, List<string> selected = null)
    {
        AssertResult(BuildContext(selected), expected);
    }

    [TestCase(0, 530, HrRuleOutcome.Support)]
    [TestCase(0, 815, HrRuleOutcome.Support)]
    [TestCase(0, 816, HrRuleOutcome.Neutral)]
    [TestCase(0, 820, HrRuleOutcome.Neutral)]
    [TestCase(1, 0, HrRuleOutcome.Neutral)]
    public void Handover_MustPrecedeOrEqualWindowStart(
        int day, int minute, HrRuleOutcome expected)
    {
        Edit(custody, data =>
        {
            data.FindProperty("dayIndex").intValue = day;
            data.FindProperty("minuteOfDay").intValue = minute;
        });

        AssertOutcome(expected);
    }

    [Test]
    public void EarlierDayHandover_CanProvideResponsibility()
    {
        incidentDay = 1;
        AssertOutcome(HrRuleOutcome.Support);
    }

    [Test]
    public void DifferentCustodian_DoesNotSupportSuspect()
    {
        var alice = AssetDatabase.LoadAssetAtPath<EmployeeDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Employees/Alice.asset");

        Assert.That(alice, Is.Not.Null);

        Edit(custody, data =>
            data.FindProperty("employee").objectReferenceValue = alice);

        AssertOutcome(HrRuleOutcome.Neutral);
    }

    [TestCase("another_carton")]
    [TestCase("SUPPLIES_CARTON_01")]
    public void ItemIdentity_MustMatchExactly(string itemId)
    {
        Edit(custody, data =>
            data.FindProperty("itemId").stringValue = itemId);

        AssertOutcome(HrRuleOutcome.Neutral);
    }

    [Test]
    public void HandoverElsewhere_DoesNotInventLaterWhereabouts()
    {
        var dispatch = AssetDatabase.LoadAssetAtPath<LocationDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Locations/DispatchArea.asset");

        Assert.That(dispatch, Is.Not.Null);

        Edit(custody, data =>
            data.FindProperty("location").objectReferenceValue = dispatch);

        AssertOutcome(HrRuleOutcome.Support);
    }

    [TestCase(null)]
    [TestCase("")]
    public void IncidentWithoutSpecificItem_HasNoCustodySupport(
        string itemId)
    {
        Edit(definition, data =>
            data.FindProperty("relatedItemId").stringValue = itemId);

        AssertOutcome(HrRuleOutcome.Neutral);
    }

    [TestCase(810, 825, HrRuleOutcome.Neutral)]
    [TestCase(815, 817, HrRuleOutcome.Support)]
    [TestCase(820, 825, HrRuleOutcome.Support)]
    public void VerifiedExclusion_RespectsWindowCoverage(
        int start, int end, HrRuleOutcome expected)
    {
        approvedRecords.Add(new ApprovedScheduleRecord(
            "absence", "steve", "offsite",
            new TimeRange(0, start, end), 100, true));

        AssertOutcome(expected);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HrKnownReceipt_DoesNotReplaceSubmittedSupport(
        bool useSocial)
    {
        knownClaims.Add(custody.ToClaim());

        var selected = new List<string>();

        if (useSocial)
            selected.Add("E_OFFICE_SOCIAL");

        AssertOutcome(HrRuleOutcome.Neutral, selected);
    }

    [Test]
    public void ActivityClaim_IsNotCustodySupport()
    {
        AssertOutcome(
            HrRuleOutcome.Neutral,
            new List<string> { "E_KAREN_COLLECTION" });
    }

    [Test]
    public void RepeatedSharedCustody_RemainsOneSupportOutcome()
    {
        var context = BuildContext(new List<string>
        {
            "E_SUPPLIES_RECEIPT", "E_RECEIPT_COPY"
        });

        Assert.That(context.SubmittedClaims.Count, Is.EqualTo(2));
        AssertResult(context, HrRuleOutcome.Support);
    }

    [Test]
    public void NullContext_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CustodySupportRule().Evaluate(null));
    }
}