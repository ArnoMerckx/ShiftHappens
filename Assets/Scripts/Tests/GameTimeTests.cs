using System;
using NUnit.Framework;

public class GameTimeTests
{
    [TestCase(0, 0, 0, 0, 0L)]
    [TestCase(0, 720, 12, 345, 43212345L)]
    [TestCase(1, 540, 0, 0, 118800000L)]
    [TestCase(
        int.MaxValue, 1439, 59, 999,
        185542587187199999L)]
    public void Constructors_RepresentTheSameInstant(
        int day,
        int minute,
        int second,
        int millisecond,
        long expectedTotal)
    {
        var calendar = new GameTime(
            day, minute, second, millisecond);

        var absolute = new GameTime(expectedTotal);

        Assert.That(
            calendar.TotalMilliseconds,
            Is.EqualTo(expectedTotal));

        Assert.That(absolute.DayIndex, Is.EqualTo(day));
        Assert.That(absolute.MinuteOfDay, Is.EqualTo(minute));
        Assert.That(absolute.SecondOfMinute, Is.EqualTo(second));
        Assert.That(
            absolute.MillisecondOfSecond,
            Is.EqualTo(millisecond));
    }

    [Test]
    public void DefaultValue_IsDayZeroMidnight()
    {
        var time = default(GameTime);

        Assert.That(time.TotalMilliseconds, Is.Zero);
        Assert.That(time.DayIndex, Is.Zero);
        Assert.That(time.MinuteOfDay, Is.Zero);
        Assert.That(time.SecondOfMinute, Is.Zero);
        Assert.That(time.MillisecondOfSecond, Is.Zero);
    }

    [Test]
    public void OneMillisecondAfterDayEnd_IsNextDayMidnight()
    {
        var lastInstant = new GameTime(0, 1439, 59, 999);
        var nextInstant = new GameTime(
            lastInstant.TotalMilliseconds + 1);

        Assert.That(
            lastInstant.TotalMilliseconds,
            Is.EqualTo(86399999L));

        Assert.That(nextInstant.DayIndex, Is.EqualTo(1));
        Assert.That(nextInstant.MinuteOfDay, Is.Zero);
        Assert.That(nextInstant.SecondOfMinute, Is.Zero);
        Assert.That(nextInstant.MillisecondOfSecond, Is.Zero);
    }

    [Test]
    public void DeadlineAndFirstLateInstant_RemainDistinct()
    {
        var deadline = new GameTime(0, 720);
        var firstLateInstant = new GameTime(0, 720, 0, 1);

        Assert.That(
            firstLateInstant.TotalMilliseconds
                - deadline.TotalMilliseconds,
            Is.EqualTo(1L));

        Assert.That(
            firstLateInstant.MinuteOfDay,
            Is.EqualTo(deadline.MinuteOfDay));
    }

    [TestCase(-1L)]
    [TestCase(long.MaxValue)]
    public void AbsoluteConstructor_RejectsInvalidTime(long total)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameTime(total));
    }

    [TestCase(-1, 0, 0, 0)]
    [TestCase(0, -1, 0, 0)]
    [TestCase(0, 1440, 0, 0)]
    [TestCase(0, 0, -1, 0)]
    [TestCase(0, 0, 60, 0)]
    [TestCase(0, 0, 0, -1)]
    [TestCase(0, 0, 0, 1000)]
    public void CalendarConstructor_RejectsInvalidComponents(
        int day,
        int minute,
        int second,
        int millisecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameTime(day, minute, second, millisecond));
    }

    [Test]
    public void AbsoluteConstructor_RejectsDayBeyondIntMaximum()
    {
        long firstUnsupportedInstant =
            ((long)int.MaxValue + 1)
            * GameTime.MillisecondsPerDay;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameTime(firstUnsupportedInstant));
    }
}