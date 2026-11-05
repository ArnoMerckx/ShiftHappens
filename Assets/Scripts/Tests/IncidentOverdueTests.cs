using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class IncidentOverdueTests
{
    private IncidentDefinition definition;
    private IncidentInstance incident;
    private GameTime deadline;

    [SetUp]
    public void SetUp()
    {
        definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
        Assert.That(definition, Is.Not.Null);
        incident = new IncidentInstance(definition, new IncidentScheduleEntry(
            "D0I2", "I2", new TimeRange(0, 635, 640), 675, 720));
        deadline = new GameTime(0, 720);
    }

    private IncidentReport Submit()
    {
        var validator = new ReportSubmissionValidator(new List<string> { "dave" },
            new EvidenceService(new EvidenceDatabase(new List<EvidenceDefinition>())));
        var report = new IncidentReport("D0I2", "dave", new List<string>(), 0, 675);
        Assert.That(incident.TrySubmit(report, validator, 0, 675).IsValid, Is.True);
        return report;
    }

    [TestCase(-1, false)]
    [TestCase(0, false)]
    [TestCase(1, true)]
    public void OpenIncident_RespectsExactInclusiveDeadline(int offset, bool expected)
    {
        bool changed = incident.TryMarkOverdue(new GameTime(deadline.TotalMilliseconds + offset));
        Assert.That(changed, Is.EqualTo(expected));
        Assert.That(incident.Status,
            Is.EqualTo(expected ? IncidentStatus.Overdue : IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
        Assert.That(incident.Decision, Is.Null);
    }

    [Test]
    public void RepeatedOverdueTransition_ReturnsFalseAndPreservesState()
    {
        Assert.That(incident.TryMarkOverdue(new GameTime(deadline.TotalMilliseconds + 1)), Is.True);
        Assert.That(incident.TryMarkOverdue(new GameTime(1, 0)), Is.False);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Overdue));
        Assert.That(incident.SubmittedReport, Is.Null);
        Assert.That(incident.Decision, Is.Null);
    }

    [Test]
    public void PendingReport_CannotBecomeOverdue()
    {
        IncidentReport report = Submit();
        Assert.That(incident.TryMarkOverdue(new GameTime(1, 0)), Is.False);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(incident.SubmittedReport, Is.SameAs(report));
        Assert.That(incident.Decision, Is.Null);
    }

    [TestCase(HrRuleOutcome.Support, IncidentStatus.Approved)]
    [TestCase(HrRuleOutcome.Neutral, IncidentStatus.Rejected)]
    public void FinalDecision_CannotBeReplacedByOverdue(
        HrRuleOutcome outcome, IncidentStatus status)
    {
        IncidentReport report = Submit();
        var decision = new HrEvaluationResult(report, new List<HrRuleResult>
        {
            new HrRuleResult("test_rule", outcome, "Test lifecycle result.")
        });
        Assert.That(incident.TryApplyDecision(decision), Is.True);

        Assert.That(incident.TryMarkOverdue(new GameTime(1, 0)), Is.False);
        Assert.That(incident.Status, Is.EqualTo(status));
        Assert.That(incident.SubmittedReport, Is.SameAs(report));
        Assert.That(incident.Decision, Is.SameAs(decision));
    }

    [Test]
    public void LaterDay_IsAfterDeadline()
    {
        Assert.That(incident.TryMarkOverdue(new GameTime(1, 0)), Is.True);
    }

    [Test]
    public void EarlierDay_DoesNotExpireLaterDayIncident()
    {
        var later = new IncidentInstance(definition, new IncidentScheduleEntry(
            "later", "I2", new TimeRange(1, 635, 640), 675, 720));
        Assert.That(later.TryMarkOverdue(new GameTime(0, 1439)), Is.False);
        Assert.That(later.Status, Is.EqualTo(IncidentStatus.Open));
    }

    [Test]
    public void FinalSupportedDay_StillSupportsFirstLateMillisecond()
    {
        var lastDay = new IncidentInstance(definition, new IncidentScheduleEntry(
            "last", "I2", new TimeRange(int.MaxValue, 1430, 1435), 1436, 1439));
        Assert.That(lastDay.TryMarkOverdue(new GameTime(int.MaxValue, 1439)), Is.False);
        Assert.That(lastDay.TryMarkOverdue(new GameTime(int.MaxValue, 1439, 0, 1)), Is.True);
    }
}
