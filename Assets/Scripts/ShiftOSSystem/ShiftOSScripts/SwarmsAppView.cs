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
        Disconnect();
    }

    private void Disconnect()
    {
        if (session == null)
            return;

        session.Deliveries.IncidentDelivered -= OnIncidentDelivered;
        session.Delegation.RequestDelivered -= OnRequestDelivered;
        session.Delegation.DecisionResolved -= OnDecisionResolved;
        session.Delegation.RequestExpired -= OnRequestExpired;
        session.SwarmsReplies.ReplyDelivered -= OnReplyDelivered;
    }

    private void ConnectAndRefresh()
    {
        Disconnect();

        session.Deliveries.IncidentDelivered += OnIncidentDelivered;
        session.Delegation.RequestDelivered += OnRequestDelivered;
        session.Delegation.DecisionResolved += OnDecisionResolved;
        session.Delegation.RequestExpired += OnRequestExpired;
        session.SwarmsReplies.ReplyDelivered += OnReplyDelivered;

        RefreshNotices();
    }

    private void OnIncidentDelivered(IncidentInstance incident)
    {
        RefreshNotices();
    }

    private void RefreshNotices()
    {
        listScroll.RequestRestore();

        List<SwarmsNoticeViewData> notices = presenter.GetNotices();

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

            SwarmsNoticeViewData notice = notices[i];
            string status = notice.StatusText;

            if (string.Equals(notice.NoticeId, presenter.State.SelectedItemId, StringComparison.Ordinal))
            {
                status += " · Selected";
            }

            rows[i].Bind(notice.NoticeId, notice.DisplayName, status, SelectNotice);
        }

        RefreshReader();
    }

    private void RefreshReader()
    {
        SwarmsNoticeViewData selected = presenter.GetSelectedNotice();

        if (selected == null)
        {
            reader.Clear("Select a notice");
            return;
        }

        reader.Bind(selected.NoticeId, selected.DisplayName, selected.BodyText, presenter.State);
    }

    private void SelectNotice(string instanceId)
    {
        if (presenter.TrySelectNotice(instanceId))
            RefreshNotices();
    }

    private void OnRequestDelivered(RequestInstance request)
    {
        RefreshNotices();
    }

    private void OnDecisionResolved(DelegationDecision decision)
    {
        RefreshNotices();
    }

    private void OnRequestExpired(RequestExpiryEntry entry)
    {
        RefreshNotices();
    }

    private void OnReplyDelivered(SwarmsReplyData reply)
    {
        RefreshNotices();
    }
}