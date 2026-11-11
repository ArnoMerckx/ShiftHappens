using System;

public sealed class RequestExpiryEntry
{
    public string RequestInstanceId { get; }
    public string RequestId { get; }
    public string RequesterId { get; }
    public GameTime ExpiresAt { get; }
    public GameTime RecordedAt { get; }

    public RequestExpiryEntry(RequestInstance instance, GameTime recordedAt)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (instance.Status != RequestStatus.Expired)
            throw new ArgumentException("An expiry record requires an expired request.", nameof(instance));

        if (recordedAt.TotalMilliseconds < instance.ExpiresAt.TotalMilliseconds)
            throw new ArgumentException("Expiry cannot be recorded before its timestamp.", nameof(recordedAt));

        RequestInstanceId = instance.InstanceId;
        RequestId = instance.Request.RequestId;
        RequesterId = instance.Request.RequesterId;
        ExpiresAt = instance.ExpiresAt;
        RecordedAt = recordedAt;
    }
}