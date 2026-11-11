using System;
using System.Collections.Generic;

public sealed class RequestProfileData
{
    public string ProfileId { get; }
    public EmployeeCapability RequiredCapability { get; }
    public IReadOnlyList<RequestSuitabilityEntry> RankedEmployees { get; }
    public IReadOnlyList<string> UnsuitableEmployeeIds { get; }

    public RequestProfileData(string profileId, EmployeeCapability requiredCapability, List<RequestSuitabilityEntry> rankedEmployees, List<string> unsuitableEmployeeIds)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile ID is required.", nameof(profileId));

        if (!Enum.IsDefined(typeof(EmployeeCapability), requiredCapability) || requiredCapability == EmployeeCapability.None)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredCapability));
        }

        if (rankedEmployees == null)
            throw new ArgumentNullException(nameof(rankedEmployees));

        if (unsuitableEmployeeIds == null)
            throw new ArgumentNullException(nameof(unsuitableEmployeeIds));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rankedCopy = new List<RequestSuitabilityEntry>();

        foreach (var entry in rankedEmployees)
        {
            if (entry == null || !seen.Add(entry.EmployeeId))
                throw new ArgumentException("Ranked employees must be nonnull and unique.", nameof(rankedEmployees));

            rankedCopy.Add(entry);
        }

        var unsuitableCopy = new List<string>();

        foreach (string employeeId in unsuitableEmployeeIds)
        {
            if (string.IsNullOrWhiteSpace(employeeId) || !seen.Add(employeeId))
            {
                throw new ArgumentException("Unsuitable employees must have unique, nonblank IDs and cannot also be ranked.", nameof(unsuitableEmployeeIds));
            }

            unsuitableCopy.Add(employeeId);
        }

        ProfileId = profileId;
        RequiredCapability = requiredCapability;
        RankedEmployees = rankedCopy.AsReadOnly();
        UnsuitableEmployeeIds = unsuitableCopy.AsReadOnly();
    }
}