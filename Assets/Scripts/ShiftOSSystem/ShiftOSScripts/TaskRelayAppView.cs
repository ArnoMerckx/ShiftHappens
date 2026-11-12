using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TaskRelayAppView : ShiftOSAppView
{
    [SerializeField] private DocumentListRowView rowPrefab;

    [SerializeField] private RectTransform requestContent;
    [SerializeField] private DocumentListScrollView requestScroll;
    [SerializeField] private DocumentReaderView requestReader;

    [SerializeField] private RectTransform employeeContent;
    [SerializeField] private DocumentListScrollView employeeScroll;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text selectionLabel;
    [SerializeField] private TMP_Text feedbackLabel;

    private readonly List<DocumentListRowView> requestRows = new List<DocumentListRowView>();

    private readonly List<DocumentListRowView> employeeRows = new List<DocumentListRowView>();

    private WorkdaySession session;
    private TaskRelayPresenter presenter;
    private string displayedRequestId;
    private string displayedEmployeeId;
    private bool confirming;

    public override void Initialize(WorkdaySession workdaySession, ShiftOSAppState appState)
    {
        if (presenter != null)
            throw new InvalidOperationException("Initialize this app view only once.");

        if (appState == null)
            throw new ArgumentNullException(nameof(appState));

        if (rowPrefab == null || requestContent == null || requestScroll == null || requestReader == null || employeeContent == null || employeeScroll == null || confirmButton == null || selectionLabel == null || feedbackLabel == null)
        {
            throw new InvalidOperationException("Assign the row prefab, both list contents, both list scroll views, request reader, confirmation button, selection label and feedback label.");
        }

        if (requestContent.childCount != 0 || employeeContent.childCount != 0)
        {
            throw new InvalidOperationException("Both TaskRelay lists must start empty.");
        }

        presenter = new TaskRelayPresenter(workdaySession, appState.TaskRelay);

        session = workdaySession;

        requestScroll.Bind(presenter.State.Requests);
        employeeScroll.Bind(presenter.State.Employees);

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
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            confirmButton.interactable = false;
        }

        if (session == null)
            return;

        session.Delegation.RequestDelivered -= OnRequestDelivered;
        session.Delegation.DecisionResolved -= OnDecisionResolved;
        session.Delegation.RequestExpired -= OnRequestExpired;
        session.Workday.StateChanged -= RefreshContent;
    }

    private void ConnectAndRefresh()
    {
        Disconnect();

        confirmButton.onClick.AddListener(OnConfirmClicked);

        session.Delegation.RequestDelivered += OnRequestDelivered;
        session.Delegation.DecisionResolved += OnDecisionResolved;
        session.Delegation.RequestExpired += OnRequestExpired;
        session.Workday.StateChanged += RefreshContent;

        RefreshContent();
    }

    private void OnRequestDelivered(RequestInstance request)
    {
        RefreshContent();
    }

    private void OnDecisionResolved(DelegationDecision decision)
    {
        RefreshContent();
    }

    private void OnRequestExpired(RequestExpiryEntry entry)
    {
        RefreshContent();
    }

    private void RefreshContent()
    {
        presenter.ReconcileSelection();

        requestScroll.RequestRestore();
        employeeScroll.RequestRestore();

        List<RequestOverviewViewData> requests = presenter.GetOpenRequests();

        while (requestRows.Count > requests.Count)
        {
            int lastIndex = requestRows.Count - 1;
            DocumentListRowView row = requestRows[lastIndex];

            requestRows.RemoveAt(lastIndex);
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        for (int i = 0; i < requests.Count; i++)
        {
            if (i >= requestRows.Count)
                requestRows.Add(Instantiate(rowPrefab, requestContent, false));

            RequestOverviewViewData request = requests[i];

            string status = request.Status == RequestStatus.Resolved ? "Submitted" : request.Status.ToString();

            if (string.Equals(request.InstanceId, presenter.State.Requests.SelectedItemId, StringComparison.Ordinal))
            {
                status += " · Selected";
            }

            requestRows[i].Bind(request.InstanceId, request.Request.DisplayName, status, SelectRequest);
        }

        List<EmployeeViewData> employees = presenter.GetEmployees();

        for (int i = 0; i < employees.Count; i++)
        {
            if (i >= employeeRows.Count)
                employeeRows.Add(Instantiate(rowPrefab, employeeContent, false));

            EmployeeViewData employee = employees[i];
            string status = employee.Role.ToString();

            if (string.Equals(employee.EmployeeId, presenter.State.Employees.SelectedItemId, StringComparison.Ordinal))
            {
                status += " · Selected";
            }

            employeeRows[i].Bind(employee.EmployeeId, employee.DisplayName, status, SelectEmployee);
        }

        RefreshReader();

        RefreshConfirmation();
    }

    private void RefreshReader()
    {
        RequestOverviewViewData request = presenter.GetSelectedRequest();

        if (request == null)
        {
            requestReader.Clear("Select a request");
            return;
        }

        string timing;

        if (request.HelpWindow.HasValue)
        {
            TimeRange window = request.HelpWindow.Value;

            timing = $"Help window: {FormatMinute(window.StartMinute)}–" + $"{FormatMinute(window.EndMinute)}\n" +  $"Arrange by: {FormatTime(request.SubmissionCutoff)}";
        }
        else
        {
            timing = "Arrange before shift end " +  $"({FormatTime(request.SubmissionCutoff)}).";
        }

        string status = request.Status == RequestStatus.Resolved ? "Submitted" : request.Status.ToString();

        string body = $"From: {GetEmployeeName(request.Request.RequesterId)}\n" + $"{timing}\nStatus: {status}\n\n" + request.Request.Description;


        requestReader.Bind(request.InstanceId, request.Request.DisplayName, body, presenter.State.Requests);
    }

    private void RefreshConfirmation()
    {
        RequestOverviewViewData request = presenter.GetSelectedRequest();
        EmployeeViewData employee = presenter.GetSelectedEmployee();

        displayedRequestId = request?.InstanceId;
        displayedEmployeeId = employee?.EmployeeId;

        if (request == null)
        {
            selectionLabel.text = "Select a request.";
        }
        else if (request.Status != RequestStatus.Open)
        {
            selectionLabel.text = "This request is closed. Check Swarms for updates.";
        }
        else if (!session.Workday.CanAcceptWork)
        {
            selectionLabel.text = "The workday is not accepting delegation.";
        }
        else if (employee == null)
        {
            selectionLabel.text = "Select an employee.";
        }
        else
        {
            selectionLabel.text = $"Assign {employee.DisplayName} to:\n" + $"{request.Request.DisplayName}?";
        }

        confirmButton.interactable = !confirming && request != null && request.Status == RequestStatus.Open && employee != null && session.Workday.CanAcceptWork;

        feedbackLabel.text = presenter.State.Feedback;
    }

    private void OnConfirmClicked()
    {
        if (!isActiveAndEnabled || presenter == null || confirming || !confirmButton.interactable)
        {
            return;
        }

        string requestId = displayedRequestId;
        string employeeId = displayedEmployeeId;

        confirming = true;
        confirmButton.interactable = false;

        try
        {
            presenter.TryConfirm(requestId, employeeId);
        }
        finally
        {
            confirming = false;

            if (isActiveAndEnabled)
                RefreshContent();
        }
    }

    private void SelectRequest(string instanceId)
    {
        if (presenter.TrySelectRequest(instanceId))
            RefreshContent();
    }

    private void SelectEmployee(string employeeId)
    {
        if (presenter.TrySelectEmployee(employeeId))
            RefreshContent();
    }

    private string GetEmployeeName(string employeeId)
    {
        foreach (EmployeeViewData employee in presenter.GetEmployees())
        {
            if (string.Equals(employee.EmployeeId, employeeId, StringComparison.Ordinal))
            {
                return employee.DisplayName;
            }
        }

        return employeeId;
    }

    private static string FormatMinute(int minute)
    {
        return $"{minute / 60:00}:{minute % 60:00}";
    }

    private static string FormatTime(GameTime time)
    {
        string text = FormatMinute(time.MinuteOfDay);

        if (time.SecondOfMinute != 0 || time.MillisecondOfSecond != 0)
            text += $":{time.SecondOfMinute:00}";

        if (time.MillisecondOfSecond != 0)
            text += $".{time.MillisecondOfSecond:000}";

        return text;
    }
}