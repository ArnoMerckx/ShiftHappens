using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ReportOverviewView : MonoBehaviour
{
    [SerializeField] private DocumentListRowView rowPrefab;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private DocumentReaderView reader;
    [SerializeField] private DocumentListScrollView listScroll;

    private readonly List<DocumentListRowView> rows = new List<DocumentListRowView>();

    private WorkdaySession session;
    private ReportOverviewPresenter presenter;
    private bool refreshPending;

    public void Initialize(WorkdaySession workdaySession, AppReadingState state)
    {
        if (presenter != null)
            throw new InvalidOperationException("Initialize this overview only once.");

        if (workdaySession == null)
            throw new ArgumentNullException(nameof(workdaySession));

        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (rowPrefab == null || listContent == null || reader == null || listScroll == null)
        {
            throw new InvalidOperationException("Assign the row prefab, list content, reader and list scroll.");
        }

        if (listContent.childCount != 0)
            throw new InvalidOperationException("The overview list must start empty.");

        session = workdaySession;
        presenter = new ReportOverviewPresenter(session, state);
        listScroll.Bind(state);

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
        if (presenter == null)
            return;

        presenter.Changed -= RequestRefresh;
        presenter.Disconnect();
    }

    private void ConnectAndRefresh()
    {
        presenter.Changed -= RequestRefresh;
        presenter.Changed += RequestRefresh;
        presenter.Connect();
    }

    private void RequestRefresh()
    {
        refreshPending = true;
    }

    private void LateUpdate()
    {
        if (presenter == null || !refreshPending)
            return;

        refreshPending = false;
        RefreshOverview();
    }

    private void RefreshOverview()
    {
        listScroll.RequestRestore();

        List<ReportOverviewViewData> entries = presenter.GetEntries();

        while (rows.Count > entries.Count)
        {
            int lastIndex = rows.Count - 1;
            DocumentListRowView row = rows[lastIndex];

            rows.RemoveAt(lastIndex);
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (i >= rows.Count)
                rows.Add(Instantiate(rowPrefab, listContent, false));

            ReportOverviewViewData entry = entries[i];

            string status = $"{entry.Status.ToString().ToUpperInvariant()} · Due {entry.DueTimeText}";

            if (string.Equals(entry.InstanceId, presenter.State.SelectedItemId, StringComparison.Ordinal))
            {
                status += " · Selected";
            }

            rows[i].Bind(entry.InstanceId, entry.DisplayName, status, SelectEntry);
        }

        ReportOverviewViewData selected = presenter.GetSelectedEntry();

        if (selected == null)
        {
            reader.Clear(entries.Count == 0 ? "No incidents delivered" : "Select an incident");
            return;
        }

        reader.Bind(selected.InstanceId, selected.DisplayName, BuildBody(selected), presenter.State);
    }

    private void SelectEntry(string instanceId)
    {
        if (isActiveAndEnabled)
            presenter.TrySelectEntry(instanceId);
    }

    private string BuildBody(ReportOverviewViewData entry)
    {
        var body = new StringBuilder();

        body.AppendLine($"Status: {entry.Status.ToString().ToUpperInvariant()}");
        body.AppendLine($"Report due: {entry.DueTimeText}");
        body.AppendLine();
        body.AppendLine(entry.BodyText);
        body.AppendLine();

        if (entry.SuspectId == null)
        {
            body.AppendLine("No report submitted.");

            if (entry.Status == IncidentStatus.Overdue)
                body.AppendLine("The reporting deadline was missed.");

            return body.ToString();
        }

        EmployeeViewData suspect = session.GetEmployeeRoster().Find(employee => string.Equals(employee.EmployeeId, entry.SuspectId, StringComparison.Ordinal));

        body.AppendLine($"Blamed employee: {suspect?.DisplayName ?? entry.SuspectId}");
        body.AppendLine("Submitted evidence:");

        List<EvidenceDocumentViewData> documents = session.GetAttachableDocuments();

        if (entry.EvidenceIds.Count == 0)
            body.AppendLine("- None");

        foreach (string evidenceId in entry.EvidenceIds)
        {
            EvidenceDocumentViewData document = documents.Find(item => string.Equals(item.EvidenceId, evidenceId, StringComparison.Ordinal));

            body.AppendLine($"- {document?.DisplayName ?? evidenceId}");
        }

        body.AppendLine();

        if (entry.Status == IncidentStatus.Pending)
        {
            body.AppendLine("Awaiting HR review.");
        }
        else
        {
            body.AppendLine(entry.DecisionSummary);

            foreach (string reason in entry.DecisionReasons)
                body.AppendLine($"- {reason}");
        }

        return body.ToString();
    }
}