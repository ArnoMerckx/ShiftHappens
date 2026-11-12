using System;

public sealed class TaskRelayResultViewData
{
    public string RequestInstanceId { get; }
    public RequestStatus Status { get; }

    public string HelperId { get; }
    public string HelperName { get; }
    public string Explanation { get; }

    public bool HasAssignment { get; }
    public bool IsSuitableArranged { get; }

    public GameTime RecordedAt { get; }

    public int CredibilityBefore { get; }
    public int CredibilityAfter { get; }
    public int CredibilityDelta => CredibilityAfter - CredibilityBefore;

    public TaskRelayResultViewData(RequestOverviewViewData request, AssignmentHistoryEntry assignment, ConsequenceEntry consequence, string helperName)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (consequence == null)
            throw new ArgumentNullException(nameof(consequence));

        if (request.Status != RequestStatus.Resolved &&
            request.Status != RequestStatus.Expired)
        {
            throw new ArgumentException("A recorded result requires a resolved or expired request.", nameof(request));
        }

        ConsequenceKind expectedKind = request.Status == RequestStatus.Resolved ? ConsequenceKind.RequestResolved : ConsequenceKind.RequestExpired;

        if (consequence.Kind != expectedKind || consequence.DayIndex != request.DeliveryAt.DayIndex || !string.Equals(consequence.InstanceId, request.InstanceId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The consequence must match this request result.", nameof(consequence));
        }

        if (request.Status == RequestStatus.Resolved)
        {
            if (assignment == null ||
                !string.Equals(
                    assignment.DayId,
                    consequence.DayId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    assignment.RequestInstanceId,
                    request.InstanceId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    assignment.HelperId,
                    request.HelperId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException("The assignment must match the recorded request and helper.", nameof(assignment));
            }

            if (string.IsNullOrWhiteSpace(helperName))
                throw new ArgumentException("The recorded helper needs a name.", nameof(helperName));
        }
        else if (assignment != null)
        {
            throw new ArgumentException("An unanswered request has no assignment.", nameof(assignment));
        }

        RequestInstanceId = request.InstanceId;
        Status = request.Status;

        HasAssignment = assignment != null;
        IsSuitableArranged = assignment?.IsSuitableArranged ?? false;

        HelperId = assignment?.HelperId ?? string.Empty;
        HelperName = HasAssignment ? helperName : string.Empty;

        Explanation = assignment?.Decision.Explanation ?? "No employee was assigned before the request expired.";

        RecordedAt = consequence.RecordedAt;
        CredibilityBefore = consequence.CredibilityBefore;
        CredibilityAfter = consequence.CredibilityAfter;
    }
}