using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class PresenceRuleTests
{
    private static EvaluationContext BuildContext(
        string filename,
        string suspect,
        TimeRange window,
        List<ApprovedScheduleRecord> approved,
        List<Claim> knownClaims = null,
        string evidenceId = null)
    {
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            $"Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/{filename}.asset");

        Assert.That(definition, Is.Not.Null);

        var documents = new List<EvidenceDefinition>();
        var selected = new List<string>();

        if (evidenceId != null)
        {
            var document =
                AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(
                    $"Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents/{evidenceId}.asset");

            Assert.That(document, Is.Not.Null);
            documents.Add(document);
            selected.Add(evidenceId);
        }

        var database = new EvidenceDatabase(documents);
        var service = new EvidenceService(database);

        int delivery = window.EndMinute + 10;

        var incident = new IncidentInstance(
            definition,
            new IncidentScheduleEntry(
                "TEST_INSTANCE", definition.IncidentId,
                window, delivery, delivery + 30));

        foreach (var id in selected)
            service.MarkRead(id, window.DayIndex, delivery);

        var report = new IncidentReport(
            incident.InstanceId, suspect, selected,
            window.DayIndex, delivery);

        var validator = new ReportSubmissionValidator(
            new List<string> { "bob", "alice", "dave", "steve", "karen" },
            service);

        Assert.That(
            incident.TrySubmit(
                report, validator, window.DayIndex, delivery).IsValid,
            Is.True);

        var hr = new HrKnowledge(
            approved, knownClaims ?? new List<Claim>());

        return new EvaluationContext(incident, hr, database);
    }

    private static HrRuleResult AssertOutcome(
        EvaluationContext context, HrRuleOutcome expected)
    {
        var report = context.Incident.SubmittedReport;
        var result = new PresenceRule().Evaluate(context);

        Assert.That(result.RuleId, Is.EqualTo("presence"));
        Assert.That(result.Outcome, Is.EqualTo(expected));
        Assert.That(string.IsNullOrWhiteSpace(result.Reason), Is.False);
        Assert.That(
            context.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(context.Incident.SubmittedReport, Is.SameAs(report));

        return result;
    }

    [TestCase("I1_CoffeeSpill", 575, 580, HrRuleOutcome.Neutral)]
    [TestCase("I2_PrinterDamage", 635, 640, HrRuleOutcome.Blocker)]
    public void DentistAbsence_AffectsOnlyRelevantWindow(
        string filename, int start, int end, HrRuleOutcome expected)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "dentist", "bob", "offsite",
                new TimeRange(0, 600, 690), 100, true)
        };

        var context = BuildContext(
            filename, "bob", new TimeRange(0, start, end), records);

        AssertOutcome(context, expected);
    }

    [TestCase(630, 635, HrRuleOutcome.Neutral)]
    [TestCase(635, 640, HrRuleOutcome.Blocker)]
    [TestCase(635, 637, HrRuleOutcome.Neutral)]
    [TestCase(637, 640, HrRuleOutcome.Neutral)]
    [TestCase(640, 645, HrRuleOutcome.Neutral)]
    public void Exclusion_RequiresWholeWindowCoverage(
        int start, int end, HrRuleOutcome expected)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "absence", "bob", "offsite",
                new TimeRange(0, start, end), 100, true)
        };

        AssertOutcome(
            BuildContext(
                "I2_PrinterDamage", "bob",
                new TimeRange(0, 635, 640), records),
            expected);
    }

    [TestCase("printer_area", false)]
    [TestCase("printer_area", true)]
    [TestCase("it_office", false)]
    public void NonblockingAssignments_DoNotSupplyReportSupport(
        string location, bool requiresPresence)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "assignment", "bob", location,
                new TimeRange(0, 630, 645), 100, requiresPresence)
        };

        AssertOutcome(
            BuildContext(
                "I2_PrinterDamage", "bob",
                new TimeRange(0, 635, 640), records),
            HrRuleOutcome.Neutral);
    }

    [Test]
    public void MissingRecords_LeavePresenceUnknown()
    {
        var context = BuildContext(
            "I2_PrinterDamage", "bob", new TimeRange(0, 635, 640),
            new List<ApprovedScheduleRecord>());

        var result = AssertOutcome(context, HrRuleOutcome.Neutral);

        Assert.That(result.Reason, Does.Contain("unknown"));
    }

    [TestCase(true, HrRuleOutcome.Neutral)]
    [TestCase(false, HrRuleOutcome.Blocker)]
    public void HighestPriorityConstraint_DeterminesExclusion(
        bool assignmentWins, HrRuleOutcome expected)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "elsewhere", "bob", "offsite",
                new TimeRange(0, 630, 645),
                assignmentWins ? 10 : 100, true),

            new ApprovedScheduleRecord(
                "assignment", "bob", "printer_area",
                new TimeRange(0, 630, 645),
                assignmentWins ? 100 : 10, false)
        };

        AssertOutcome(
            BuildContext(
                "I2_PrinterDamage", "bob",
                new TimeRange(0, 635, 640), records),
            expected);
    }

    [Test]
    public void OtherEmployeeAndDay_DoNotExcludeSuspect()
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "other_employee", "alice", "offsite",
                new TimeRange(0, 630, 645), 100, true),

            new ApprovedScheduleRecord(
                "other_day", "bob", "offsite",
                new TimeRange(1, 630, 645), 100, true)
        };

        AssertOutcome(
            BuildContext(
                "I2_PrinterDamage", "bob",
                new TimeRange(0, 635, 640), records),
            HrRuleOutcome.Neutral);
    }

    [Test]
    public void KnownClaim_IsNotPromotedToApprovedConstraint()
    {
        var claims = new List<Claim>
        {
            new WhereaboutsClaim(
                "claimed_absence", "bob", "offsite",
                new TimeRange(0, 600, 690), true)
        };

        AssertOutcome(
            BuildContext(
                "I2_PrinterDamage", "bob",
                new TimeRange(0, 635, 640),
                new List<ApprovedScheduleRecord>(),
                knownClaims: claims),
            HrRuleOutcome.Neutral);
    }

    [Test]
    public void SubmittedClaim_IsNotPromotedToApprovedConstraint()
    {
        AssertOutcome(
            BuildContext(
                "I2_PrinterDamage", "bob",
                new TimeRange(0, 635, 640),
                new List<ApprovedScheduleRecord>(),
                evidenceId: "E_BOB_DENTIST"),
            HrRuleOutcome.Neutral);
    }

    [Test]
    public void ConflictingVerifiedRequirements_AreConfigurationError()
    {
        var records = new List<ApprovedScheduleRecord>
        {
            new ApprovedScheduleRecord(
                "offsite", "bob", "offsite",
                new TimeRange(0, 630, 645), 100, true),

            new ApprovedScheduleRecord(
                "printer", "bob", "printer_area",
                new TimeRange(0, 630, 645), 100, true)
        };

        var context = BuildContext(
            "I2_PrinterDamage", "bob",
            new TimeRange(0, 635, 640), records);

        Assert.Throws<InvalidOperationException>(() =>
            new PresenceRule().Evaluate(context));
    }

    [Test]
    public void NullContext_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PresenceRule().Evaluate(null));
    }
}