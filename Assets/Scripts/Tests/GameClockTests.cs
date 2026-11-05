using System;
using NUnit.Framework;

public class GameClockTests
{
    private static void Tick(GameClock clock, double realSeconds)
    {
        clock.AdvanceTo(
            clock.ConsumeElapsedSeconds(realSeconds));
    }

    [Test]
    public void Constructor_StoresStartingState()
    {
        var clock = new GameClock(new GameTime(0, 540));

        Assert.That(
            clock.Now.TotalMilliseconds,
            Is.EqualTo(32400000L));
        Assert.That(clock.GameSecondsPerRealSecond, Is.EqualTo(20));
        Assert.That(clock.IsPaused, Is.False);
    }

    [Test]
    public void Conversion_ReturnsTargetBeforeMovingClock()
    {
        var clock = new GameClock(new GameTime(0, 540));

        GameTime target = clock.ConsumeElapsedSeconds(1);

        Assert.That(
            target.TotalMilliseconds,
            Is.EqualTo(32420000L));
        Assert.That(
            clock.Now.TotalMilliseconds,
            Is.EqualTo(32400000L));

        clock.AdvanceTo(target);

        Assert.That(
            clock.Now.TotalMilliseconds,
            Is.EqualTo(target.TotalMilliseconds));
    }

    [Test]
    public void Advancement_CrossesMidnight()
    {
        var clock = new GameClock(
            new GameTime(0, 1439, 59), 1);

        Tick(clock, 2);

        Assert.That(clock.Now.DayIndex, Is.EqualTo(1));
        Assert.That(clock.Now.MinuteOfDay, Is.Zero);
        Assert.That(clock.Now.SecondOfMinute, Is.EqualTo(1));
    }

    [Test]
    public void SmallIntervals_RetainFractionalMilliseconds()
    {
        var clock = new GameClock(default(GameTime), 1);

        Tick(clock, 0.0005);

        Assert.That(clock.Now.TotalMilliseconds, Is.Zero);

        Tick(clock, 0.0005);

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(1L));
    }

    [Test]
    public void SplittingElapsedTime_PreservesTotalProgress()
    {
        var whole = new GameClock(default(GameTime), 1);
        var split = new GameClock(default(GameTime), 1);

        Tick(whole, 0.125);

        for (int i = 0; i < 500; i++)
            Tick(split, 0.00025);

        Assert.That(whole.Now.TotalMilliseconds, Is.EqualTo(125L));
        Assert.That(
            split.Now.TotalMilliseconds,
            Is.EqualTo(whole.Now.TotalMilliseconds));
    }

    [Test]
    public void RateChange_AffectsFutureElapsedAndPreservesCarry()
    {
        var clock = new GameClock(default(GameTime), 1);

        Tick(clock, 0.0005);
        clock.SetRate(2);
        Tick(clock, 0.00025);

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(1L));

        Tick(clock, 1);

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(2001L));
    }

    [Test]
    public void Pause_DiscardsPausedElapsedAndPreservesCarry()
    {
        var clock = new GameClock(default(GameTime), 1);

        Tick(clock, 0.0005);
        clock.SetPaused(true);
        Tick(clock, 100);

        Assert.That(clock.IsPaused, Is.True);
        Assert.That(clock.Now.TotalMilliseconds, Is.Zero);

        clock.SetPaused(false);
        Tick(clock, 0.0005);

        Assert.That(clock.IsPaused, Is.False);
        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(1L));
    }

    [Test]
    public void ZeroElapsed_DoesNotConsumeExistingCarry()
    {
        var clock = new GameClock(default(GameTime), 1);

        Tick(clock, 0.0005);
        Tick(clock, 0);
        Tick(clock, 0.0005);

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(1L));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void InvalidRate_IsRejectedByConstructorAndSetter(
        double rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameClock(default(GameTime), rate));

        var clock = new GameClock(default(GameTime));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => clock.SetRate(rate));

        Assert.That(clock.GameSecondsPerRealSecond, Is.EqualTo(20));
    }

    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void InvalidElapsed_IsRejectedEvenWhilePaused(
        double elapsed)
    {
        var clock = new GameClock(default(GameTime));
        clock.SetPaused(true);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => clock.ConsumeElapsedSeconds(elapsed));

        Assert.That(clock.Now.TotalMilliseconds, Is.Zero);
    }

    [TestCase(2)]
    [TestCase(double.MaxValue)]
    public void Overflow_LeavesTimeAndCarryUnchanged(
        double elapsed)
    {
        var maximum = new GameTime(
            int.MaxValue, 1439, 59, 999);

        var clock = new GameClock(
            new GameTime(maximum.TotalMilliseconds - 1), 1);

        Tick(clock, 0.0005);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => clock.ConsumeElapsedSeconds(elapsed));

        Assert.That(
            clock.Now.TotalMilliseconds,
            Is.EqualTo(maximum.TotalMilliseconds - 1));

        Tick(clock, 0.0005);

        Assert.That(
            clock.Now.TotalMilliseconds,
            Is.EqualTo(maximum.TotalMilliseconds));
    }

    [Test]
    public void AdvanceTo_AllowsForwardAndSameTimeButRejectsBackward()
    {
        var clock = new GameClock(default(GameTime));

        clock.AdvanceTo(new GameTime(1000L));
        clock.AdvanceTo(new GameTime(1000L));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => clock.AdvanceTo(new GameTime(999L)));

        Assert.That(clock.Now.TotalMilliseconds, Is.EqualTo(1000L));
    }

    [Test]
    public void AdvanceTo_RejectsMovementWhilePaused()
    {
        var clock = new GameClock(default(GameTime));
        clock.SetPaused(true);

        clock.AdvanceTo(clock.Now);

        Assert.Throws<InvalidOperationException>(
            () => clock.AdvanceTo(new GameTime(1L)));

        Assert.That(clock.Now.TotalMilliseconds, Is.Zero);
    }
}