using System;

public sealed class EvidenceDocumentViewData
{
    public string EvidenceId { get; }
    public string DisplayName { get; }
    public string BodyText { get; }
    public EvidenceSource Source { get; }
    public bool IsDiscovered { get; }
    public bool IsRead { get; }

    internal EvidenceDocumentViewData(EvidenceDefinition definition, bool isDiscovered, bool isRead)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        EvidenceId = definition.EvidenceId;
        DisplayName = definition.DisplayName;
        BodyText = definition.BodyText;
        Source = definition.Source;
        IsDiscovered = isDiscovered;
        IsRead = isRead;
    }
}