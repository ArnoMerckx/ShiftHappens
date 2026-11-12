using System;
using System.Collections.Generic;

public sealed class TaskRelayPresenter
{
    private readonly WorkdaySession session;

    public TaskRelayAppState State { get; }

    public TaskRelayPresenter(WorkdaySession session, TaskRelayAppState state)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));

        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public List<RequestOverviewViewData> GetRequests()
    {
        return session.GetRequestOverview();
    }

    public List<RequestOverviewViewData> GetOpenRequests()
    {
        var open = new List<RequestOverviewViewData>();

        foreach (RequestOverviewViewData request in GetRequests())
        {
            if (request.Status == RequestStatus.Open)
                open.Add(request);
        }

        return open;
    }

    public List<EmployeeViewData> GetEmployees()
    {
        return session.GetEmployeeRoster();
    }

    public RequestOverviewViewData GetSelectedRequest()
    {
        foreach (RequestOverviewViewData request in GetRequests())
        {
            if (string.Equals(request.InstanceId, State.Requests.SelectedItemId, StringComparison.Ordinal))
            {
                return request;
            }
        }

        return null;
    }

    public EmployeeViewData GetSelectedEmployee()
    {
        foreach (EmployeeViewData employee in GetEmployees())
        {
            if (string.Equals(employee.EmployeeId, State.Employees.SelectedItemId, StringComparison.Ordinal))
            {
                return employee;
            }
        }

        return null;
    }

    public bool TrySelectRequest(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        foreach (RequestOverviewViewData request in GetOpenRequests())
        {
            if (!string.Equals(request.InstanceId, instanceId, StringComparison.Ordinal))
            {
                continue;
            }

            State.SelectRequest(instanceId);
            return true;
        }

        return false;
    }

    public bool TrySelectEmployee(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return false;

        RequestOverviewViewData request = GetSelectedRequest();

        if (request == null || request.Status != RequestStatus.Open)
            return false;

        foreach (EmployeeViewData employee in GetEmployees())
        {
            if (!string.Equals(employee.EmployeeId, employeeId, StringComparison.Ordinal))
            {
                continue;
            }

            State.SelectEmployee(employeeId);
            return true;
        }

        return false;
    }

    public void ReconcileSelection()
    {
        if (State.Requests.SelectedItemId == null)
            return;

        RequestOverviewViewData request = GetSelectedRequest();

        if (request != null && request.Status == RequestStatus.Open)
            return;

        State.ClearRequestSelection();

        if (request?.Status == RequestStatus.Resolved)
        {
            State.SetFeedback("Assignment submitted. Watch Swarms for a reply.");
        }
        else if (request?.Status == RequestStatus.Expired)
        {
            State.SetFeedback("This request is closed. Check Swarms for updates.");
        }
    }

    public DelegationSubmissionResult TryConfirm(string displayedRequestId, string displayedEmployeeId)
    {
        RequestOverviewViewData request = GetSelectedRequest();

        if (request == null)
            return RejectConfirmation("Select a delivered request.");

        EmployeeViewData employee = GetSelectedEmployee();

        if (employee == null)
            return RejectConfirmation("Select an employee.");

        if (!string.Equals(request.InstanceId, displayedRequestId, StringComparison.Ordinal) || !string.Equals(employee.EmployeeId, displayedEmployeeId, StringComparison.Ordinal))
        {
            return RejectConfirmation("The selection changed. Review the request and employee before confirming.");
        }

        DelegationSubmissionResult result = session.TryDelegate(displayedRequestId, displayedEmployeeId);

        if (string.Equals(State.Requests.SelectedItemId, displayedRequestId, StringComparison.Ordinal) && string.Equals(State.Employees.SelectedItemId, displayedEmployeeId, StringComparison.Ordinal))
        {
            State.SetFeedback(result.IsAccepted ? "Assignment submitted. Watch Swarms for a reply." : result.Reason);
        }

        return result;
    }

    public TaskRelayResultViewData GetSelectedResult()
    {
        RequestOverviewViewData request = GetSelectedRequest();

        if (request == null || request.Status == RequestStatus.Open)
            return null;

        AssignmentHistoryEntry assignment = null;

        foreach (AssignmentHistoryEntry entry in session.Workload.History)
        {
            if (string.Equals(entry.DayId, session.Settings.DayId, StringComparison.Ordinal) && string.Equals(entry.RequestInstanceId, request.InstanceId, StringComparison.Ordinal))
            {
                assignment = entry;
                break;
            }
        }

        ConsequenceKind expectedKind = request.Status == RequestStatus.Resolved ? ConsequenceKind.RequestResolved : ConsequenceKind.RequestExpired;

        ConsequenceEntry consequence = null;

        foreach (ConsequenceEntry entry in session.Consequences.History)
        {
            if (entry.Kind == expectedKind && string.Equals(entry.DayId, session.Settings.DayId, StringComparison.Ordinal) && string.Equals(entry.InstanceId, request.InstanceId, StringComparison.Ordinal))
            {
                consequence = entry;
                break;
            }
        }

        if (consequence == null)
            return null;

        string helperName = request.HelperId;

        foreach (EmployeeViewData employee in GetEmployees())
        {
            if (string.Equals(employee.EmployeeId, request.HelperId, StringComparison.Ordinal))
            {
                helperName = employee.DisplayName;
                break;
            }
        }

        return new TaskRelayResultViewData(request, assignment, consequence, helperName);
    }

    private DelegationSubmissionResult RejectConfirmation(string reason)
    {
        State.SetFeedback(reason);
        return DelegationSubmissionResult.Rejected(reason);
    }
}