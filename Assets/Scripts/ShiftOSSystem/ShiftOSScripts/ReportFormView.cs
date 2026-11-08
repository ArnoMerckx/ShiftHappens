using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ReportFormView : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown incidentDropdown;
    [SerializeField] private TMP_Dropdown suspectDropdown;

    [SerializeField] private DocumentListRowView evidenceRowPrefab;
    [SerializeField] private RectTransform evidenceList;
    [SerializeField] private DocumentListScrollView formScroll;

    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text feedbackLabel;

    private WorkdaySession session;

    private readonly List<DocumentListRowView> evidenceRows = new List<DocumentListRowView>();

    private ReportFormPresenter presenter;

    private List<IncidentNoticeViewData> incidents = new List<IncidentNoticeViewData>();

    private List<EmployeeViewData> employees = new List<EmployeeViewData>();

    private bool refreshing;
    private bool refreshPending;

    public void Initialize(ReportFormPresenter form, WorkdaySession workdaySession, AppReadingState scrollState)
    {
        if (presenter != null)
            throw new InvalidOperationException("Initialize this form view only once.");

        if (form == null)
            throw new ArgumentNullException(nameof(form));

        if (incidentDropdown == null || suspectDropdown == null || evidenceRowPrefab == null || evidenceList == null || formScroll == null || submitButton == null || feedbackLabel == null)
        {
            throw new InvalidOperationException("Assign both dropdowns, the evidence row prefab, evidence list and form scroll, and submitButton and feedbackLabel");
        }

        if (evidenceList.childCount != 0)
            throw new InvalidOperationException("The evidence list must start empty.");

        if (workdaySession == null)
            throw new ArgumentNullException(nameof(workdaySession));

        if (scrollState == null)
            throw new ArgumentNullException(nameof(scrollState));

        session = workdaySession;
        formScroll.Bind(scrollState);

        submitButton.interactable = false;
        feedbackLabel.text = string.Empty;

        presenter = form;

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
        if (incidentDropdown != null)
            incidentDropdown.onValueChanged.RemoveListener(OnIncidentChanged);

        if (suspectDropdown != null)
            suspectDropdown.onValueChanged.RemoveListener(OnSuspectChanged);

        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(OnSubmitClicked);
            submitButton.interactable = false;
        }

        SetSessionSubscriptions(false);
    }

    private void ConnectAndRefresh()
    {
        submitButton.onClick.RemoveListener(OnSubmitClicked);
        submitButton.onClick.AddListener(OnSubmitClicked);

        SetSessionSubscriptions(true);

        incidentDropdown.onValueChanged.RemoveListener(OnIncidentChanged);
        incidentDropdown.onValueChanged.AddListener(OnIncidentChanged);

        suspectDropdown.onValueChanged.RemoveListener(OnSuspectChanged);
        suspectDropdown.onValueChanged.AddListener(OnSuspectChanged);

        RefreshChoices();
    }

    public void RefreshChoices()
    {
        refreshPending = true;
    }

    private void LateUpdate()
    {
        if (presenter == null || !refreshPending)
            return;

        if (incidentDropdown.IsExpanded || suspectDropdown.IsExpanded)
            return;

        RefreshChoicesNow();
    }

    private void RefreshChoicesNow()
    {
        refreshing = true;

        try
        {
            presenter.ReconcileDraft();

            incidents = presenter.GetOpenIncidents();
            employees = presenter.GetEmployees();

            List<string> incidentLabels = incidents.ConvertAll(item => $"{item.DisplayName} — due {item.DueTimeText}");

            incidentLabels.Insert(0, incidents.Count == 0 ? "No open incidents" : "Select an incident");

            incidentDropdown.ClearOptions();
            incidentDropdown.AddOptions(incidentLabels);
            incidentDropdown.SetValueWithoutNotify(incidents.FindIndex(item => string.Equals(item.InstanceId, presenter.Draft.SelectedIncidentInstanceId, StringComparison.Ordinal)) + 1);

            incidentDropdown.interactable = incidents.Count > 0;

            List<string> employeeLabels = employees.ConvertAll(employee => employee.DisplayName);

            employeeLabels.Insert(0, "Select a suspect");

            suspectDropdown.ClearOptions();
            suspectDropdown.AddOptions(employeeLabels);
            suspectDropdown.SetValueWithoutNotify(employees.FindIndex(employee => string.Equals(employee.EmployeeId, presenter.Draft.SelectedSuspectId, StringComparison.Ordinal)) + 1);

            suspectDropdown.interactable = presenter.Draft.SelectedIncidentInstanceId != null && employees.Count > 0;

            RefreshEvidenceRows();

            submitButton.interactable = session.Workday.CanAcceptWork;

            refreshPending = false;
        }
        finally
        {
            refreshing = false;
        }
    }

    private void OnIncidentChanged(int index)
    {
        if (refreshing || presenter == null)
            return;

        if (index == 0)
            presenter.Draft.Clear();
        else if (index > 0 && index <= incidents.Count)
            presenter.TrySelectIncident(incidents[index - 1].InstanceId);

        RefreshChoices();
    }

    private void OnSuspectChanged(int index)
    {
        if (refreshing || presenter == null)
            return;

        if (index == 0)
            presenter.Draft.ClearSuspect();
        else if (index > 0 && index <= employees.Count)
            presenter.TrySelectSuspect(employees[index - 1].EmployeeId);

        RefreshChoices();
    }
    private void SetSessionSubscriptions(bool subscribe)
    {
        if (session == null)
            return;

        session.Deliveries.IncidentDelivered -= OnIncidentStateChanged;
        session.Deadlines.IncidentOverdue -= OnIncidentStateChanged;
        session.Submissions.ReviewQueued -= OnReviewQueued;
        session.EvidenceChanged -= OnEvidenceChanged;
        session.Workday.StateChanged -= RefreshChoices;

        if (!subscribe)
            return;

        session.Deliveries.IncidentDelivered += OnIncidentStateChanged;
        session.Deadlines.IncidentOverdue += OnIncidentStateChanged;
        session.Submissions.ReviewQueued += OnReviewQueued;
        session.EvidenceChanged += OnEvidenceChanged;
        session.Workday.StateChanged += RefreshChoices;
    }

    private void OnIncidentStateChanged(IncidentInstance incident)
    {
        RefreshChoices();
    }

    private void OnReviewQueued(QueuedHrReview review)
    {
        RefreshChoices();
    }

    private void OnEvidenceChanged(string evidenceId)
    {
        RefreshChoices();
    }

    private void RefreshEvidenceRows()
    {
        formScroll.RequestRestore();

        List<EvidenceDocumentViewData> documents = presenter.GetEvidence();

        while (evidenceRows.Count > documents.Count)
        {
            int lastIndex = evidenceRows.Count - 1;
            DocumentListRowView row = evidenceRows[lastIndex];

            evidenceRows.RemoveAt(lastIndex);
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        bool canAttach = presenter.Draft.SelectedIncidentInstanceId != null;

        for (int i = 0; i < documents.Count; i++)
        {
            if (i >= evidenceRows.Count)
                evidenceRows.Add(Instantiate(evidenceRowPrefab, evidenceList, false));

            EvidenceDocumentViewData document = documents[i];

            evidenceRows[i].Bind(document.EvidenceId, document.DisplayName, IsEvidenceSelected(document.EvidenceId) ? "Attached — click to remove" : "Click to attach", OnEvidenceClicked);

            evidenceRows[i].GetComponent<Button>().interactable = canAttach;
        }
    }

    private bool IsEvidenceSelected(string evidenceId)
    {
        foreach (string selectedId in presenter.Draft.EvidenceIds)
        {
            if (string.Equals(selectedId, evidenceId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void OnEvidenceClicked(string evidenceId)
    {
        if (refreshing || presenter == null)
            return;

        presenter.TrySetEvidenceSelected(evidenceId, !IsEvidenceSelected(evidenceId));

        RefreshChoices();
    }

    private void OnSubmitClicked()
    {
        if (!isActiveAndEnabled || presenter == null || refreshing || !submitButton.interactable || incidentDropdown.IsExpanded || suspectDropdown.IsExpanded)
        {
            return;
        }

        submitButton.interactable = false;

        ReportValidationResult result = presenter.TrySubmit();

        formScroll.RequestRestore();

        feedbackLabel.text = result.IsValid ? "Report submitted — pending HR review." : result.Reason;

        RefreshChoices();
    }
}