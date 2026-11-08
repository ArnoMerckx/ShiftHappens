using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SwarmsAppView : ShiftOSAppView
{
    [SerializeField] private DocumentListRowView rowPrefab;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private DocumentReaderView reader;
    [SerializeField] private DocumentListScrollView listScroll;

    private readonly List<DocumentListRowView> rows = new List<DocumentListRowView>();

    private WorkdaySession session;
    private SwarmsPresenter presenter;
    private string shownInstanceId;

    public override void Initialize(WorkdaySession workdaySession, ShiftOSAppState appState)
    {
        if (presenter != null)
            throw new InvalidOperationException("Initialize this app view only once.");

        if (appState == null)
            throw new ArgumentNullException(nameof(appState));

        if (rowPrefab == null || listContent == null || reader == null || listScroll == null)
        {
            throw new InvalidOperationException("Assign the row prefab, list content, reader and list scroll view.");
        }

        if (listContent.childCount != 0)
            throw new InvalidOperationException("The notice list must start empty.");

        presenter = new SwarmsPresenter(workdaySession, appState.Swarms);
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
            session.Deliveries.IncidentDelivered -= OnIncidentDelivered;
    }

    private void ConnectAndRefresh()
    {
        session.Deliveries.IncidentDelivered -= OnIncidentDelivered;
        session.Deliveries.IncidentDelivered += OnIncidentDelivered;

        RefreshNotices();
    }

    private void OnIncidentDelivered(IncidentInstance incident)
    {
        RefreshNotices();
    }

    private void RefreshNotices()
    {
        listScroll.RequestRestore();

        List<IncidentNoticeViewData> notices = presenter.GetNotices();

        while (rows.Count > notices.Count)
        {
            int lastIndex = rows.Count - 1;
            DocumentListRowView row = rows[lastIndex];

            rows.RemoveAt(lastIndex);
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        for (int i = 0; i < notices.Count; i++)
        {
            if (i >= rows.Count)
                rows.Add(Instantiate(rowPrefab, listContent, false));

            IncidentNoticeViewData notice = notices[i];
            string status = $"Due {notice.DueTimeText}";

            if (string.Equals(notice.InstanceId, presenter.State.SelectedItemId, StringComparison.Ordinal))
            {
                status += " · Selected";
            }

            rows[i].Bind(notice.InstanceId, notice.DisplayName, status, SelectNotice);
        }

        RefreshReader();
    }

    private void RefreshReader()
    {
        IncidentNoticeViewData selected = presenter.GetSelectedNotice();

        if (selected == null)
        {
            shownInstanceId = null;
            reader.Clear("Select a notice");
            return;
        }

        if (string.Equals(selected.InstanceId, shownInstanceId, StringComparison.Ordinal))
        {
            return;
        }

        string body = $"Report due: {selected.DueTimeText}\n\n{selected.BodyText}";

        reader.Bind(selected.InstanceId, selected.DisplayName, body, presenter.State);

        shownInstanceId = selected.InstanceId;
    }

    private void SelectNotice(string instanceId)
    {
        if (presenter.TrySelectNotice(instanceId))
            RefreshNotices();
    }
}