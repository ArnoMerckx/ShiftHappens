using System;
using NUnit.Framework;

public class DurationTimerTests
{
    private GameClock clock;
    private EventScheduler scheduler;
    private DurationTimer timer;

    [SetUp]
    public void SetUp()
    {
        clock = new GameClock(new GameTime(0, 540));
        scheduler = new EventScheduler(clock);
        timer = new DurationTimer(scheduler);
    }

    private void AdvanceMilliseconds(long milliseconds)
    {
        scheduler.AdvanceTo(new GameTime(
            scheduler.Now.TotalMilliseconds + milliseconds));
    }

    [Test]
    public void Constructor_RejectsMissingScheduler()
    {
        Assert.Throws<ArgumentNullException>(() => new DurationTimer(null));
    }

    [Test]
    public void NewTimer_IsIdleWithNoConfiguredDuration()
    {
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(timer.DurationMilliseconds, Is.Zero);
        Assert.That(timer.RemainingMilliseconds, Is.Zero);
        Assert.That(timer.TryPause(), Is.False);
        Assert.That(timer.TryResume(), Is.False);
    }

    [TestCase(1, 1000L)]
    [TestCase(2400, 2400000L)]
    [TestCase(int.MaxValue, 2147483647000L)]
    public void Start_ConvertsPositiveSecondsWithoutIntegerOverflow(
        int seconds, long expectedMilliseconds)
    {
        long before = scheduler.Now.TotalMilliseconds;
        Assert.That(timer.TryStart(seconds), Is.True);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Running));
        Assert.That(timer.DurationMilliseconds, Is.EqualTo(expectedMilliseconds));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(expectedMilliseconds));
        Assert.That(scheduler.Now.TotalMilliseconds, Is.EqualTo(before));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Start_RejectsNonpositiveDurationWithoutChangingIdleState(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.TryStart(seconds));
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(timer.DurationMilliseconds, Is.Zero);
        Assert.That(timer.RemainingMilliseconds, Is.Zero);
    }

    [Test]
    public void Remaining_TracksSharedTimeToTheMillisecond()
    {
        timer.TryStart(60);
        AdvanceMilliseconds(12345);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(47655L));
        Assert.That(timer.DurationMilliseconds, Is.EqualTo(60000L));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(47655L));
    }

    [Test]
    public void Remaining_UsesConfiguredGameClockRate()
    {
        timer.TryStart(60);
        scheduler.AdvanceRealSeconds(1);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(40000L));
        clock.SetRate(10);
        scheduler.AdvanceRealSeconds(1);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(30000L));
    }

    [TestCase(1000L)]
    [TestCase(1001L)]
    [TestCase(100000L)]
    public void Remaining_ClampsAtZeroBeforeCompletionServiceIsAttached(long elapsed)
    {
        timer.TryStart(1);
        AdvanceMilliseconds(elapsed);
        Assert.That(timer.RemainingMilliseconds, Is.Zero);
        Assert.That(timer.TryPause(), Is.False);
        // This fixture covers the model; the later service owns completion.
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Running));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Start_CannotReplaceAnActiveDuration(bool paused)
    {
        timer.TryStart(60);
        AdvanceMilliseconds(12345);
        if (paused)
            Assert.That(timer.TryPause(), Is.True);
        Assert.That(timer.TryStart(120), Is.False);
        Assert.That(timer.DurationMilliseconds, Is.EqualTo(60000L));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(47655L));
        Assert.That(timer.State, Is.EqualTo(paused
            ? DurationTimerState.Paused : DurationTimerState.Running));
    }

    [Test]
    public void InvalidStart_DoesNotDamageAnActiveTimer()
    {
        timer.TryStart(60);
        AdvanceMilliseconds(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.TryStart(0));
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Running));
        Assert.That(timer.DurationMilliseconds, Is.EqualTo(60000L));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(59999L));
    }

    [Test]
    public void IndividualPause_FreezesTimerWhileGameContinues()
    {
        timer.TryStart(60);
        AdvanceMilliseconds(12345);
        Assert.That(timer.TryPause(), Is.True);
        Assert.That(timer.TryPause(), Is.False);
        AdvanceMilliseconds(120000);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Paused));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(47655L));
        Assert.That(clock.IsPaused, Is.False);
    }

    [Test]
    public void Resume_UsesSavedRemainderFromCurrentSharedTime()
    {
        timer.TryStart(60);
        AdvanceMilliseconds(12345);
        timer.TryPause();
        AdvanceMilliseconds(120000);
        Assert.That(timer.TryResume(), Is.True);
        Assert.That(timer.TryResume(), Is.False);
        AdvanceMilliseconds(47654);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(1L));
        AdvanceMilliseconds(1);
        Assert.That(timer.RemainingMilliseconds, Is.Zero);
    }

    [Test]
    public void RepeatedPauseResume_PreservesPreciseRemainder()
    {
        timer.TryStart(1);
        for (int i = 0; i < 3; i++)
        {
            AdvanceMilliseconds(123);
            Assert.That(timer.TryPause(), Is.True);
            AdvanceMilliseconds(10000);
            Assert.That(timer.TryResume(), Is.True);
        }
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(631L));
    }

    [Test]
    public void GamePause_FreezesRunningTimerWithoutChangingItsState()
    {
        timer.TryStart(60);
        clock.SetPaused(true);
        scheduler.AdvanceRealSeconds(100);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Running));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(60000L));
        clock.SetPaused(false);
        scheduler.AdvanceRealSeconds(1);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(40000L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Reset_RestoresConfiguredDurationAndAllowsANewStart(bool paused)
    {
        timer.TryStart(60);
        AdvanceMilliseconds(10000);
        if (paused)
            timer.TryPause();
        timer.Reset();
        AdvanceMilliseconds(120000);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(timer.DurationMilliseconds, Is.EqualTo(60000L));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(60000L));
        Assert.That(timer.TryResume(), Is.False);
        Assert.That(timer.TryStart(30), Is.True);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(30000L));
    }

    [Test]
    public void Reset_BeforeFirstStartIsHarmless()
    {
        timer.Reset();
        timer.Reset();
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(timer.DurationMilliseconds, Is.Zero);
        Assert.That(timer.RemainingMilliseconds, Is.Zero);
    }

    [Test]
    public void StoppedScheduler_RejectsStartAndResumeWithoutMutation()
    {
        var idle = new DurationTimer(scheduler);
        timer.TryStart(60);
        AdvanceMilliseconds(12345);
        timer.TryPause();
        scheduler.Stop();
        Assert.That(idle.TryStart(60), Is.False);
        Assert.That(idle.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(idle.DurationMilliseconds, Is.Zero);
        Assert.That(timer.TryResume(), Is.False);
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Paused));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(47655L));
    }

    [Test]
    public void Start_BeyondRepresentableTimeLeavesTimerUnconfigured()
    {
        long maximum = ((long)int.MaxValue + 1)
            * GameTime.MillisecondsPerDay - 1;
        scheduler.AdvanceTo(new GameTime(maximum - 500));
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.TryStart(1));
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Idle));
        Assert.That(timer.DurationMilliseconds, Is.Zero);
        Assert.That(timer.RemainingMilliseconds, Is.Zero);
    }

    [Test]
    public void Resume_BeyondRepresentableTimePreservesPausedRemainder()
    {
        timer.TryStart(60);
        AdvanceMilliseconds(10000);
        timer.TryPause();
        long maximum = ((long)int.MaxValue + 1)
            * GameTime.MillisecondsPerDay - 1;
        scheduler.AdvanceTo(new GameTime(maximum - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.TryResume());
        Assert.That(timer.State, Is.EqualTo(DurationTimerState.Paused));
        Assert.That(timer.DurationMilliseconds, Is.EqualTo(60000L));
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(50000L));
    }

    [Test]
    public void TimersSharingScheduler_HaveIndependentDurationState()
    {
        var other = new DurationTimer(scheduler);
        timer.TryStart(60);
        other.TryStart(120);
        AdvanceMilliseconds(10000);
        timer.TryPause();
        AdvanceMilliseconds(20000);
        Assert.That(timer.RemainingMilliseconds, Is.EqualTo(50000L));
        Assert.That(other.RemainingMilliseconds, Is.EqualTo(90000L));
        timer.Reset();
        Assert.That(other.State, Is.EqualTo(DurationTimerState.Running));
        Assert.That(other.RemainingMilliseconds, Is.EqualTo(90000L));
    }
}
