using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class QueuedHrReviewTests
{
    private IncidentDefinition definition;
    private IncidentInstance incident;
    private IncidentReport report;
    private GameTime submittedAt;
    private readonly List<UnityEngine.Object> temporaryObjects =
        new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
        Assert.That(definition, Is.Not.Null);
        incident = new IncidentInstance(definition, new IncidentScheduleEntry(
            "D0I2", "I2", new TimeRange(0, 635, 640), 675, 720));
        report = new IncidentReport("D0I2", "dave", new List<string>(), 0, 675);
        submittedAt = new GameTime(0, 675, 30, 250);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var temporary in temporaryObjects)
        {
            if (temporary != null)
                UnityEngine.Object.DestroyImmediate(temporary);
        }
        temporaryObjects.Clear();
    }

    private IncidentInstance WithDelay(int delay, int dayIndex = 0)
    {
        var clone = UnityEngine.Object.Instantiate(definition);
        temporaryObjects.Add(clone);
        var serialized = new SerializedObject(clone);
        serialized.FindProperty("reviewDelayMinutes").intValue = delay;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return new IncidentInstance(clone, new IncidentScheduleEntry(
            "D0I2", "I2", new TimeRange(dayIndex, 635, 640), 675, 720));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Constructor_RejectsNullReference(bool missingIncident)
    {
        Assert.Throws<ArgumentNullException>(() => new QueuedHrReview(
            missingIncident ? null : incident,
            missingIncident ? report : null,
            submittedAt));
    }

    [Test]
    public void ReportForAnotherInstance_IsRejected()
    {
        var otherReport = new IncidentReport(
            "another_instance", "dave", new List<string>(), 0, 675);
        Assert.Throws<ArgumentException>(
            () => new QueuedHrReview(incident, otherReport, submittedAt));
    }

    [TestCase(1, 675)]
    [TestCase(0, 676)]
    public void ReportDayAndMinute_MustMatchPreciseTimestamp(int day, int minute)
    {
        var otherReport = new IncidentReport(
            "D0I2", "dave", new List<string>(), day, minute);
        Assert.Throws<ArgumentException>(
            () => new QueuedHrReview(incident, otherReport, submittedAt));
    }

    [Test]
    public void ReviewDelay_CanCrossMidnightWithoutLosingPrecision()
    {
        var lateIncident = new IncidentInstance(definition, new IncidentScheduleEntry(
            "late", "I2", new TimeRange(0, 1430, 1435), 1436, 1439));
        var lateReport = new IncidentReport(
            "late", "dave", new List<string>(), 0, 1438);
        var preciseTime = new GameTime(0, 1438, 30, 250);

        var review = new QueuedHrReview(lateIncident, lateReport, preciseTime);

        Assert.That(review.SubmittedAt.TotalMilliseconds,
            Is.EqualTo(preciseTime.TotalMilliseconds));
        Assert.That(review.ReviewAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(1, 13, 30, 250).TotalMilliseconds));
    }

    [Test]
    public void DelayConversion_UsesLongArithmetic()
    {
        var longDelayIncident = WithDelay(int.MaxValue);
        var review = new QueuedHrReview(longDelayIncident, report, submittedAt);

        Assert.That(
            review.ReviewAt.TotalMilliseconds - review.SubmittedAt.TotalMilliseconds,
            Is.EqualTo(128849018820000L));
    }

    [Test]
    public void Constructor_RetainsExactReferencesAndDoesNotSubmitReport()
    {
        var review = new QueuedHrReview(incident, report, submittedAt);

        Assert.That(review.Incident, Is.SameAs(incident));
        Assert.That(review.Report, Is.SameAs(report));
        Assert.That(review.SubmittedAt.TotalMilliseconds,
            Is.EqualTo(submittedAt.TotalMilliseconds));
        Assert.That(review.ReviewAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 690, 30, 250).TotalMilliseconds));
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(incident.SubmittedReport, Is.Null);
        Assert.That(incident.Decision, Is.Null);
    }

    [Test]
    public void ReviewBeyondSupportedTimeline_IsRejected()
    {
        var lastDayIncident = WithDelay(int.MaxValue, int.MaxValue);
        var lastReport = new IncidentReport(
            "D0I2", "dave", new List<string>(), int.MaxValue, 675);
        var lastTime = new GameTime(int.MaxValue, 675, 30, 250);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new QueuedHrReview(lastDayIncident, lastReport, lastTime));
    }
}
