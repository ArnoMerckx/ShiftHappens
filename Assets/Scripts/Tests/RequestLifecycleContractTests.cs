using System;
using System.Collections.Generic;
using NUnit.Framework;

public class RequestLifecycleContractTests
{
    private static readonly GameTime Closing = new GameTime(3, 1020);

    private static DayRequestPlan Plan(bool fixedWindow = true, string instanceId = "D3R1",
        GameTime? cutoff = null)
    {
        var request = new RequestData("pc_help", "PC help", "Arrange desktop help.",
            "alice", EmployeeCapability.ComputerSupport);
        var profile = new RequestProfileData("desktop", EmployeeCapability.ComputerSupport,
            new List<RequestSuitabilityEntry> { new RequestSuitabilityEntry("steve", 0) },
            new List<string> { "alice" });
        return new DayRequestPlan(instanceId, request, profile, new GameTime(3, 595),
            fixedWindow ? (TimeRange?)new TimeRange(3, 630, 645) : null, cutoff);
    }

    private static DelegationDecision Decision(GameTime at,
        string instanceId = "D3R1", DelegationOutcome outcome = DelegationOutcome.BestAvailable)
    {
        return new DelegationDecision(instanceId, "steve", at, outcome,
            "Steve can provide the arranged help.");
    }

    [TestCase("request", null)]
    [TestCase("request", "")]
    [TestCase("request", " ")]
    [TestCase("helper", null)]
    [TestCase("helper", "")]
    [TestCase("helper", " ")]
    [TestCase("explanation", null)]
    [TestCase("explanation", "")]
    [TestCase("explanation", " ")]
    public void Decision_RejectsMissingIdentityOrExplanation(string field, string invalid)
    {
        Assert.Throws<ArgumentException>(() => new DelegationDecision(
            field == "request" ? invalid : "D3R1",
            field == "helper" ? invalid : "steve", new GameTime(3, 600),
            DelegationOutcome.BestAvailable,
            field == "explanation" ? invalid : "Help arranged."));
    }

    [Test]
    public void Decision_RejectsUndefinedOutcome()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Decision(new GameTime(3, 600), outcome: (DelegationOutcome)999));
    }

    [Test]
    public void Instance_RejectsMissingPlan()
    {
        Assert.Throws<ArgumentNullException>(() => new RequestInstance(null, Closing));
    }

    [TestCase(2, 1020)]
    [TestCase(4, 1020)]
    [TestCase(3, 594)]
    [TestCase(3, 595)]
    public void Closing_MustFollowDeliveryOnSameDay(int day, int minute)
    {
        Assert.Throws<ArgumentException>(() =>
            new RequestInstance(Plan(false), new GameTime(day, minute)));
    }

    [TestCase(630)]
    [TestCase(644)]
    public void FixedWindow_RejectsCutoffAtClosingOrWindowBeyondClosing(int minute)
    {
        Assert.Throws<ArgumentException>(() =>
            new RequestInstance(Plan(), new GameTime(3, minute)));
    }

    [Test]
    public void WindowMayEndExactlyAtClosing()
    {
        var instance = new RequestInstance(Plan(), new GameTime(3, 645));
        Assert.That(instance.ExpiresAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(3, 630).TotalMilliseconds + 1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FixedCutoff_AllowsExactEqualityAndExpiresOneMillisecondLater(bool earlierCutoff)
    {
        var cutoff = earlierCutoff ? new GameTime(3, 620, 12, 345) : new GameTime(3, 630);
        var plan = Plan(cutoff: earlierCutoff ? (GameTime?)cutoff : null);
        var resolved = new RequestInstance(plan, Closing);
        var expired = new RequestInstance(plan, Closing);
        var late = new GameTime(cutoff.TotalMilliseconds + 1);

        Assert.That(resolved.TryExpire(cutoff), Is.False);
        Assert.That(resolved.Status, Is.EqualTo(RequestStatus.Open));
        var decision = Decision(cutoff);
        Assert.That(resolved.TryResolve(decision), Is.True);
        Assert.That(resolved.Decision, Is.SameAs(decision));

        Assert.That(expired.ExpiresAt.TotalMilliseconds, Is.EqualTo(late.TotalMilliseconds));
        Assert.That(expired.TryResolve(Decision(late)), Is.False);
        Assert.That(expired.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(expired.Decision, Is.Null);
        Assert.That(expired.TryExpire(late), Is.True);
        Assert.That(expired.Status, Is.EqualTo(RequestStatus.Expired));
    }

    [Test]
    public void EndOfShift_AllowsLastMillisecondButRejectsClosingAndExpiresAtClosing()
    {
        var plan = Plan(false);
        var lastInstant = new GameTime(Closing.TotalMilliseconds - 1);
        var resolved = new RequestInstance(plan, Closing);
        var expired = new RequestInstance(plan, Closing);

        Assert.That(resolved.TryExpire(lastInstant), Is.False);
        Assert.That(resolved.TryResolve(Decision(lastInstant)), Is.True);
        Assert.That(resolved.Decision.SubmittedAt.TotalMilliseconds,
            Is.EqualTo(lastInstant.TotalMilliseconds));
        Assert.That(expired.ExpiresAt.TotalMilliseconds, Is.EqualTo(Closing.TotalMilliseconds));
        Assert.That(expired.TryResolve(Decision(Closing)), Is.False);
        Assert.That(expired.Decision, Is.Null);
        Assert.That(expired.TryExpire(Closing), Is.True);
    }

    [Test]
    public void BeforeDeliveryIsRejected_ExactDeliveryIsAllowed()
    {
        var plan = Plan();
        var instance = new RequestInstance(plan, Closing);
        Assert.That(instance.TryResolve(Decision(
            new GameTime(plan.DeliveryAt.TotalMilliseconds - 1))), Is.False);
        Assert.That(instance.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(instance.Decision, Is.Null);
        Assert.That(instance.TryResolve(Decision(plan.DeliveryAt)), Is.True);
    }

    [TestCase("other")]
    [TestCase("d3r1")]
    public void WrongOrCaseMismatchedInstanceId_DoesNotConsumeRequest(string id)
    {
        var instance = new RequestInstance(Plan(), Closing);
        Assert.That(instance.TryResolve(Decision(new GameTime(3, 600), id)), Is.False);
        Assert.That(instance.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(instance.Decision, Is.Null);
        Assert.That(instance.TryResolve(Decision(new GameTime(3, 600))), Is.True);
    }

    [Test]
    public void NullDecision_ThrowsWithoutChangingState()
    {
        var instance = new RequestInstance(Plan(), Closing);
        Assert.Throws<ArgumentNullException>(() => instance.TryResolve(null));
        Assert.That(instance.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(instance.Decision, Is.Null);
    }

    [TestCase(DelegationOutcome.BestAvailable)]
    [TestCase(DelegationOutcome.CapableAlternative)]
    [TestCase(DelegationOutcome.Unsuitable)]
    [TestCase(DelegationOutcome.Unavailable)]
    public void EveryEvaluatedOutcome_ResolvesOnceAndPreservesFirstDecision(DelegationOutcome outcome)
    {
        var instance = new RequestInstance(Plan(), Closing);
        var first = Decision(new GameTime(3, 600, 12, 345), outcome: outcome);
        var replacement = new DelegationDecision("D3R1", "alice", new GameTime(3, 610),
            DelegationOutcome.Unsuitable, "This helper is unsuitable.");

        Assert.That(instance.TryResolve(first), Is.True);
        Assert.That(instance.Status, Is.EqualTo(RequestStatus.Resolved));
        Assert.That(instance.TryResolve(first), Is.False);
        Assert.That(instance.TryResolve(replacement), Is.False);
        Assert.That(instance.TryExpire(Closing), Is.False);
        Assert.That(instance.Decision, Is.SameAs(first));
        Assert.That(instance.Decision.SubmittedAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(3, 600, 12, 345).TotalMilliseconds));
        Assert.That(instance.Status, Is.EqualTo(RequestStatus.Resolved));
    }

    [Test]
    public void ExpiryAfterLargeJump_IsTerminalAndCannotBeRepeatedOrBackdatedAway()
    {
        var instance = new RequestInstance(Plan(false), Closing);
        Assert.That(instance.TryExpire(new GameTime(5, 600)), Is.True);
        Assert.That(instance.TryExpire(new GameTime(5, 601)), Is.False);
        Assert.That(instance.TryResolve(Decision(new GameTime(3, 600))), Is.False);
        Assert.That(instance.Status, Is.EqualTo(RequestStatus.Expired));
        Assert.That(instance.Decision, Is.Null);
    }

    [Test]
    public void SeparateRuntimeInstances_DoNotShareStateOrConfuseTemplateWithInstanceId()
    {
        var plan = Plan();
        var first = new RequestInstance(plan, Closing);
        var second = new RequestInstance(plan, Closing);
        var anotherPlan = new DayRequestPlan("D3R2", plan.Request, plan.Profile,
            plan.DeliveryAt, plan.HelpWindow, plan.CutoffAt);
        var repeatedTemplate = new RequestInstance(anotherPlan, Closing);

        Assert.That(first.TryResolve(Decision(new GameTime(3, 600))), Is.True);
        Assert.That(second.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(second.Decision, Is.Null);
        Assert.That(repeatedTemplate.TryResolve(first.Decision), Is.False);
        Assert.That(repeatedTemplate.Status, Is.EqualTo(RequestStatus.Open));
        Assert.That(repeatedTemplate.TryResolve(Decision(new GameTime(3, 600), "D3R2")), Is.True);
        Assert.That(second.TryExpire(Closing), Is.True);
        Assert.That(first.Status, Is.EqualTo(RequestStatus.Resolved));
        Assert.That(repeatedTemplate.Status, Is.EqualTo(RequestStatus.Resolved));
    }
}
