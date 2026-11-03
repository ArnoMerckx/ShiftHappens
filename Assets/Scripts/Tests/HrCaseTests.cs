using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class HrCaseTests
{
    private EvidenceDatabase database;
    private EvidenceService service;
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

        var approved = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "dentist", "bob", "offsite",
                new TimeRange(0, 600, 690), 100, true),

            new ApprovedScheduleRecord(
                "meeting", "alice", "accounts_office",
                new TimeRange(0, 565, 590), 100, true),

            new ApprovedScheduleRecord(
                "inspection", "dave", "printer_area",
                new TimeRange(0, 630, 645), 100, false),

            new ApprovedScheduleRecord(
                "collection", "karen", "supplies_area",
                new TimeRange(0, 810, 825), 100, false)
        };

        var knownClaims = new List<Claim>();

        foreach (var id in new[]
        {
            "E_BOB_DENTIST",
            "E_ALICE_MEETING",
            "E_DAVE_PRINTER",
            "E_KAREN_COLLECTION"
        })
        {
            knownClaims.AddRange(database.GetById(id).GetClaims());
        }

        hr = new HrKnowledge(approved, knownClaims);
    }

    [TestCase(
        "I1_CoffeeSpill", "bob", "E_SCHEDULE",
        575, 580, 590, 630, HrVerdict.Approved, false)]
    [TestCase(
        "I1_CoffeeSpill", "alice", "E_SCHEDULE",
        575, 580, 590, 630, HrVerdict.Rejected, true)]
    [TestCase(
        "I2_PrinterDamage", "dave", "E_DAVE_PRINTER",
        635, 640, 675, 720, HrVerdict.Approved, false)]
    [TestCase(
        "I2_PrinterDamage", "bob", "E_SCHEDULE",
        635, 640, 675, 720, HrVerdict.Rejected, true)]
    [TestCase(
        "I2_PrinterDamage", "dave", "E_OFFICE_SOCIAL",
        635, 640, 675, 720, HrVerdict.Rejected, false)]
    [TestCase(
        "I3MissingSupplies", "karen", "E_KAREN_COLLECTION",
        815, 820, 840, 900, HrVerdict.Approved, false)]
    [TestCase(
        "I3MissingSupplies", "steve", "E_SUPPLIES_RECEIPT",
        815, 820, 840, 900, HrVerdict.Approved, false)]
    [TestCase(
        "I3MissingSupplies", "steve", "E_SCHEDULE",
        815, 820, 840, 900, HrVerdict.Rejected, false)]
    public void AgreedAccusation_ProducesExpectedVerdict(
        string filename, string suspect, string evidenceId,
        int start, int end, int delivery, int deadline,
        HrVerdict expectedVerdict, bool expectedBlocker)
    {
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            $"Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/{filename}.asset");

        Assert.That(definition, Is.Not.Null);

        string instanceId = $"D0_{definition.IncidentId}";

        var incident = new IncidentInstance(
            definition,
            new IncidentScheduleEntry(
                instanceId, definition.IncidentId,
                new TimeRange(0, start, end), delivery, deadline));

        Assert.That(
            service.MarkRead(evidenceId, 0, delivery), Is.True);

        var report = new IncidentReport(
            instanceId, suspect,
            new List<string> { evidenceId }, 0, delivery);

        var validator = new ReportSubmissionValidator(
            new List<string> { "bob", "alice", "dave", "steve", "karen" },
            service);

        Assert.That(
            incident.TrySubmit(report, validator, 0, delivery).IsValid,
            Is.True);

        var context = new EvaluationContext(incident, hr, database);
        var result = new HrEvaluator().Evaluate(context);

        Assert.That(result.Report, Is.SameAs(report));
        Assert.That(result.Verdict, Is.EqualTo(expectedVerdict));
        Assert.That(string.IsNullOrWhiteSpace(result.Summary), Is.False);
        Assert.That(result.RuleResults.Count, Is.EqualTo(3));

        var expectedRuleIds = new[]
        {
            "presence", "activity_support", "custody_support"
        };

        bool hasBlocker = false;
        bool hasSupport = false;

        for (int i = 0; i < result.RuleResults.Count; i++)
        {
            var ruleResult = result.RuleResults[i];

            Assert.That(ruleResult.RuleId, Is.EqualTo(expectedRuleIds[i]));
            Assert.That(
                string.IsNullOrWhiteSpace(ruleResult.Reason), Is.False);

            if (ruleResult.Outcome == HrRuleOutcome.Blocker)
                hasBlocker = true;

            if (ruleResult.Outcome == HrRuleOutcome.Support)
                hasSupport = true;
        }

        Assert.That(hasBlocker, Is.EqualTo(expectedBlocker));

        if (expectedVerdict == HrVerdict.Approved)
            Assert.That(hasSupport, Is.True);
        else if (!expectedBlocker)
            Assert.That(hasSupport, Is.False);

        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(incident.SubmittedReport, Is.SameAs(report));
        Assert.That(service.IsDiscovered("E_BOB_DENTIST"), Is.False);
        Assert.That(hr.KnownClaims.Count, Is.EqualTo(4));

        foreach (var known in hr.KnownClaims)
            Assert.That(
                known.ClaimId, Is.Not.EqualTo("C_SUPPLIES_HANDOVER"));
    }
}