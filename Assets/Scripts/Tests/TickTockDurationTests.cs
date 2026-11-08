using System;
using System.Globalization;
using NUnit.Framework;

public class TickTockDurationTests
{
    [TestCase("0", "1", 1)]
    [TestCase("0", "59", 59)]
    [TestCase("1", "0", 60)]
    [TestCase("40", "00", 2400)]
    [TestCase("60", "0", 3600)]
    [TestCase(" 40 ", " 05 ", 2405)]
    [TestCase("0001", "09", 69)]
    [TestCase("35791394", "7", int.MaxValue)]
    public void ValidFields_ReturnPositiveSecondsAndNoError(
        string minutes, string seconds, int expected)
    {
        bool accepted = TickTockDuration.TryParseDuration(
            minutes, seconds, out int duration, out string error);

        Assert.That(accepted, Is.True, error);
        Assert.That(duration, Is.EqualTo(expected));
        Assert.That(error, Is.Empty);
    }

    [TestCase(null, "0")]
    [TestCase("", "1")]
    [TestCase(" ", "1")]
    [TestCase("1", null)]
    [TestCase("1", "")]
    [TestCase("1", " ")]
    [TestCase("0", "0")]
    [TestCase("-1", "0")]
    [TestCase("1", "-1")]
    [TestCase("+1", "0")]
    [TestCase("1", "+1")]
    [TestCase("1.5", "0")]
    [TestCase("1,5", "0")]
    [TestCase("1", "0.5")]
    [TestCase("1", "60")]
    [TestCase("1", "2147483648")]
    [TestCase("1:30", "0")]
    [TestCase("11:55", "0")]
    [TestCase("1e2", "0")]
    [TestCase("1 0", "0")]
    [TestCase("word", "0")]
    [TestCase("1", "word")]
    [TestCase("35791394", "8")]
    [TestCase("35791395", "0")]
    [TestCase("2147483647", "59")]
    [TestCase("2147483648", "0")]
    public void InvalidFields_ReturnZeroAndReadableFeedback(
        string minutes, string seconds)
    {
        bool accepted = TickTockDuration.TryParseDuration(
            minutes, seconds, out int duration, out string error);

        Assert.That(accepted, Is.False);
        Assert.That(duration, Is.Zero);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [TestCase(0L, "00:00")]
    [TestCase(1L, "00:01")]
    [TestCase(999L, "00:01")]
    [TestCase(1000L, "00:01")]
    [TestCase(1001L, "00:02")]
    [TestCase(59999L, "01:00")]
    [TestCase(60000L, "01:00")]
    [TestCase(60001L, "01:01")]
    [TestCase(2400000L, "40:00")]
    [TestCase(3600000L, "60:00")]
    [TestCase(86400000L, "1440:00")]
    [TestCase(2147483647000L, "35791394:07")]
    [TestCase(long.MaxValue, "153722867280912:56")]
    public void RemainingDisplay_RoundsUpWithoutHourDayOrIntegerWrap(
        long milliseconds, string expected)
    {
        Assert.That(TickTockDuration.FormatRemaining(milliseconds),
            Is.EqualTo(expected));
    }

    [TestCase(-1L)]
    [TestCase(-1000L)]
    [TestCase(long.MinValue)]
    public void NegativeRemainingDuration_IsRejected(long milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TickTockDuration.FormatRemaining(milliseconds));
    }

    [TestCase("fr-BE")]
    [TestCase("en-US")]
    public void ParsingAndDisplay_DoNotDependOnCurrentCulture(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            Assert.That(TickTockDuration.TryParseDuration(
                "40", "05", out int duration, out string error), Is.True);
            Assert.That(duration, Is.EqualTo(2405));
            Assert.That(error, Is.Empty);
            Assert.That(TickTockDuration.FormatRemaining(2405000),
                Is.EqualTo("40:05"));
            Assert.That(TickTockDuration.TryParseDuration(
                "1,5", "0", out _, out _), Is.False);
            Assert.That(TickTockDuration.TryParseDuration(
                "1.5", "0", out _, out _), Is.False);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
