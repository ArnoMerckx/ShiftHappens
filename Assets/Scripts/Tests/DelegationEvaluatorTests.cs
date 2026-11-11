using System;
using System.Collections.Generic;
using NUnit.Framework;

public class DelegationEvaluatorTests
{
    private static EmployeeAvailability Available(string id, int start = 540, int end = 1020)
    {
        return new EmployeeAvailability(id, new TimeRange(3, start, end));
    }

    private static ApprovedScheduleRecord Block(string id, string employee, int start, int end)
    {
        return new ApprovedScheduleRecord(id, employee, "offsite",
            new TimeRange(3, start, end), 100, false, true);
    }

    private static DayScheduleData Day(List<ApprovedScheduleRecord> records = null,
        List<EmployeeAvailability> availability = null)
    {
        return new DayScheduleData(
            new WorkdaySettings("evaluation", "Evaluation", 3, 540, 1020, 40),
            new List<string> { "alice", "bob", "dave", "steve" },
            records ?? new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>(),
            new List<EmployeeViewData>
            {
                new EmployeeViewData("alice", "Alice"), new EmployeeViewData("bob", "Bob"),
                new EmployeeViewData("dave", "Dave"), new EmployeeViewData("steve", "Steve")
            }, availability ?? new List<EmployeeAvailability>
            {
                Available("alice"), Available("bob"), Available("dave"), Available("steve")
            });
    }

    private static DayRequestPlan Plan(bool fixedWindow = true, bool allowSelfHelp = false,
        List<RequestSuitabilityEntry> ranked = null, GameTime? cutoff = null,
        string instanceId = "D3R1")
    {
        var tiers = ranked ?? new List<RequestSuitabilityEntry>
        {
            new RequestSuitabilityEntry("dave", 0), new RequestSuitabilityEntry("steve", 1)
        };
        var unsuitable = new List<string> { "alice", "bob", "dave", "steve" };
        foreach (var entry in tiers) unsuitable.Remove(entry.EmployeeId);
        return new DayRequestPlan(instanceId,
            new RequestData("pc_help", "PC help", "Arrange desktop help.", "alice",
                EmployeeCapability.ComputerSupport, allowSelfHelp),
            new RequestProfileData("private_desktop_profile", EmployeeCapability.ComputerSupport,
                tiers, unsuitable), new GameTime(3, 595),
            fixedWindow ? (TimeRange?)new TimeRange(3, 660, 720) : null, cutoff);
    }

    [Test]
    public void D01_DaveAbsentForWholeWindow_SteveBecomesBestAvailable()
    {
        var day = Day(new List<ApprovedScheduleRecord> { Block("doctor", "dave", 600, 780) });
        Assert.That(DelegationEvaluator.Evaluate(Plan(), "dave", new GameTime(3, 610), day),
            Is.EqualTo(DelegationOutcome.Unavailable));
        Assert.That(DelegationEvaluator.Evaluate(Plan(), "steve", new GameTime(3, 610), day),
            Is.EqualTo(DelegationOutcome.BestAvailable));
    }

    [Test]
    public void D02_UnavailableUnsuitableEmployee_IsUnavailableBeforeSuitability()
    {
        var day = Day(new List<ApprovedScheduleRecord> { Block("dentist", "bob", 600, 780) });
        Assert.That(DelegationEvaluator.Evaluate(Plan(), "bob", new GameTime(3, 610), day),
            Is.EqualTo(DelegationOutcome.Unavailable));
        Assert.That(DelegationEvaluator.Evaluate(Plan(), "bob", new GameTime(3, 610), Day()),
            Is.EqualTo(DelegationOutcome.Unsuitable));
    }

    [Test]
    public void D03_DaveAvailable_IsBestAndSteveIsCapableAlternative()
    {
        var plan = Plan();
        var day = Day();
        Assert.That(DelegationEvaluator.Evaluate(plan, "dave", new GameTime(3, 610), day),
            Is.EqualTo(DelegationOutcome.BestAvailable));
        Assert.That(DelegationEvaluator.Evaluate(plan, "steve", new GameTime(3, 610), day),
            Is.EqualTo(DelegationOutcome.CapableAlternative));
    }

    [TestCase(690, 700, DelegationOutcome.BestAvailable)]
    [TestCase(600, 660, DelegationOutcome.CapableAlternative)]
    [TestCase(720, 780, DelegationOutcome.CapableAlternative)]
    public void D04_BetterCandidateMustCoverEntireWindow_WithHalfOpenBoundaries(
        int start, int end, DelegationOutcome expectedSteveOutcome)
    {
        var day = Day(new List<ApprovedScheduleRecord> { Block("commitment", "dave", start, end) });
        Assert.That(DelegationEvaluator.Evaluate(Plan(), "steve", new GameTime(3, 610), day),
            Is.EqualTo(expectedSteveOutcome));
    }

    [Test]
    public void D05_EndOfShift_UsesOnlyCoverageAtOrAfterPreciseSubmission()
    {
        var day = Day(availability: new List<EmployeeAvailability>
        {
            Available("dave", 800, 900), Available("steve", 950, 1000)
        });
        var plan = Plan(false);
        Assert.That(DelegationEvaluator.Evaluate(plan, "steve", new GameTime(3, 910), day),
            Is.EqualTo(DelegationOutcome.BestAvailable));
        Assert.That(DelegationEvaluator.Evaluate(plan, "dave", new GameTime(3, 910), day),
            Is.EqualTo(DelegationOutcome.Unavailable));
        Assert.That(DelegationEvaluator.Evaluate(plan, "steve", new GameTime(3, 999, 59, 999), day),
            Is.EqualTo(DelegationOutcome.BestAvailable));
        Assert.That(DelegationEvaluator.Evaluate(plan, "steve", new GameTime(3, 1000), day),
            Is.EqualTo(DelegationOutcome.Unavailable));
    }

    [Test]
    public void D06_FixedQualityIsIndependentOfValidSubmissionTime_EndOfShiftCanChange()
    {
        var day = Day(availability: new List<EmployeeAvailability>
        {
            Available("dave", 540, 620), Available("steve", 540, 1020)
        });
        var fixedPlan = Plan();
        foreach (var at in new[] { new GameTime(3, 600), new GameTime(3, 630) })
            Assert.That(DelegationEvaluator.Evaluate(fixedPlan, "steve", at, day),
                Is.EqualTo(DelegationOutcome.BestAvailable));
        var endPlan = Plan(false);
        Assert.That(DelegationEvaluator.Evaluate(endPlan, "steve", new GameTime(3, 619, 59, 999), day),
            Is.EqualTo(DelegationOutcome.CapableAlternative));
        Assert.That(DelegationEvaluator.Evaluate(endPlan, "steve", new GameTime(3, 620), day),
            Is.EqualTo(DelegationOutcome.BestAvailable));
    }

    [Test]
    public void D09_AllCapableUnavailable_DoesNotPromoteAvailableUnsuitableEmployee()
    {
        var day = Day(new List<ApprovedScheduleRecord>
        {
            Block("dave_busy", "dave", 660, 720), Block("steve_busy", "steve", 660, 720)
        });
        Assert.That(DelegationEvaluator.Evaluate(Plan(), "bob", new GameTime(3, 610), day),
            Is.EqualTo(DelegationOutcome.Unsuitable));
        Assert.That(DelegationEvaluator.Evaluate(Plan(ranked: new List<RequestSuitabilityEntry>()),
            "bob", new GameTime(3, 610), Day()), Is.EqualTo(DelegationOutcome.Unsuitable));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void D10_TiedTiersProduceSameOutcomeRegardlessOfProfileOrder(bool reverse)
    {
        var tiers = new List<RequestSuitabilityEntry>
        {
            new RequestSuitabilityEntry("steve", 7), new RequestSuitabilityEntry("dave", 7)
        };
        if (reverse) tiers.Reverse();
        var plan = Plan(ranked: tiers);
        foreach (string id in new[] { "dave", "steve" })
            Assert.That(DelegationEvaluator.Evaluate(plan, id, new GameTime(3, 610), Day()),
                Is.EqualTo(DelegationOutcome.BestAvailable));
    }

    [TestCase(false, DelegationOutcome.BestAvailable)]
    [TestCase(true, DelegationOutcome.CapableAlternative)]
    public void SelfHelpPolicy_AlsoControlsWhichBetterCandidatesAreEligible(
        bool allowSelfHelp, DelegationOutcome expectedSteveOutcome)
    {
        var plan = Plan(allowSelfHelp: allowSelfHelp, ranked: new List<RequestSuitabilityEntry>
        {
            new RequestSuitabilityEntry("alice", 0), new RequestSuitabilityEntry("steve", 1)
        });
        Assert.That(DelegationEvaluator.Evaluate(plan, "steve", new GameTime(3, 610), Day()),
            Is.EqualTo(expectedSteveOutcome));
        if (allowSelfHelp)
            Assert.That(DelegationEvaluator.Evaluate(plan, "alice", new GameTime(3, 610), Day()),
                Is.EqualTo(DelegationOutcome.BestAvailable));
        else
            Assert.Throws<ArgumentException>(() =>
                DelegationEvaluator.Evaluate(plan, "alice", new GameTime(3, 610), Day()));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("unknown")]
    [TestCase("Steve")]
    public void UnknownOrInvalidHelper_IsRejectedInsteadOfReceivingOutcome(string id)
    {
        Assert.Throws<ArgumentException>(() =>
            DelegationEvaluator.EvaluateDecision(Plan(), id, new GameTime(3, 610), Day()));
    }

    [Test]
    public void MissingInputs_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DelegationEvaluator.EvaluateDecision(null, "steve", new GameTime(3, 610), Day()));
        Assert.Throws<ArgumentNullException>(() =>
            DelegationEvaluator.EvaluateDecision(Plan(), "steve", new GameTime(3, 610), null));
    }

    [Test]
    public void Evaluation_RespectsPreciseDeliveryCutoffAndClosing()
    {
        var day = Day();
        var cutoff = new GameTime(3, 650, 12, 345);
        var plan = Plan(cutoff: cutoff);
        Assert.DoesNotThrow(() => DelegationEvaluator.Evaluate(plan, "steve", plan.DeliveryAt, day));
        Assert.DoesNotThrow(() => DelegationEvaluator.Evaluate(plan, "steve", cutoff, day));
        foreach (var invalid in new[]
        {
            new GameTime(plan.DeliveryAt.TotalMilliseconds - 1),
            new GameTime(cutoff.TotalMilliseconds + 1), new GameTime(4, 610)
        })
            Assert.Throws<ArgumentException>(() => DelegationEvaluator.Evaluate(plan, "steve", invalid, day));
        var endPlan = Plan(false);
        Assert.DoesNotThrow(() => DelegationEvaluator.Evaluate(endPlan, "steve",
            new GameTime(day.Settings.ClosesAt.TotalMilliseconds - 1), day));
        Assert.Throws<ArgumentException>(() => DelegationEvaluator.Evaluate(endPlan, "steve", day.Settings.ClosesAt, day));
    }

    [Test]
    public void DecisionFeedback_UsesChosenDisplayNameAndDoesNotRevealComparativeRanking()
    {
        var plan = Plan(instanceId: "D3R2");
        var at = new GameTime(3, 610, 12, 345);
        var alternative = DelegationEvaluator.EvaluateDecision(plan, "steve", at, Day());
        var best = DelegationEvaluator.EvaluateDecision(plan, "steve", at,
            Day(new List<ApprovedScheduleRecord> { Block("doctor", "dave", 600, 780) }));
        Assert.That(alternative.Outcome, Is.EqualTo(DelegationOutcome.CapableAlternative));
        Assert.That(best.Outcome, Is.EqualTo(DelegationOutcome.BestAvailable));
        Assert.That(best.Explanation, Is.EqualTo(alternative.Explanation));
        Assert.That(best.Explanation, Does.Contain("Steve"));
        Assert.That(best.Explanation.ToLowerInvariant(), Does.Contain("arranged"));
        foreach (var forbidden in new[] { "dave", "alice", "bob", "tier", "best", "alternative", "private_desktop_profile", "completed" })
            Assert.That(best.Explanation.ToLowerInvariant(), Does.Not.Contain(forbidden));
        Assert.That(best.RequestInstanceId, Is.EqualTo("D3R2"));
        Assert.That(best.HelperId, Is.EqualTo("steve"));
        Assert.That(best.SubmittedAt.TotalMilliseconds, Is.EqualTo(at.TotalMilliseconds));
    }

    [Test]
    public void BadChoiceFeedback_ExplainsChosenHelperAndTimingMode()
    {
        var at = new GameTime(3, 610);
        var unavailable = Day(availability: new List<EmployeeAvailability>());
        var fixedResult = DelegationEvaluator.EvaluateDecision(Plan(), "steve", at, unavailable);
        var endResult = DelegationEvaluator.EvaluateDecision(Plan(false), "steve", at, unavailable);
        var unsuitable = DelegationEvaluator.EvaluateDecision(Plan(), "bob", at, Day());
        Assert.That(fixedResult.Outcome, Is.EqualTo(DelegationOutcome.Unavailable));
        Assert.That(fixedResult.Explanation.ToLowerInvariant(), Does.Contain("full").And.Contain("window"));
        Assert.That(endResult.Outcome, Is.EqualTo(DelegationOutcome.Unavailable));
        Assert.That(endResult.Explanation.ToLowerInvariant(), Does.Contain("submission").And.Contain("shift end"));
        Assert.That(unsuitable.Outcome, Is.EqualTo(DelegationOutcome.Unsuitable));
        Assert.That(unsuitable.Explanation, Does.Contain("Bob"));
        Assert.That(unsuitable.Explanation.ToLowerInvariant(), Does.Contain("not suited"));
    }

    [Test]
    public void RepeatedEvaluation_IsDeterministicAndDoesNotChangeRuntimeStateOrRosterOrder()
    {
        var plan = Plan();
        var day = Day();
        var instance = new RequestInstance(plan, day.Settings.ClosesAt);
        var beforeRoster = new List<string>();
        foreach (var employee in day.EmployeeRoster) beforeRoster.Add(employee.EmployeeId);
        var first = DelegationEvaluator.EvaluateDecision(plan, "steve", new GameTime(3, 610), day);
        var second = DelegationEvaluator.EvaluateDecision(plan, "steve", new GameTime(3, 610), day);
        Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
        Assert.That(second.Explanation, Is.EqualTo(first.Explanation));
        Assert.That(instance.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(instance.Decision, Is.Null);
        var afterRoster = new List<string>();
        foreach (var employee in day.EmployeeRoster) afterRoster.Add(employee.EmployeeId);
        CollectionAssert.AreEqual(beforeRoster, afterRoster);
        Assert.That(plan.Profile.RankedEmployees[0].EmployeeId, Is.EqualTo("dave"));
        Assert.That(plan.Profile.RankedEmployees[1].EmployeeId, Is.EqualTo("steve"));
        Assert.That(day.HrKnowledge.ApprovedRecords, Is.Empty);
    }
}
