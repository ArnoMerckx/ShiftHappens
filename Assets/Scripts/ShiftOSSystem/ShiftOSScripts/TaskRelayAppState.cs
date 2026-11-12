using System;

public sealed class TaskRelayAppState
{
    public AppReadingState Requests { get; } = new AppReadingState();
    public AppReadingState Employees { get; } = new AppReadingState();

    public string Feedback { get; private set; } = string.Empty;

    public void SelectRequest(string instanceId)
    {
        ValidateId(instanceId, nameof(instanceId));

        if (string.Equals(Requests.SelectedItemId, instanceId, StringComparison.Ordinal))
        {
            return;
        }

        Requests.SelectItem(instanceId);
        ClearEmployeeSelection();
    }

    public void SelectEmployee(string employeeId)
    {
        ValidateId(employeeId, nameof(employeeId));

        if (Requests.SelectedItemId == null)
            throw new InvalidOperationException("Select a request first.");

        if (string.Equals(Employees.SelectedItemId, employeeId, StringComparison.Ordinal))
        {
            return;
        }

        Employees.SelectItem(employeeId);
        Feedback = string.Empty;
    }

    public void ClearRequestSelection()
    {
        Requests.ClearSelection();
        ClearEmployeeSelection();
    }

    public void ClearEmployeeSelection()
    {
        Employees.ClearSelection();
        Feedback = string.Empty;
    }

    public void SetFeedback(string text)
    {
        Feedback = text ?? string.Empty;
    }

    private static void ValidateId(string id, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An ID is required.", parameterName);
    }
}