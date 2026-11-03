using System;
using System.Collections.Generic;
using NUnit.Framework;

public class IncidentReportTests
{
    [TestCase(0, 845)]
    [TestCase(0, 0)]
    [TestCase(2, 1439)]
    public void ValidReport_StoresSubmittedValues(int day, int minute)
    {
        var report = new IncidentReport(
            "D0_I3", "steve",
            new List<string> { "E_SUPPLIES_RECEIPT" },
            day, minute);

        Assert.That(report.IncidentInstanceId, Is.EqualTo("D0_I3"));
        Assert.That(report.SuspectId, Is.EqualTo("steve"));
        Assert.That(
            report.EvidenceIds,
            Is.EqualTo(new[] { "E_SUPPLIES_RECEIPT" }));
        Assert.That(report.SubmittedDayIndex, Is.EqualTo(day));
        Assert.That(report.SubmittedMinute, Is.EqualTo(minute));
    }

    [Test]
    public void EvidenceSelection_IsAnOrderedReadOnlySnapshot()
    {
        var selected = new List<string>
        {
            "E_SCHEDULE", "E_BOB_DENTIST"
        };

        var report = new IncidentReport(
            "D0_I2", "bob", selected, 0, 680);

        selected.Clear();
        selected.Add("E_OFFICE_SOCIAL");

        Assert.That(
            report.EvidenceIds,
            Is.EqualTo(new[] { "E_SCHEDULE", "E_BOB_DENTIST" }));

        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)report.EvidenceIds).Add("E_OFFICE_SOCIAL"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankIdentity_IsRejected(string invalid)
    {
        var evidence = new List<string> { "E_SCHEDULE" };

        Assert.Throws<ArgumentException>(() =>
            new IncidentReport(invalid, "bob", evidence, 0, 600));

        Assert.Throws<ArgumentException>(() =>
            new IncidentReport("D0_I1", invalid, evidence, 0, 600));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankEvidenceId_IsRejected(string invalid)
    {
        Assert.Throws<ArgumentException>(() =>
            new IncidentReport(
                "D0_I1", "bob",
                new List<string> { "E_SCHEDULE", invalid },
                0, 600));
    }

    [Test]
    public void NullEvidenceSelection_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IncidentReport("D0_I1", "bob", null, 0, 600));
    }

    [Test]
    public void DuplicateEvidenceId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new IncidentReport(
                "D0_I1", "bob",
                new List<string> { "E_SCHEDULE", "E_SCHEDULE" },
                0, 600));
    }

    [Test]
    public void NegativeDay_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IncidentReport(
                "D0_I1", "bob", new List<string>(), -1, 600));
    }

    [TestCase(-1)]
    [TestCase(1440)]
    public void InvalidMinute_IsRejected(int minute)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IncidentReport(
                "D0_I1", "bob", new List<string>(), 0, minute));
    }

    [Test]
    public void EmptyEvidenceSelection_IsRepresentable()
    {
        var report = new IncidentReport(
            "D0_I1", "bob", new List<string>(), 0, 600);

        Assert.That(report.EvidenceIds, Is.Empty);
    }
}