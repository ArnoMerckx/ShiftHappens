using System;
using System.Collections.Generic;
using NUnit.Framework;

public class SwarmsCoworkerReactionTests
{
    [Test]
    public void Rules_ValidateHiddenThresholds()
    {
        var rules = new CoworkerReactionRules();
        Assert.That(rules.LowTrustThreshold, Is.EqualTo(40));
        Assert.That(rules.RepeatedAssignmentCount, Is.EqualTo(2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoworkerReactionRules(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CoworkerReactionRules(40, 1));
    }

    [Test]
    public void LowHiddenTrust_TakesPriorityOverThanksWithoutExposingValue()
    {
        var context = new ConsequenceTestContext(trust: 35);
        WorkdaySession session = context.Session;
        context.Advance(595);
        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        Assert.That(session.Trust.GetTrust("dave"), Is.EqualTo(40));
        context.Advance(645);

        SwarmsReplyData reaction = FindReaction(session, "R1", "dave");
        Assert.That(reaction.RuleId, Is.EqualTo("coworker-low-trust-v1"));
        Assert.That(reaction.BodyText, Does.Contain("uneasy"));
        Assert.That(reaction.BodyText, Does.Not.Contain("40"));
        Assert.That(reaction.BodyText.ToLowerInvariant(), Does.Not.Contain("trust"));
        Assert.That(reaction.BodyText.ToLowerInvariant(), Does.Not.Contain("credibility"));
        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(2));
    }

    [Test]
    public void SecondCurrentDayAssignment_UsesOneWorkloadReactionForThatSource()
    {
        var context = new ConsequenceTestContext(
            ConsequenceTestContext.CreateDay(requestCount: 2));
        WorkdaySession session = context.Session;
        context.Advance(595);
        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        Assert.That(session.TryDelegate("R2", "dave").IsAccepted, Is.True);
        Assert.That(session.Workload.GetAttemptedCount("dave"), Is.EqualTo(2));
        context.Advance(645);

        SwarmsReplyData first = FindReaction(session, "R1", "dave");
        SwarmsReplyData second = FindReaction(session, "R2", "dave");
        Assert.That(first.RuleId, Is.EqualTo("coworker-assignment-thanks-v1"));
        Assert.That(second.RuleId, Is.EqualTo("coworker-repeated-assignment-v1"));
        Assert.That(second.BodyText, Does.Contain("several requests today"));
        Assert.That(first.ReplyId, Is.Not.EqualTo(second.ReplyId));
        Assert.That(CountReactions(session, "R1"), Is.EqualTo(1));
        Assert.That(CountReactions(session, "R2"), Is.EqualTo(1));
        Assert.That(session.SwarmsReplies.DeliveredReplies.Count, Is.EqualTo(4));
        Assert.That(session.Consequences.History.Count, Is.EqualTo(2));
        Assert.That(session.Workload.History.Count, Is.EqualTo(2));
    }

    [Test]
    public void PriorDayAssignments_DoNotTriggerCurrentDayWorkloadReaction()
    {
        DayData day = ConsequenceTestContext.CreateDay();
        var trusts = new List<EmployeeTrustEntry>();
        foreach (string id in day.ScheduleData.EmployeeIds)
            trusts.Add(new EmployeeTrustEntry(id, 50));
        var priorDecision = new DelegationDecision(
            "OLD", "dave", new GameTime(2, 600),
            DelegationOutcome.BestAvailable, "Prior arranged help.");
        var opening = new WorkdayOpeningState(
            80, trusts,
            new List<AssignmentHistoryEntry> {
                new AssignmentHistoryEntry("prior_day", priorDecision) });
        var context = new ConsequenceTestContext(day, opening: opening);
        WorkdaySession session = context.Session;
        context.Advance(595);
        Assert.That(session.TryDelegate("R1", "dave").IsAccepted, Is.True);
        Assert.That(session.Workload.GetAttemptedCount("dave"), Is.EqualTo(2));
        context.Advance(645);

        SwarmsReplyData reaction = FindReaction(session, "R1", "dave");
        Assert.That(reaction.RuleId, Is.EqualTo("coworker-assignment-thanks-v1"));
        Assert.That(reaction.BodyText, Does.Not.Contain("several"));
    }

    private static SwarmsReplyData FindReaction(
        WorkdaySession session, string sourceInstanceId, string senderEmployeeId)
    {
        SwarmsReplyData found = null;

        foreach (SwarmsReplyData reply in session.SwarmsReplies.DeliveredReplies)
        {
            if (!reply.RuleId.StartsWith("coworker-", StringComparison.Ordinal) ||
                !string.Equals(reply.SourceInstanceId, sourceInstanceId, StringComparison.Ordinal) ||
                !string.Equals(reply.SenderEmployeeId, senderEmployeeId, StringComparison.Ordinal))
            {
                continue;
            }

            Assert.That(found, Is.Null, "Only one coworker reaction may be authored per request.");
            found = reply;
        }

        Assert.That(found, Is.Not.Null);
        return found;
    }

    private static int CountReactions(WorkdaySession session, string sourceInstanceId)
    {
        int count = 0;

        foreach (SwarmsReplyData reply in session.SwarmsReplies.DeliveredReplies)
        {
            if (reply.RuleId.StartsWith("coworker-", StringComparison.Ordinal) &&
                string.Equals(reply.SourceInstanceId, sourceInstanceId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
