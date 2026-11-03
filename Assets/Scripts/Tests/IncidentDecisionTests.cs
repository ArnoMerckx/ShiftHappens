using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class IncidentDecisionTests
{
    private EvidenceDatabase database;
    private EvidenceService service;
    private ReportSubmissionValidator validator;
    private IncidentInstance incident;
    private HrKnowledge hr;

    [SetUp]
    public void SetUp()
    {
        const string folder =
            "Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents";

        var documents = new List<EvidenceDefinition>();

        foreach (var guid in AssetDatabase.FindAssets(
            "t:EvidenceDefinition", new[] { folder }))
        {
            documents.Add(
                AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)));
        }

        database = new EvidenceDatabase(documents);
        service = new EvidenceService(database);

        validator = new ReportSubmissionValidator(
            new List<string> { "bob", "alice", "dave", "steve", "karen" },
            service);

        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I3MissingSupplies.asset");

        Assert.That(definition, Is.Not.Null);

        incident = new IncidentInstance(
            definition,
            new IncidentScheduleEntry(
                "D0_I3", "I3", new TimeRange(0, 815, 820), 840, 900));

        hr = new HrKnowledge(
            new List<ApprovedScheduleRecord>(), new List<Claim>());
    }

    private HrEvaluationResult SubmitAndEvaluate(List<string> selected)
    {
        foreach (var id in selected)
            service.MarkRead(id, 0, 840);

        var report = new IncidentReport(
            "D0_I3", "steve", selected, 0, 845);

        Assert.That(
            incident.TrySubmit(report, validator, 0, 845).IsValid,
            Is.True);

        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(incident.Decision, Is.Null);

        return new HrEvaluator().Evaluate(
            new EvaluationContext(incident, hr, database));
    }

    [TestCase("E_SUPPLIES_RECEIPT", IncidentStatus.Approved)]
    [TestCase("E_SCHEDULE", IncidentStatus.Rejected)]
    [TestCase("E_OFFICE_SOCIAL", IncidentStatus.Rejected)]
    public void DecisionApplication_RetainsOriginalHistory(
        string evidenceId, IncidentStatus expectedStatus)
    {
        var selected = new List<string> { evidenceId };
        var decision = SubmitAndEvaluate(selected);
        var report = incident.SubmittedReport;

        selected.Clear();

        Assert.That(incident.TryApplyDecision(decision), Is.True);

        Assert.That(incident.Status, Is.EqualTo(expectedStatus));
        Assert.That(incident.Decision, Is.SameAs(decision));
        Assert.That(incident.SubmittedReport, Is.SameAs(report));
        Assert.That(incident.Decision.Report, Is.SameAs(report));
        Assert.That(report.SuspectId, Is.EqualTo("steve"));
        Assert.That(report.SubmittedDayIndex, Is.EqualTo(0));
        Assert.That(report.SubmittedMinute, Is.EqualTo(845));
        Assert.That(report.EvidenceIds, Is.EqualTo(new[] { evidenceId }));
        Assert.That(incident.Decision.RuleResults.Count, Is.EqualTo(3));
        Assert.That(
            string.IsNullOrWhiteSpace(incident.Decision.Summary), Is.False);
    }

    [TestCase("E_SUPPLIES_RECEIPT")]
    [TestCase("E_SCHEDULE")]
    public void FinalizedIncident_PreservesItsFirstReportAndDecision(
        string evidenceId)
    {
        var decision = SubmitAndEvaluate(
            new List<string> { evidenceId });

        var report = incident.SubmittedReport;

        Assert.That(incident.TryApplyDecision(decision), Is.True);

        var finalStatus = incident.Status;
        var oppositeRules = new List<HrRuleResult>();

        if (decision.Verdict == HrVerdict.Rejected)
        {
            oppositeRules.Add(new HrRuleResult(
                "test_support", HrRuleOutcome.Support, "Test replacement."));
        }

        var replacement = new HrEvaluationResult(report, oppositeRules);

        Assert.That(
            replacement.Verdict, Is.Not.EqualTo(decision.Verdict));
        Assert.That(incident.TryApplyDecision(decision), Is.False);
        Assert.That(incident.TryApplyDecision(replacement), Is.False);

        service.MarkRead("E_SCHEDULE", 0, 840);

        var anotherReport = new IncidentReport(
            "D0_I3", "karen",
            new List<string> { "E_SCHEDULE" }, 0, 846);

        Assert.That(
            incident.TrySubmit(
                anotherReport, validator, 0, 846).IsValid,
            Is.False);

        Assert.Throws<InvalidOperationException>(() =>
            new EvaluationContext(incident, hr, database));

        Assert.That(incident.Status, Is.EqualTo(finalStatus));
        Assert.That(incident.SubmittedReport, Is.SameAs(report));
        Assert.That(incident.Decision, Is.SameAs(decision));
    }

    [TestCase("D0_I3", "steve")]
    [TestCase("D0_I3", "karen")]
    [TestCase("D0_OTHER", "steve")]
    public void ForeignReportDecision_CannotChangePendingIncident(
        string instanceId, string suspectId)
    {
        var legitimate = SubmitAndEvaluate(
            new List<string> { "E_SUPPLIES_RECEIPT" });

        var acceptedReport = incident.SubmittedReport;

        var otherReport = new IncidentReport(
            instanceId, suspectId,
            new List<string>(acceptedReport.EvidenceIds), 0, 845);

        var foreignDecision = new HrEvaluationResult(
            otherReport, new List<HrRuleResult>());

        Assert.That(incident.TryApplyDecision(foreignDecision), Is.False);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(incident.SubmittedReport, Is.SameAs(acceptedReport));
        Assert.That(incident.Decision, Is.Null);

        Assert.That(incident.TryApplyDecision(legitimate), Is.True);
        Assert.That(incident.Decision, Is.SameAs(legitimate));
    }

    [Test]
    public void OpenIncident_CannotReceiveDecision()
    {
        var report = new IncidentReport(
            "D0_I3", "steve", new List<string>(), 0, 845);

        var decision = new HrEvaluationResult(
            report, new List<HrRuleResult>());

        Assert.That(incident.TryApplyDecision(decision), Is.False);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
        Assert.That(incident.Decision, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NullDecision_ThrowsWithoutChangingState(bool pending)
    {
        if (pending)
            SubmitAndEvaluate(new List<string> { "E_SUPPLIES_RECEIPT" });

        var originalStatus = incident.Status;
        var originalReport = incident.SubmittedReport;

        Assert.Throws<ArgumentNullException>(() =>
            incident.TryApplyDecision(null));

        Assert.That(incident.Status, Is.EqualTo(originalStatus));
        Assert.That(incident.SubmittedReport, Is.SameAs(originalReport));
        Assert.That(incident.Decision, Is.Null);
    }
}