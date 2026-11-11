using System;

public sealed class RequestData
{
    public string RequestId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string RequesterId { get; }
    public EmployeeCapability Category { get; }
    public bool AllowSelfHelp { get; }

    public RequestData(string requestId, string displayName, string description, string requesterId, EmployeeCapability category, bool allowSelfHelp = false)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("Request ID is required.", nameof(requestId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Request name is required.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Request description is required.", nameof(description));

        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID is required.", nameof(requesterId));

        if (!Enum.IsDefined(typeof(EmployeeCapability), category) || category == EmployeeCapability.None)
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        RequestId = requestId;
        DisplayName = displayName;
        Description = description;
        RequesterId = requesterId;
        Category = category;
        AllowSelfHelp = allowSelfHelp;
    }
}