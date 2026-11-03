using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class HrEvaluationTests
{
    private static IncidentReport NewReport()
    {
        return new IncidentReport(
            "TEST", "bob", new List<string>(), 0, 680);
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(20, false)]
    [TestCase(20, true)]
    public void Blocker_WinsRegardlessOfSupportCountOrOrder(
        int supportCount, bool reverse)
    {
        var results = new List<HrRuleResult>();

        for (int i = 0; i < supportCount; i++)
        {
            results.Add(new HrRuleResult(
                $"support_{i}", HrRuleOutcome.Support, "Test support."));
        }

        results.Add(new HrRuleResult(
            "blocker", HrRuleOutcome.Blocker, "Test contradiction."));

        if (reverse)
            results.Reverse();

        var decision = new HrEvaluationResult(NewReport(), results);

        Assert.That(decision.Verdict, Is.EqualTo(HrVerdict.Rejected));
        Assert.That(decision.RuleResults.Count, Is.EqualTo(supportCount + 1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingPositiveSupport_IsRejected(bool includeNeutral)
    {
        var results = new List<HrRuleResult>();

        if (includeNeutral)
        {
            results.Add(new HrRuleResult(
                "neutral", HrRuleOutcome.Neutral, "No positive support."));
        }

        var decision = new HrEvaluationResult(NewReport(), results);

        Assert.That(decision.Verdict, Is.EqualTo(HrVerdict.Rejected));
        Assert.That(decision.Summary, Does.Contain("no relevant support"));
    }

    [Test]
    public void AggregateResults_AreProtectedSnapshots()
    {
        var report = NewReport();
        var support = new HrRuleResult(
            "support", HrRuleOutcome.Support, "Test support.");

        var input = new List<HrRuleResult> { support };
        var decision = new HrEvaluationResult(report, input);

        input.Clear();

        Assert.That(decision.Report, Is.SameAs(report));
        Assert.That(decision.Verdict, Is.EqualTo(HrVerdict.Approved));
        Assert.That(decision.RuleResults.Count, Is.EqualTo(1));
        Assert.That(decision.RuleResults[0], Is.SameAs(support));

        Assert.Throws<NotSupportedException>(() =>
            ((IList<HrRuleResult>)decision.RuleResults).Clear());
    }

    [Test]
    public void MalformedAggregateInputs_AreRejected()
    {
        var report = NewReport();

        Assert.Throws<ArgumentNullException>(() =>
            new HrEvaluationResult(null, new List<HrRuleResult>()));

        Assert.Throws<ArgumentNullException>(() =>
            new HrEvaluationResult(report, null));

        Assert.Throws<ArgumentException>(() =>
            new HrEvaluationResult(
                report, new List<HrRuleResult> { null }));

        Assert.Throws<ArgumentException>(() =>
            new HrEvaluationResult(report, new List<HrRuleResult>
            {
                new HrRuleResult(
                    "same", HrRuleOutcome.Support, "Test support."),
                new HrRuleResult(
                    "same", HrRuleOutcome.Blocker, "Test blocker.")
            }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankRuleIdentityOrExplanation_IsRejected(string invalid)
    {
        Assert.Throws<ArgumentException>(() =>
            new HrRuleResult(
                invalid, HrRuleOutcome.Neutral, "Test reason."));

        Assert.Throws<ArgumentException>(() =>
            new HrRuleResult(
                "test", HrRuleOutcome.Neutral, invalid));
    }

    [Test]
    public void UndefinedRuleOutcome_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HrRuleResult(
                "test", (HrRuleOutcome)999, "Test reason."));
    }

    private static EvaluationContext BuildContext(
        string suspect, List<string> selected)
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

        var database = new EvidenceDatabase(documents);
        var service = new EvidenceService(database);

        foreach (var id in selected)
            service.MarkRead(id, 0, 675);

        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");

        Assert.That(definition, Is.Not.Null);

        var incident = new IncidentInstance(
            definition,
            new IncidentScheduleEntry(
                "D0_I2", "I2", new TimeRange(0, 635, 640), 675, 720));

        var report = new IncidentReport(
            "D0_I2", suspect, selected, 0, 680);

        var validator = new ReportSubmissionValidator(
            new List<string> { "bob", "alice", "dave", "steve", "karen" },
            service);

        Assert.That(
            incident.TrySubmit(report, validator, 0, 680).IsValid,
            Is.True);

        var known = new List<Claim>();
        known.AddRange(database.GetById("E_BOB_DENTIST").GetClaims());
        known.AddRange(database.GetById("E_DAVE_PRINTER").GetClaims());

        var hr = new HrKnowledge(
            new List<ApprovedScheduleRecord>
            {
                new ApprovedScheduleRecord(
                    "dentist", "bob", "offsite",
                    new TimeRange(0, 600, 690), 100, true),

                new ApprovedScheduleRecord(
                    "inspection", "dave", "printer_area",
                    new TimeRange(0, 630, 645), 100, false)
            },
            known);

        return new EvaluationContext(incident, hr, database);
    }

    [TestCase(
        "bob", "E_SCHEDULE", "E_OFFICE_SOCIAL", HrVerdict.Rejected)]
    [TestCase(
        "bob", "E_SCHEDULE", "E_BOB_DENTIST", HrVerdict.Rejected)]
    [TestCase(
        "dave", "E_OFFICE_SOCIAL", "E_SCHEDULE", HrVerdict.Rejected)]
    [TestCase(
        "dave", "E_DAVE_PRINTER", "E_OFFICE_SOCIAL", HrVerdict.Approved)]
    [TestCase(
        "dave", "E_DAVE_PRINTER", "E_BOB_DENTIST", HrVerdict.Approved)]
    [TestCase(
        "steve", "E_SCHEDULE", "E_DAVE_PRINTER", HrVerdict.Rejected)]
    public void MixedEvidence_PreservesRelevantDecision(
        string suspect, string first, string second, HrVerdict expected)
    {
        var context = BuildContext(
            suspect, new List<string> { first, second });

        var decision = new HrEvaluator().Evaluate(context);

        Assert.That(decision.Verdict, Is.EqualTo(expected));
        Assert.That(
            context.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
    }

    private static void AssertEquivalent(
        HrEvaluationResult first, HrEvaluationResult second)
    {
        Assert.That(second.Verdict, Is.EqualTo(first.Verdict));
        Assert.That(second.Summary, Is.EqualTo(first.Summary));
        Assert.That(
            second.RuleResults.Count, Is.EqualTo(first.RuleResults.Count));

        for (int i = 0; i < first.RuleResults.Count; i++)
        {
            Assert.That(
                second.RuleResults[i].RuleId,
                Is.EqualTo(first.RuleResults[i].RuleId));
            Assert.That(
                second.RuleResults[i].Outcome,
                Is.EqualTo(first.RuleResults[i].Outcome));
            Assert.That(
                second.RuleResults[i].Reason,
                Is.EqualTo(first.RuleResults[i].Reason));
        }
    }

    [Test]
    public void RepeatedAndReorderedEvidence_ProducesSameDecision()
    {
        var selected = new List<string>
        {
            "E_DAVE_PRINTER", "E_OFFICE_SOCIAL"
        };

        var context = BuildContext("dave", selected);
        var evaluator = new HrEvaluator();

        var first = evaluator.Evaluate(context);
        var repeated = evaluator.Evaluate(context);

        selected.Reverse();

        var reordered = evaluator.Evaluate(
            BuildContext("dave", selected));

        AssertEquivalent(first, repeated);
        AssertEquivalent(first, reordered);
        Assert.That(first.Report, Is.SameAs(context.Report));
        Assert.That(
            context.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(
            context.Incident.SubmittedReport, Is.SameAs(context.Report));
        Assert.That(context.HrKnowledge.KnownClaims.Count, Is.EqualTo(2));
    }

    [Test]
    public void NullEvaluatorContext_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HrEvaluator().Evaluate(null));
    }
}