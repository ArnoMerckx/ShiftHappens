using System;
using System.Collections.Generic;

public sealed class DigitalDocumentPresenter
{
    private readonly WorkdaySession session;
    private readonly EvidenceSource source;

    public AppReadingState State { get; }

    public DigitalDocumentPresenter(WorkdaySession session, AppReadingState state, EvidenceSource source)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));

        State = state ?? throw new ArgumentNullException(nameof(state));

        if (source != EvidenceSource.InWatch && source != EvidenceSource.Excess && source != EvidenceSource.ShareLess)
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        this.source = source;
    }

    public List<EvidenceDocumentViewData> GetDocuments()
    {
        return session.GetAvailableDigitalDocuments(source);
    }

    public EvidenceDocumentViewData GetSelectedDocument()
    {
        foreach (EvidenceDocumentViewData document in GetDocuments())
        {
            if (string.Equals(document.EvidenceId, State.SelectedItemId, StringComparison.Ordinal))
            {
                return document;
            }
        }

        return null;
    }

    public bool TryOpenDocument(string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId) || !session.Workday.CanAcceptWork)
        {
            return false;
        }

        foreach (EvidenceDocumentViewData document in GetDocuments())
        {
            if (!string.Equals(document.EvidenceId, evidenceId, StringComparison.Ordinal))
            {
                continue;
            }

            session.TryReadEvidence(evidenceId);
            State.SelectItem(evidenceId);
            return true;
        }

        return false;
    }
}