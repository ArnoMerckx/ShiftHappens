using System;
using System.Collections.Generic;

public sealed class SwarmsCoworkerReactionComposer
{
    private const string ProtestRuleId = "coworker-assignment-protest-v1";

    private const string LowTrustRuleId = "coworker-low-trust-v1";

    private const string RepeatedAssignmentRuleId = "coworker-repeated-assignment-v1";

    private const string ThanksRuleId = "coworker-assignment-thanks-v1";

    private readonly WorkdaySettings settings;
    private readonly DelegationService delegation;
    private readonly EmployeeWorkloadHistory workload;
    private readonly EmployeeTrustState trust;
    private readonly SwarmsReplyFeed feed;
    private readonly ReplyTimingRules timing;
    private readonly CoworkerReactionRules rules;

    private readonly Dictionary<string, string> employeeNames;

    public SwarmsCoworkerReactionComposer(DayData day, DelegationService delegation, ConsequenceCoordinator consequences, EmployeeWorkloadHistory workload, EmployeeTrustState trust, SwarmsReplyFeed feed, ReplyTimingRules timing, CoworkerReactionRules rules)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));

        this.delegation = delegation ?? throw new ArgumentNullException(nameof(delegation));

        if (consequences == null)
            throw new ArgumentNullException(nameof(consequences));

        this.workload = workload ?? throw new ArgumentNullException(nameof(workload));

        this.trust = trust ?? throw new ArgumentNullException(nameof(trust));

        this.feed = feed ?? throw new ArgumentNullException(nameof(feed));

        this.timing = timing ?? throw new ArgumentNullException(nameof(timing));

        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));

        settings = day.ScheduleData.Settings;

        employeeNames = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (EmployeeViewData employee in
                 day.ScheduleData.EmployeeRoster)
        {
            employeeNames.Add(employee.EmployeeId, employee.DisplayName);
        }

        consequences.ConsequenceCommitted += OnConsequenceCommitted;
    }

    private void OnConsequenceCommitted(ConsequenceEntry consequence)
    {
        if (consequence == null || consequence.Kind != ConsequenceKind.RequestResolved || !string.Equals(consequence.DayId, settings.DayId, StringComparison.Ordinal))
        {
            return;
        }

        if (!delegation.TryGetDelivered(consequence.InstanceId, out RequestInstance instance))
        {
            return;
        }

        if (instance.Status != RequestStatus.Resolved || instance.Decision == null)
        {
            return;
        }

        AssignmentHistoryEntry assignment = FindAssignment(consequence.InstanceId);

        if (assignment == null || !ReferenceEquals(assignment.Decision, instance.Decision))
        {
            return;
        }

        if (!employeeNames.TryGetValue(assignment.HelperId, out string helperName) || !employeeNames.TryGetValue( instance.Request.RequesterId, out string requesterName))
        {
            return;
        }

        string ruleId;
        string body;

        if (assignment.Outcome == DelegationOutcome.Unsuitable || assignment.Outcome == DelegationOutcome.Unavailable)
        {
            ruleId = ProtestRuleId;
            body = "I couldn't help " + requesterName + " with " + instance.Request.DisplayName + " this time.";
        }
        else if (trust.GetTrust(assignment.HelperId) <= rules.LowTrustThreshold)
        {
            ruleId = LowTrustRuleId;
            body = "I'm still uneasy about how requests involving me have been handled. Please take more care with future assignments.";
        }
        else if (CountCurrentDayAssignments(assignment.HelperId) >= rules.RepeatedAssignmentCount)
        {
            ruleId = RepeatedAssignmentRuleId;
            body = "You've assigned me several requests today. Please keep sharing requests around.";
        }
        else
        {
            ruleId = ThanksRuleId;
            body = "Thanks for arranging for me to help " + requesterName + " with " + instance.Request.DisplayName + ".";
        }

        var request = new RequestOverviewViewData(instance);

        GameTime dueAt = timing.GetRequestDueAt(request, consequence.RecordedAt, settings);

        feed.TryQueue(new SwarmsReplyData(settings.DayId, consequence.Kind, consequence.InstanceId, ruleId, assignment.HelperId, helperName, "About " + instance.Request.DisplayName, body, dueAt));
    }

    private AssignmentHistoryEntry FindAssignment(string requestInstanceId)
    {
        foreach (AssignmentHistoryEntry assignment in workload.History)
        {
            if (string.Equals(assignment.DayId, settings.DayId, StringComparison.Ordinal) &&
                string.Equals(assignment.RequestInstanceId, requestInstanceId, StringComparison.Ordinal))
            {
                return assignment;
            }
        }

        return null;
    }

    private int CountCurrentDayAssignments(string employeeId)
    {
        int count = 0;

        foreach (AssignmentHistoryEntry assignment in workload.History)
        {
            if (string.Equals(assignment.DayId, settings.DayId, StringComparison.Ordinal) &&
                string.Equals(assignment.HelperId, employeeId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}