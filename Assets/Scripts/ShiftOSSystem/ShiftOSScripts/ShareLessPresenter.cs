using System;
using System.Collections.Generic;

public sealed class ShareLessPresenter
{
    private readonly WorkdaySession session;

    public AppReadingState State { get; }

    public ShareLessPresenter(WorkdaySession session, AppReadingState state)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));

        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public List<EmployeeViewData> GetEmployees()
    {
        return session.GetEmployeeRoster();
    }

    public EmployeeViewData GetSelectedEmployee()
    {
        foreach (EmployeeViewData employee in GetEmployees())
        {
            if (string.Equals(employee.EmployeeId, State.SelectedItemId, StringComparison.Ordinal))
            {
                return employee;
            }
        }

        return null;
    }

    public bool TrySelectEmployee(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return false;

        foreach (EmployeeViewData employee in GetEmployees())
        {
            if (!string.Equals(employee.EmployeeId, employeeId, StringComparison.Ordinal))
            {
                continue;
            }

            State.SelectItem(employeeId);
            return true;
        }

        return false;
    }

    public int GetTrust(string employeeId)
    {
        return session.Trust.GetTrust(employeeId);
    }
}