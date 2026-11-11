using System;
using NUnit.Framework;

public class CredibilityRulesTests
{
    [Test]
    public void Defaults_MatchChosenStartingValues()
    {
        var rules = new CredibilityRules();
        Assert.That(rules.StartingCredibility, Is.EqualTo(100));
        Assert.That(rules.RejectedReportPenalty, Is.EqualTo(25));
        Assert.That(rules.MissedDeadlinePenalty, Is.EqualTo(35));
        Assert.That(rules.FiringThreshold, Is.Zero);
    }

    [Test]
    public void CustomValues_ArePreserved()
    {
        var rules = new CredibilityRules(80, 12, 17, 10);
        Assert.That(rules.StartingCredibility, Is.EqualTo(80));
        Assert.That(rules.RejectedReportPenalty, Is.EqualTo(12));
        Assert.That(rules.MissedDeadlinePenalty, Is.EqualTo(17));
        Assert.That(rules.FiringThreshold, Is.EqualTo(10));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonpositiveStartingCredibility_IsRejected(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CredibilityRules(startingCredibility: value));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NegativePenalty_IsRejected(bool rejectedReportPenalty)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CredibilityRules(
            rejectedReportPenalty: rejectedReportPenalty ? -1 : 25,
            missedDeadlinePenalty: rejectedReportPenalty ? 35 : -1));
    }

    [TestCase(-1)]
    [TestCase(100)]
    [TestCase(101)]
    public void ThresholdMustBeNonnegativeAndBelowStartingScore(int threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CredibilityRules(firingThreshold: threshold));
    }

    [Test]
    public void ZeroPenalties_AreSupported()
    {
        var rules = new CredibilityRules(rejectedReportPenalty: 0, missedDeadlinePenalty: 0);
        Assert.That(rules.RejectedReportPenalty, Is.Zero);
        Assert.That(rules.MissedDeadlinePenalty, Is.Zero);
    }

    [Test]
    public void Maximum_DefaultsTo100AndCanBeConfigured()
    {
        Assert.That(new CredibilityRules().MaximumCredibility, Is.EqualTo(100));
        var rules = new CredibilityRules(120, 25, 35, 0, 150);
        Assert.That(rules.MaximumCredibility, Is.EqualTo(150));
        Assert.That(rules.StartingCredibility, Is.EqualTo(120));
        Assert.That(rules.ApplyDelta(145, 10), Is.EqualTo(150));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonpositiveMaximum_IsRejected(int maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CredibilityRules(maximumCredibility: maximum));
    }

    [Test]
    public void StartingAboveMaximum_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CredibilityRules(startingCredibility: 101));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CredibilityRules(startingCredibility: 80, maximumCredibility: 79));
    }

    [TestCase(98, 5, 100)]
    [TestCase(100, 5, 100)]
    [TestCase(10, -35, 0)]
    [TestCase(0, -1, 0)]
    [TestCase(0, 5, 5)]
    [TestCase(80, 2, 82)]
    [TestCase(80, -25, 55)]
    [TestCase(80, 0, 80)]
    [TestCase(100, int.MaxValue, 100)]
    [TestCase(100, int.MinValue, 0)]
    public void SignedDelta_ClampsWithoutOverflow(int current, int delta, int expected)
    {
        Assert.That(new CredibilityRules().ApplyDelta(current, delta), Is.EqualTo(expected));
    }

    [TestCase(-1)]
    [TestCase(101)]
    public void InvalidCurrent_IsRejectedInsteadOfSilentlyRepaired(int current)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CredibilityRules().ApplyDelta(current, 0));
    }

    [Test]
    public void SignedDelta_HandlesLargestConfiguredMaximum()
    {
        var rules = new CredibilityRules(maximumCredibility: int.MaxValue);
        Assert.That(rules.ApplyDelta(int.MaxValue, int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(rules.ApplyDelta(int.MaxValue, int.MinValue), Is.Zero);
    }
}
