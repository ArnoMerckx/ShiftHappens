using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShareLessAppView : ShiftOSAppView
{
    [SerializeField] private DocumentListRowView rowPrefab;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private DocumentReaderView reader;
    [SerializeField] private DocumentListScrollView listScroll;

    private readonly List<DocumentListRowView> rows = new List<DocumentListRowView>();

    private WorkdaySession session;
    private ShareLessPresenter presenter;

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
            throw new InvalidOperationException("The employee list must start empty.");

        presenter = new ShareLessPresenter(workdaySession, appState.ShareLess);

        session = workdaySession;
        listScroll.Bind(presenter.State);

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
        {
            session.Consequences.ConsequenceCommitted -= OnConsequenceCommitted;
        }
    }

    private void ConnectAndRefresh()
    {
        session.Consequences.ConsequenceCommitted -= OnConsequenceCommitted;

        session.Consequences.ConsequenceCommitted += OnConsequenceCommitted;

        RefreshDirectory();
    }

    private void OnConsequenceCommitted(ConsequenceEntry entry)
    {
        RefreshDirectory();
    }

    private void RefreshDirectory()
    {
        listScroll.RequestRestore();

        List<EmployeeViewData> employees = presenter.GetEmployees();

        for (int i = 0; i < employees.Count; i++)
        {
            if (i >= rows.Count)
                rows.Add(Instantiate(rowPrefab, listContent, false));

            EmployeeViewData employee = employees[i];

            string status = employee.Role.ToString();

            if (string.Equals(employee.EmployeeId, presenter.State.SelectedItemId, StringComparison.Ordinal))
            {
                status += " · Selected";
            }

            rows[i].Bind(employee.EmployeeId, employee.DisplayName, status, SelectEmployee);
        }

        RefreshReader();
    }

    private void RefreshReader()
    {
        EmployeeViewData employee = presenter.GetSelectedEmployee();

        if (employee == null)
        {
            reader.Clear("Select an employee");
            return;
        }

        string body = $"Role: {employee.Role}\n\n" + $"{employee.Bio}\n\n" + $"Interests and experience\n{employee.InterestsAndExperience}";

        reader.Bind(employee.EmployeeId, employee.DisplayName, body, presenter.State);
    }

    private void SelectEmployee(string employeeId)
    {
        if (presenter.TrySelectEmployee(employeeId))
            RefreshDirectory();
    }
}