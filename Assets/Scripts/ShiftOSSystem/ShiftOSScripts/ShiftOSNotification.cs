using System;

public sealed class ShiftOSNotification
{
    public ShiftOSNotificationKind Kind { get; }
    public string Title { get; }
    public string Body { get; }

    public ShiftOSNotification(ShiftOSNotificationKind kind, string title, string body)
    {
        if (!Enum.IsDefined(typeof(ShiftOSNotificationKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A notification title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Notification text is required.", nameof(body));

        Kind = kind;
        Title = title;
        Body = body;
    }
}