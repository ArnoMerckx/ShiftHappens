using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System;

public class ReportSubmissionTests
{
    private IncidentInstance incident;
    private EvidenceService service;
    private ReportSubmissionValidator validator;
    private EvidenceDefinition receipt;

    [SetUp]
    public void SetUp()
    {
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I3MissingSupplies.asset");

        var originalReceipt =
            AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(
                "Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents/E_SUPPLIES_RECEIPT.asset");

        var social = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(
            "Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents/E_OFFICE_SOCIAL.asset");

        Assert.That(definition, Is.Not.Null);
        Assert.That(originalReceipt, Is.Not.Null);
        Assert.That(social, Is.Not.Null);

        receipt = UnityEngine.Object.Instantiate(originalReceipt);

        service = new EvidenceService(new EvidenceDatabase(
            new List<EvidenceDefinition> { receipt, social }));

        validator = new ReportSubmissionValidator(
            new List<string> { "bob", "alice", "dave", "steve", "karen" },
            service);

        incident = new IncidentInstance(
            definition,
            new IncidentScheduleEntry(
                "D0_I3", "I3", new TimeRange(0, 815, 820), 840, 900));
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(receipt);
    }

    private static IncidentReport NewReport(
        string suspectId = "steve",
        string evidenceId = "E_SUPPLIES_RECEIPT",
        string instanceId = "D0_I3")
    {
        return new IncidentReport(
            instanceId, suspectId,
            new List<string> { evidenceId }, 0, 845);
    }

    private void AssertRejected(IncidentReport report)
    {
        var result = incident.TrySubmit(report, validator, 0, 845);

        Assert.That(result.IsValid, Is.False);
        Assert.That(string.IsNullOrWhiteSpace(result.Reason), Is.False);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
    }

    [TestCase("E_SUPPLIES_RECEIPT")]
    [TestCase("E_OFFICE_SOCIAL")]
    public void DiscoveredEvidence_CanBeSubmitted(string evidenceId)
    {
        service.MarkRead(evidenceId, 0, 840);
        var report = NewReport(evidenceId: evidenceId);

        var result = incident.TrySubmit(report, validator, 0, 845);

        Assert.That(result.IsValid, Is.True);
        Assert.That(string.IsNullOrWhiteSpace(result.Reason), Is.False);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(incident.SubmittedReport, Is.SameAs(report));
    }

    [Test]
    public void ValidationAlone_DoesNotAcceptReport()
    {
        service.MarkDiscovered("E_SUPPLIES_RECEIPT", 0, 840);

        var result = validator.Validate(
            incident, NewReport(), 0, 845);

        Assert.That(result.IsValid, Is.True);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
        Assert.That(service.IsRead("E_SUPPLIES_RECEIPT"), Is.False);
    }

    [Test]
    public void TemplateId_CannotReplaceInstanceId()
    {
        service.MarkRead("E_SUPPLIES_RECEIPT", 0, 840);
        AssertRejected(NewReport(instanceId: "I3"));
    }

    [TestCase("unknown")]
    [TestCase("Steve")]
    public void UnknownSuspect_IsRejected(string suspectId)
    {
        service.MarkRead("E_SUPPLIES_RECEIPT", 0, 840);
        AssertRejected(NewReport(suspectId: suspectId));
    }

    [Test]
    public void UnknownEvidence_IsRejected()
    {
        AssertRejected(NewReport(evidenceId: "E_MISSING"));
    }

    [Test]
    public void AvailableButUndiscoveredEvidence_IsRejected()
    {
        Assert.That(
            service.IsAvailable("E_SUPPLIES_RECEIPT", 0, 845), Is.True);

        AssertRejected(NewReport());

        Assert.That(
            service.IsDiscovered("E_SUPPLIES_RECEIPT"), Is.False);
    }

    [Test]
    public void UnavailableEvidence_IsRejected()
    {
        var data = new SerializedObject(receipt);
        data.FindProperty("availableMinute").intValue = 850;
        data.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(
            service.IsAvailable("E_SUPPLIES_RECEIPT", 0, 845), Is.False);

        AssertRejected(NewReport());
    }

    [Test]
    public void OneInvalidAttachment_PreventsWholeSubmission()
    {
        service.MarkRead("E_SUPPLIES_RECEIPT", 0, 840);

        var report = new IncidentReport(
            "D0_I3", "steve",
            new List<string> { "E_SUPPLIES_RECEIPT", "E_MISSING" },
            0, 845);

        AssertRejected(report);
    }

    [Test]
    public void EmptyEvidence_CanBeSubmittedForEvaluation()
    {
        var report = new IncidentReport(
            "D0_I3", "steve", new List<string>(), 0, 845);

        var result = incident.TrySubmit(report, validator, 0, 845);

        Assert.That(result.IsValid, Is.True);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(incident.SubmittedReport, Is.SameAs(report));
    }

    [TestCase(0, 839, false)]
    [TestCase(0, 840, true)]
    [TestCase(0, 900, true)]
    [TestCase(0, 901, false)]
    [TestCase(1, 0, false)]
    public void SubmissionWindow_UsesInclusiveDeadline(
    int day, int minute, bool accepted)
    {
        service.MarkRead("E_SUPPLIES_RECEIPT", 0, 540);

        var report = new IncidentReport(
            "D0_I3", "steve",
            new List<string> { "E_SUPPLIES_RECEIPT" },
            day, minute);

        var result = incident.TrySubmit(report, validator, day, minute);

        Assert.That(result.IsValid, Is.EqualTo(accepted));

        if (accepted)
        {
            Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
            Assert.That(incident.SubmittedReport, Is.SameAs(report));
        }
        else
        {
            Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
            Assert.That(incident.SubmittedReport, Is.Null);
        }
    }

    [Test]
    public void SubmissionOnPreviousDay_IsRejected()
    {
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I3MissingSupplies.asset");

        incident = new IncidentInstance(
            definition,
            new IncidentScheduleEntry(
                "D1_I3", "I3", new TimeRange(1, 815, 820), 840, 900));

        service.MarkRead("E_SUPPLIES_RECEIPT", 0, 540);

        var report = new IncidentReport(
            "D1_I3", "steve",
            new List<string> { "E_SUPPLIES_RECEIPT" }, 0, 1439);

        var result = incident.TrySubmit(report, validator, 0, 1439);

        Assert.That(result.IsValid, Is.False);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
    }

    [TestCase(0, 844, 0, 845)]
    [TestCase(0, 846, 0, 845)]
    [TestCase(1, 845, 0, 845)]
    [TestCase(0, 900, 0, 901)]
    public void ReportTimestamp_MustMatchCurrentTime(
        int reportDay, int reportMinute, int currentDay, int currentMinute)
    {
        service.MarkRead("E_SUPPLIES_RECEIPT", 0, 540);

        var report = new IncidentReport(
            "D0_I3", "steve",
            new List<string> { "E_SUPPLIES_RECEIPT" },
            reportDay, reportMinute);

        var result = incident.TrySubmit(
            report, validator, currentDay, currentMinute);

        Assert.That(result.IsValid, Is.False);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
    }

    [Test]
    public void SecondSubmission_CannotReplaceAcceptedReport()
    {
        service.MarkRead("E_SUPPLIES_RECEIPT", 0, 840);
        service.MarkRead("E_OFFICE_SOCIAL", 0, 840);

        var first = NewReport();

        Assert.That(
            incident.TrySubmit(first, validator, 0, 845).IsValid,
            Is.True);

        var second = new IncidentReport(
            "D0_I3", "karen",
            new List<string> { "E_OFFICE_SOCIAL" }, 0, 846);

        var result = incident.TrySubmit(second, validator, 0, 846);

        Assert.That(result.IsValid, Is.False);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(incident.SubmittedReport, Is.SameAs(first));
    }

    [Test]
    public void FailedSubmission_DoesNotPreventValidRetry()
    {
        var report = NewReport();

        AssertRejected(report);

        service.MarkRead("E_SUPPLIES_RECEIPT", 0, 845);

        var result = incident.TrySubmit(report, validator, 0, 845);

        Assert.That(result.IsValid, Is.True);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(incident.SubmittedReport, Is.SameAs(report));
    }

    [TestCase(-1, 845)]
    [TestCase(0, -1)]
    [TestCase(0, 1440)]
    public void InvalidCurrentTime_ThrowsWithoutChangingState(
        int day, int minute)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            incident.TrySubmit(NewReport(), validator, day, minute));

        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
    }

    [Test]
    public void NullSubmissionArguments_ThrowWithoutChangingState()
    {
        Assert.Throws<ArgumentNullException>(() =>
            incident.TrySubmit(null, validator, 0, 845));

        Assert.Throws<ArgumentNullException>(() =>
            incident.TrySubmit(NewReport(), null, 0, 845));

        Assert.Throws<ArgumentNullException>(() =>
            validator.Validate(null, NewReport(), 0, 845));

        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
    }

    [Test]
    public void NullValidatorDependencies_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReportSubmissionValidator(null, service));

        Assert.Throws<ArgumentNullException>(() =>
            new ReportSubmissionValidator(
                new List<string> { "steve" }, null));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankEmployeeRegistryId_IsRejected(string id)
    {
        Assert.Throws<ArgumentException>(() =>
            new ReportSubmissionValidator(
                new List<string> { id }, service));
    }

    [Test]
    public void DuplicateEmployeeRegistryId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReportSubmissionValidator(
                new List<string> { "steve", "steve" }, service));
    }

    [Test]
    public void EmployeeRegistryMembership_IsCopied()
    {
        var employeeIds = new List<string> { "steve" };
        var localValidator = new ReportSubmissionValidator(
            employeeIds, service);

        employeeIds.Clear();
        service.MarkRead("E_SUPPLIES_RECEIPT", 0, 840);

        var result = incident.TrySubmit(
            NewReport(), localValidator, 0, 845);

        Assert.That(result.IsValid, Is.True);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
    }
}