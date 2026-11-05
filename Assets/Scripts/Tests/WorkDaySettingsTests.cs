using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class WorkdaySettingsTests
{
    private static WorkdaySettings MakeSettings(
        int day = 0,
        int start = 540,
        int end = 1020,
        double rate = 20)
    {
        return new WorkdaySettings(
            "monday", "Monday", day, start, end, rate);
    }

    [Test]
    public void ValidSettings_StoreIdentityHoursAndRate()
    {
        var settings = MakeSettings();

        Assert.That(settings.DayId, Is.EqualTo("monday"));
        Assert.That(settings.DisplayName, Is.EqualTo("Monday"));
        Assert.That(settings.DayIndex, Is.Zero);
        Assert.That(settings.WorkHours.StartMinute, Is.EqualTo(540));
        Assert.That(settings.WorkHours.EndMinute, Is.EqualTo(1020));
        Assert.That(
            settings.OpensAt.TotalMilliseconds,
            Is.EqualTo(32400000L));
        Assert.That(
            settings.ClosesAt.TotalMilliseconds,
            Is.EqualTo(61200000L));
        Assert.That(settings.GameSecondsPerRealSecond, Is.EqualTo(20));
    }

    [Test]
    public void EndMinute1440_ClosesAtNextMidnight()
    {
        var settings = MakeSettings(day: 2, end: 1440);

        Assert.That(settings.DayIndex, Is.EqualTo(2));
        Assert.That(settings.ClosesAt.DayIndex, Is.EqualTo(3));
        Assert.That(settings.ClosesAt.MinuteOfDay, Is.Zero);
        Assert.That(settings.ClosesAt.SecondOfMinute, Is.Zero);
        Assert.That(settings.ClosesAt.MillisecondOfSecond, Is.Zero);
    }

    [Test]
    public void MaximumDay_SupportsClosingBeforeMidnight()
    {
        var settings = MakeSettings(
            day: int.MaxValue, end: 1439);

        Assert.That(
            settings.ClosesAt.DayIndex,
            Is.EqualTo(int.MaxValue));
        Assert.That(settings.ClosesAt.MinuteOfDay, Is.EqualTo(1439));
    }

    [Test]
    public void ClosingBeyondSupportedDay_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MakeSettings(day: int.MaxValue, end: 1440));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void BlankDayId_IsRejected(string id)
    {
        Assert.Throws<ArgumentException>(
            () => new WorkdaySettings(
                id, "Monday", 0, 540, 1020, 20));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void BlankDisplayName_IsRejected(string name)
    {
        Assert.Throws<ArgumentException>(
            () => new WorkdaySettings(
                "monday", name, 0, 540, 1020, 20));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void InvalidRate_IsRejected(double rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MakeSettings(rate: rate));
    }

    [Test]
    public void NegativeDay_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MakeSettings(day: -1));
    }

    [TestCase(-1, 1020)]
    [TestCase(1440, 1440)]
    [TestCase(540, -1)]
    [TestCase(540, 1441)]
    [TestCase(540, 540)]
    [TestCase(600, 540)]
    public void InvalidHours_AreRejected(int start, int end)
    {
        Assert.Catch<ArgumentException>(
            () => MakeSettings(start: start, end: end));
    }

    [Test]
    public void GetSettings_CopiesScalarsFromAuthoredDay()
    {
        var original = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");

        Assert.That(original, Is.Not.Null);

        var clone = UnityEngine.Object.Instantiate(original);

        try
        {
            WorkdaySettings before = clone.GetSettings();

            var serialized = new SerializedObject(clone);
            serialized.FindProperty("dayId").stringValue = "changed";
            serialized.FindProperty("displayName").stringValue = "Changed";
            serialized.FindProperty("endMinute").intValue = 960;
            serialized.FindProperty("gameSecondsPerRealSecond")
                .doubleValue = 10;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            WorkdaySettings after = clone.GetSettings();

            Assert.That(before.DayId, Is.EqualTo("monday"));
            Assert.That(before.DisplayName, Is.EqualTo("Monday"));
            Assert.That(before.WorkHours.EndMinute, Is.EqualTo(1020));
            Assert.That(before.GameSecondsPerRealSecond, Is.EqualTo(20));

            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after.DayId, Is.EqualTo("changed"));
            Assert.That(after.DisplayName, Is.EqualTo("Changed"));
            Assert.That(after.WorkHours.EndMinute, Is.EqualTo(960));
            Assert.That(after.GameSecondsPerRealSecond, Is.EqualTo(10));
            Assert.That(
                after.ClosesAt.TotalMilliseconds,
                Is.EqualTo(57600000L));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
        }
    }
}