using System;
using System.Collections.Generic;

public sealed class ConsequenceEntry
{
    public string DayId { get; }
    public string InstanceId { get; }
    public ConsequenceKind Kind { get; }
    public GameTime RecordedAt { get; }
    public int DayIndex => RecordedAt.DayIndex;

    public int CredibilityBefore { get; }
    public int CredibilityAfter { get; }
    public int CredibilityDelta => CredibilityAfter - CredibilityBefore;

    public IReadOnlyList<EmployeeTrustChange> TrustChanges { get; }

    public ConsequenceEntry(string dayId, string instanceId, ConsequenceKind kind, GameTime recordedAt, int credibilityBefore, int credibilityAfter, List<EmployeeTrustChange> trustChanges)
    {
        if (string.IsNullOrWhiteSpace(dayId))
            throw new ArgumentException("A day ID is required.", nameof(dayId));

        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("An instance ID is required.", nameof(instanceId));

        if (!Enum.IsDefined(typeof(ConsequenceKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (credibilityBefore < 0)
            throw new ArgumentOutOfRangeException(nameof(credibilityBefore));

        if (credibilityAfter < 0)
            throw new ArgumentOutOfRangeException(nameof(credibilityAfter));

        if (trustChanges == null)
            throw new ArgumentNullException(nameof(trustChanges));

        var copy = new List<EmployeeTrustChange>();
        var employeeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var change in trustChanges)
        {
            if (change == null)
                throw new ArgumentException("Trust changes cannot contain null.", nameof(trustChanges));

            if (!employeeIds.Add(change.EmployeeId))
                throw new ArgumentException("Each employee can appear only once per consequence.", nameof(trustChanges));

            copy.Add(change);
        }

        DayId = dayId;
        InstanceId = instanceId;
        Kind = kind;
        RecordedAt = recordedAt;
        CredibilityBefore = credibilityBefore;
        CredibilityAfter = credibilityAfter;
        TrustChanges = copy.AsReadOnly();
    }
}