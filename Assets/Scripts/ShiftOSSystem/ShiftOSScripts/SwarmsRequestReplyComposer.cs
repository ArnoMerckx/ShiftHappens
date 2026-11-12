using System;
using System.Collections.Generic;

public sealed class SwarmsRequestReplyComposer
{
    private const string ArrangedRuleId = "requester-help-arranged-v1";
    private const string NotHelpedRuleId = "requester-not-helped-v1";
    private const string ExpiredRuleId = "requester-expired-v1";

    private readonly WorkdaySettings settings;
    private readonly DelegationService delegation;
    private readonly ConsequenceCoordinator consequences;
    private readonly EmployeeWorkloadHistory workload;
    private readonly SwarmsReplyFeed feed;
    private readonly ReplyTimingRules timing;
    private readonly Dictionary<string, string> employeeNames;

    public SwarmsRequestReplyComposer(DayData day, DelegationService delegation, ConsequenceCoordinator consequences, EmployeeWorkloadHistory workload, SwarmsReplyFeed feed, ReplyTimingRules timing)
    {
        if (day == null)
            throw new ArgumentNullException(nameof(day));

        this.delegation = delegation ?? throw new ArgumentNullException(nameof(delegation));

        this.consequences = consequences ?? throw new ArgumentNullException(nameof(consequences));

        this.workload = workload ?? throw new ArgumentNullException(nameof(workload));

        this.feed = feed ?? throw new ArgumentNullException(nameof(feed));

        this.timing = timing ?? throw new ArgumentNullException(nameof(timing));

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
        if (consequence == null || !string.Equals(consequence.DayId, settings.DayId, StringComparison.Ordinal))
        {
            return;
        }

        if (consequence.Kind != ConsequenceKind.RequestResolved && consequence.Kind != ConsequenceKind.RequestExpired)
        {
            return;
        }

        if (!delegation.TryGetDelivered(consequence.InstanceId, out RequestInstance instance))
        {
            return;
        }

        var request = new RequestOverviewViewData(instance);

        if (consequence.Kind == ConsequenceKind.RequestResolved)
        {
            QueueResolvedReply(consequence, request, instance);
            return;
        }

        QueueExpiredReply(consequence, request);
    }

    private void QueueResolvedReply(ConsequenceEntry consequence, RequestOverviewViewData request, RequestInstance instance)
    {
        if (request.Status != RequestStatus.Resolved || instance.Decision == null)
        {
            return;
        }

        AssignmentHistoryEntry assignment = FindAssignment(consequence.InstanceId);

        if (assignment == null || !ReferenceEquals(assignment.Decision, instance.Decision))
        {
            return;
        }

        string helperName = GetEmployeeName(assignment.HelperId);
        string ruleId;
        string body;

        switch (assignment.Outcome)
        {
            case DelegationOutcome.BestAvailable:
            case DelegationOutcome.CapableAlternative:
                ruleId = ArrangedRuleId;
                body = helperName + " was arranged to help me with " + request.Request.DisplayName + ".";
                break;

            case DelegationOutcome.Unsuitable:
            case DelegationOutcome.Unavailable:
                ruleId = NotHelpedRuleId;
                body = "I wasn't helped with " + request.Request.DisplayName + " after the assignment to " + helperName + ".";
                break;

            default:
                return;
        }

        QueueReply(consequence, request, ruleId, body);
    }

    private void QueueExpiredReply(ConsequenceEntry consequence, RequestOverviewViewData request)
    {
        if (request.Status != RequestStatus.Expired)
            return;

        string body ="I wasn't helped with " + request.Request.DisplayName + " before the request closed.";

        QueueReply(consequence, request, ExpiredRuleId, body);
    }

    private void QueueReply(ConsequenceEntry consequence, RequestOverviewViewData request, string ruleId, string body)
    {
        string requesterId = request.Request.RequesterId;

        if (!employeeNames.TryGetValue(requesterId, out string requesterName))
        {
            return;
        }

        GameTime dueAt = timing.GetRequestDueAt(request, consequence.RecordedAt, settings);

        var reply = new SwarmsReplyData(settings.DayId, consequence.Kind, consequence.InstanceId, ruleId, requesterId, requesterName, request.Request.DisplayName + " update", body, dueAt);

        feed.TryQueue(reply);
    }

    private AssignmentHistoryEntry FindAssignment(string requestInstanceId)
    {
        foreach (AssignmentHistoryEntry assignment in workload.History)
        {
            if (string.Equals(assignment.DayId, settings.DayId, StringComparison.Ordinal) && string.Equals( assignment.RequestInstanceId, requestInstanceId, StringComparison.Ordinal))
            {
                return assignment;
            }
        }

        return null;
    }

    private string GetEmployeeName(string employeeId)
    {
        return employeeNames.TryGetValue(employeeId, out string employeeName) ? employeeName : employeeId;
    }
}