using System;
using System.Globalization;

public sealed class SwarmsNoticeViewData
{
    public SwarmsNoticeKind Kind { get; }
    public string SourceId { get; }
    public string NoticeId { get; }
    public string DisplayName { get; }
    public string BodyText { get; }
    public string StatusText { get; }

    public SwarmsNoticeViewData(SwarmsNoticeKind kind, string sourceId, string displayName, string bodyText, string statusText)
    {
        if (!Enum.IsDefined(typeof(SwarmsNoticeKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("A source ID is required.", nameof(sourceId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A notice title is required.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(bodyText))
            throw new ArgumentException("Notice text is required.", nameof(bodyText));

        if (string.IsNullOrWhiteSpace(statusText))
            throw new ArgumentException("A notice status is required.", nameof(statusText));

        Kind = kind;
        SourceId = sourceId;
        NoticeId =((int)kind).ToString(CultureInfo.InvariantCulture) + ":" + sourceId;

        DisplayName = displayName;
        BodyText = bodyText;
        StatusText = statusText;
    }
}