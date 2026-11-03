using System;

public sealed class ReportValidationResult
{
    public bool IsValid { get; }
    public string Reason { get; }

    public ReportValidationResult(bool isValid, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A validation reason is required.", nameof(reason));

        IsValid = isValid;
        Reason = reason;
    }
}