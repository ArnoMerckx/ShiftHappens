using System;
using NUnit.Framework;

public class TimeRangeTests
{
    [Test]
    public void Constructor_StoresValues()
    {
        var range = new TimeRange(2, 540, 600);

        Assert.That(range.DayIndex, Is.EqualTo(2));
        Assert.That(range.StartMinute, Is.EqualTo(540));
        Assert.That(range.EndMinute, Is.EqualTo(600));
    }

    [TestCase(0, 540, true)]
    [TestCase(0, 599, true)]
    [TestCase(0, 600, false)]
    [TestCase(0, 539, false)]
    [TestCase(1, 550, false)]
    [TestCase(0, -1, false)]
    [TestCase(0, 1440, false)]
    public void Contains_RespectsDayAndBoundaries(int day, int minute, bool expected)
    {
        var range = new TimeRange(0, 540, 600);

        Assert.That(range.Contains(day, minute), Is.EqualTo(expected));
    }

    [TestCase(0, 590, 620, true)]
    [TestCase(0, 550, 560, true)]
    [TestCase(0, 600, 660, false)]
    [TestCase(0, 480, 540, false)]
    [TestCase(1, 540, 600, false)]
    [TestCase(0, 540, 600, true)]
    public void Overlaps_RespectsDayAndBoundaries(int day, int start, int end, bool expected)
    {
        var range = new TimeRange(0, 540, 600);
        var other = new TimeRange(day, start, end);

        Assert.That(range.Overlaps(other), Is.EqualTo(expected));
        Assert.That(other.Overlaps(range), Is.EqualTo(expected));
    }

    [TestCase(-1, 540, 600)]
    [TestCase(0, -1, 600)]
    [TestCase(0, 1440, 1440)]
    [TestCase(0, 540, -1)]
    [TestCase(0, 540, 1441)]
    [TestCase(0, 540, 540)]
    [TestCase(0, 600, 540)]
    public void Constructor_RejectsInvalidRanges(int day, int start, int end)
    {
        Assert.Catch<ArgumentException>(() =>
        {
            new TimeRange(day, start, end);
        });
    }

    [Test]
    public void FinalMinute_AllowsExclusiveMidnightEnd()
    {
        var range = new TimeRange(0, 1439, 1440);

        Assert.That(range.Contains(0, 1439), Is.True);
        Assert.That(range.Contains(0, 1440), Is.False);
    }

    [TestCase(0)]
    [TestCase(540)]
    public void DefaultRange_ContainsAndOverlapsNothing(int start)
    {
        var empty = default(TimeRange);
        var valid = new TimeRange(0, start, start + 60);

        Assert.That(empty.Contains(0, start), Is.False);
        Assert.That(empty.Overlaps(valid), Is.False);
        Assert.That(valid.Overlaps(empty), Is.False);
        Assert.That(empty.Overlaps(empty), Is.False);
    }
}