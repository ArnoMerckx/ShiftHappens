using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DigitalDocumentAppView : ShiftOSAppView
{
    [SerializeField] private EvidenceSource source = EvidenceSource.InWatch;
    [SerializeField] private DocumentListRowView rowPrefab;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private DocumentReaderView reader;

    [SerializeField] private DocumentListScrollView listScroll;

    private readonly List<DocumentListRowView> rows = new List<DocumentListRowView>();

    private WorkdaySession session;
    private DigitalDocumentPresenter presenter;
    private string shownEvidenceId;

    public override void Initialize(WorkdaySession workdaySession, ShiftOSAppState appState)
    {
        if (appState == null)
            throw new ArgumentNullException(nameof(appState));

        AppReadingState readingState;

        switch (source)
        {
            case EvidenceSource.InWatch:
                readingState = appState.InWatch;
                break;

            case EvidenceSource.Excess:
                readingState = appState.Excess;
                break;

            case EvidenceSource.ShareLess:
                readingState = appState.ShareLess;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }

        Initialize(workdaySession, readingState);
    }

    public void Initialize(WorkdaySession workdaySession, AppReadingState readingState)
    {
        if (presenter != null)
            throw new InvalidOperationException("Initialize this app view only once.");

        if (rowPrefab == null || listContent == null || reader == null || listScroll == null)
        {
            throw new InvalidOperationException("Assign the row prefab, list content, reader and list scroll view.");
        }

        if (listContent.childCount != 0)
            throw new InvalidOperationException("The document list must start empty.");



        presenter = new DigitalDocumentPresenter(workdaySession, readingState, source);

        listScroll.Bind(presenter.State);

        session = workdaySession;

        if (isActiveAndEnabled)
            ConnectAndRefresh();
    }

    private void OnEnable()
    {
        if (presenter != null)
            ConnectAndRefresh();
    }

    private void OnDisable()
    {
        if (session != null)
            session.Deliveries.EvidenceAvailable -= OnEvidenceAvailable;
    }

    private void ConnectAndRefresh()
    {
        session.Deliveries.EvidenceAvailable -= OnEvidenceAvailable;
        session.Deliveries.EvidenceAvailable += OnEvidenceAvailable;

        RefreshDocuments();
    }

    private void OnEvidenceAvailable(string evidenceId)
    {
        RefreshDocuments();
    }

    private void RefreshDocuments()
    {
        listScroll.RequestRestore();

        List<EvidenceDocumentViewData> documents = presenter.GetDocuments();

        while (rows.Count > documents.Count)
        {
            int lastIndex = rows.Count - 1;
            DocumentListRowView row = rows[lastIndex];

            rows.RemoveAt(lastIndex);
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        for (int i = 0; i < documents.Count; i++)
        {
            if (i >= rows.Count)
            {
                DocumentListRowView row = Instantiate(rowPrefab, listContent, false);

                rows.Add(row);
            }

            EvidenceDocumentViewData document = documents[i];

            rows[i].Bind(document, string.Equals(document.EvidenceId, presenter.State.SelectedItemId, StringComparison.Ordinal), OpenDocument);
        }

        RefreshReader();
    }

    private void RefreshReader()
    {
        EvidenceDocumentViewData selected = presenter.GetSelectedDocument();

        if (selected == null)
        {
            shownEvidenceId = null;
            reader.Clear();
            return;
        }

        if (string.Equals(selected.EvidenceId, shownEvidenceId, StringComparison.Ordinal))
        {
            return;
        }

        reader.Bind(selected, presenter.State);
        shownEvidenceId = selected.EvidenceId;
    }

    private void OpenDocument(string evidenceId)
    {
        if (presenter.TryOpenDocument(evidenceId))
            RefreshDocuments();
    }
}