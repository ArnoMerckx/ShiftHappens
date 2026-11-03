using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EvaluationContextTests
{
    private const string Documents =
        "Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents";

    private IncidentInstance incident;
    private EvidenceDatabase database;
    private EvidenceService service;
    private ReportSubmissionValidator validator;
    private HrKnowledge hr;

    private EvidenceDefinition receipt;
    private EvidenceDefinition mirror;
    private CustodyClaimDefinition custodyDefinition;

    private static EvidenceDefinition LoadDocument(string id)
    {
        var document = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(
            $"{Documents}/{id}.asset");

        Assert.That(document, Is.Not.Null);
        return document;
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
        receipt = UnityEngine.Object.Instantiate(
            LoadDocument("E_SUPPLIES_RECEIPT"));

        var originalClaim =
            AssetDatabase.LoadAssetAtPath<CustodyClaimDefinition>(
                "Assets/Scripts/EvidenceSystem/EvidenceObjects/Claims/C_SUPPLIES_HANDOVER.asset");

        Assert.That(originalClaim, Is.Not.Null);
        custodyDefinition = UnityEngine.Object.Instantiate(originalClaim);

        Edit(receipt, data =>
            data.FindProperty("claimDefinitions")
                .GetArrayElementAtIndex(0).objectReferenceValue =
                    custodyDefinition);

        mirror = UnityEngine.Object.Instantiate(receipt);

        Edit(mirror, data =>
            data.FindProperty("evidenceId").stringValue = "E_RECEIPT_COPY");

        var dentist = LoadDocument("E_BOB_DENTIST");

        database = new EvidenceDatabase(new List<EvidenceDefinition>
        {
            receipt, mirror, dentist, LoadDocument("E_OFFICE_SOCIAL")
        });

        service = new EvidenceService(database);
        validator = new ReportSubmissionValidator(
            new List<string> { "bob", "alice", "dave", "steve", "karen" },
            service);

        hr = new HrKnowledge(
            new List<ApprovedScheduleRecord>
            {
                new ApprovedScheduleRecord(
                    "dentist", "bob", "offsite",
                    new TimeRange(0, 600, 690), 100, true)
            },
            new List<Claim>(dentist.GetClaims()));

        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I3MissingSupplies.asset");

        Assert.That(definition, Is.Not.Null);

        incident = new IncidentInstance(
            definition,
            new IncidentScheduleEntry(
                "D0_I3", "I3", new TimeRange(0, 815, 820), 840, 900));
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(mirror);
        UnityEngine.Object.DestroyImmediate(receipt);
        UnityEngine.Object.DestroyImmediate(custodyDefinition);
    }

    private EvaluationContext SubmitAndBuild(List<string> evidenceIds)
    {
        foreach (var id in evidenceIds)
            service.MarkRead(id, 0, 840);

        var report = new IncidentReport(
            "D0_I3", "steve", evidenceIds, 0, 845);

        Assert.That(
            incident.TrySubmit(report, validator, 0, 845).IsValid,
            Is.True);

        return new EvaluationContext(incident, hr, database);
    }

    [Test]
    public void SelectedClaims_StaySeparateFromHrKnowledge()
    {
        var context = SubmitAndBuild(
            new List<string> { "E_SUPPLIES_RECEIPT" });

        Assert.That(context.Incident, Is.SameAs(incident));
        Assert.That(context.Report, Is.SameAs(incident.SubmittedReport));
        Assert.That(context.HrKnowledge, Is.SameAs(hr));
        Assert.That(context.SubmittedClaims.Count, Is.EqualTo(1));
        Assert.That(
            context.SubmittedClaims[0].ClaimId,
            Is.EqualTo("C_SUPPLIES_HANDOVER"));

        Assert.That(hr.KnownClaims.Count, Is.EqualTo(1));
        Assert.That(
            hr.KnownClaims[0].ClaimId,
            Is.EqualTo("C_BOB_DENTIST"));

        Assert.That(service.IsDiscovered("E_BOB_DENTIST"), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyOrSocialOnlyReport_HasNoSubmittedClaims(bool useSocial)
    {
        var ids = new List<string>();

        if (useSocial)
            ids.Add("E_OFFICE_SOCIAL");

        var context = SubmitAndBuild(ids);

        Assert.That(context.SubmittedClaims, Is.Empty);
        Assert.That(context.HrKnowledge.KnownClaims.Count, Is.EqualTo(1));
    }

    [Test]
    public void SubmittedDocumentOrder_IsPreserved()
    {
        var context = SubmitAndBuild(new List<string>
        {
            "E_SUPPLIES_RECEIPT", "E_BOB_DENTIST"
        });

        Assert.That(context.SubmittedClaims.Count, Is.EqualTo(2));
        Assert.That(
            context.SubmittedClaims[0].ClaimId,
            Is.EqualTo("C_SUPPLIES_HANDOVER"));
        Assert.That(
            context.SubmittedClaims[1].ClaimId,
            Is.EqualTo("C_BOB_DENTIST"));
    }

    [Test]
    public void SubmittedClaims_AreProtectedSnapshots()
    {
        var context = SubmitAndBuild(
            new List<string> { "E_SUPPLIES_RECEIPT" });

        Assert.Throws<NotSupportedException>(() =>
            ((IList<Claim>)context.SubmittedClaims).Clear());

        Edit(custodyDefinition, data =>
            data.FindProperty("itemId").stringValue = "changed_carton");

        Assert.That(
            ((CustodyClaim)context.SubmittedClaims[0]).ItemId,
            Is.EqualTo("supplies_carton_01"));

        var laterContext = new EvaluationContext(incident, hr, database);

        Assert.That(
            ((CustodyClaim)laterContext.SubmittedClaims[0]).ItemId,
            Is.EqualTo("changed_carton"));
    }

    [Test]
    public void SharedClaimInTwoDocuments_RemainsTwoStatements()
    {
        var context = SubmitAndBuild(new List<string>
        {
            "E_SUPPLIES_RECEIPT", "E_RECEIPT_COPY"
        });

        Assert.That(context.SubmittedClaims.Count, Is.EqualTo(2));

        foreach (var claim in context.SubmittedClaims)
            Assert.That(claim.ClaimId, Is.EqualTo("C_SUPPLIES_HANDOVER"));
    }

    [Test]
    public void OpenIncident_CannotBeEvaluated()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new EvaluationContext(incident, hr, database));
    }

    [Test]
    public void MissingSelectedDocument_IsNotSilentlyIgnored()
    {
        SubmitAndBuild(new List<string> { "E_SUPPLIES_RECEIPT" });

        var emptyDatabase = new EvidenceDatabase(
            new List<EvidenceDefinition>());

        Assert.Throws<KeyNotFoundException>(() =>
            new EvaluationContext(incident, hr, emptyDatabase));
    }

    [Test]
    public void NullDependencies_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EvaluationContext(null, hr, database));

        Assert.Throws<ArgumentNullException>(() =>
            new EvaluationContext(incident, null, database));

        Assert.Throws<ArgumentNullException>(() =>
            new EvaluationContext(incident, hr, null));
    }
}