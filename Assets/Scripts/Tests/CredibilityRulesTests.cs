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
}
