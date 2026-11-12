using System;
using System.Globalization;

public sealed class SwarmsReplyData
{
    public string ReplyId { get; }
    public string DayId { get; }
    public ConsequenceKind SourceKind { get; }
    public string SourceInstanceId { get; }
    public string RuleId { get; }

    public string SenderEmployeeId { get; }
    public string SenderName { get; }
    public string Subject { get; }
    public string BodyText { get; }
    public GameTime DueAt { get; }

    public SwarmsReplyData(string dayId, ConsequenceKind sourceKind, string sourceInstanceId, string ruleId, string senderEmployeeId, string senderName, string subject, string bodyText, GameTime dueAt)
    {
        if (!Enum.IsDefined(typeof(ConsequenceKind), sourceKind))
            throw new ArgumentOutOfRangeException(nameof(sourceKind));

        RequireText(dayId, nameof(dayId));
        RequireText(sourceInstanceId, nameof(sourceInstanceId));
        RequireText(ruleId, nameof(ruleId));
        RequireText(senderEmployeeId, nameof(senderEmployeeId));
        RequireText(senderName, nameof(senderName));
        RequireText(subject, nameof(subject));
        RequireText(bodyText, nameof(bodyText));

        DayId = dayId;
        SourceKind = sourceKind;
        SourceInstanceId = sourceInstanceId;
        RuleId = ruleId;
        SenderEmployeeId = senderEmployeeId;
        SenderName = senderName;
        Subject = subject;
        BodyText = bodyText;
        DueAt = dueAt;

        ReplyId = EncodePart(dayId) + ":" + ((int)sourceKind).ToString(CultureInfo.InvariantCulture) + ":" + EncodePart(sourceInstanceId) + ":" + EncodePart(ruleId) + ":" + EncodePart(senderEmployeeId);
    }

    private static string EncodePart(string value)
    {
        return value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
    }

    private static void RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A nonblank value is required.", parameterName);
    }
}