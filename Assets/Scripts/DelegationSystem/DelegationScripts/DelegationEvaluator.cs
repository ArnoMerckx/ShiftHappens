using System;

public static class DelegationEvaluator
{
    public static DelegationOutcome Evaluate(DayRequestPlan plan, string employeeId, GameTime submittedAt, DayScheduleData day)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        if (day == null)
            throw new ArgumentNullException(nameof(day));

        long submitted = submittedAt.TotalMilliseconds;

        if (plan.DeliveryAt.DayIndex != day.Settings.DayIndex || submitted < plan.DeliveryAt.TotalMilliseconds || submitted >= day.Settings.ClosesAt.TotalMilliseconds || (plan.CutoffAt.HasValue && submitted > plan.CutoffAt.Value.TotalMilliseconds))
        {
            throw new ArgumentException("Evaluation requires a valid submission timestamp.", nameof(submittedAt));
        }

        if (!plan.Request.AllowSelfHelp && string.Equals(employeeId, plan.Request.RequesterId, StringComparison.Ordinal))
        {
            throw new ArgumentException("This request does not allow self-help.", nameof(employeeId));
        }

        if (!IsAvailable(plan, employeeId, submittedAt, day))
            return DelegationOutcome.Unavailable;

        int? selectedTier = null;

        foreach (var entry in plan.Profile.RankedEmployees)
        {
            if (string.Equals(entry.EmployeeId, employeeId, StringComparison.Ordinal))
            {
                selectedTier = entry.Tier;
                break;
            }
        }

        if (!selectedTier.HasValue)
            return DelegationOutcome.Unsuitable;

        foreach (var entry in plan.Profile.RankedEmployees)
        {
            if (entry.Tier >= selectedTier.Value)
                continue;

            if (!plan.Request.AllowSelfHelp && string.Equals(entry.EmployeeId, plan.Request.RequesterId, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsAvailable(plan, entry.EmployeeId, submittedAt, day))
                return DelegationOutcome.CapableAlternative;
        }

        return DelegationOutcome.BestAvailable;
    }

    public static DelegationDecision EvaluateDecision(DayRequestPlan plan, string employeeId, GameTime submittedAt, DayScheduleData day)
    {
        DelegationOutcome outcome = Evaluate(plan, employeeId, submittedAt, day);

        string helperName = employeeId;

        foreach (var employee in day.EmployeeRoster)
        {
            if (string.Equals(employee.EmployeeId, employeeId, StringComparison.Ordinal))
            {
                helperName = employee.DisplayName;
                break;
            }
        }

        string explanation = BuildExplanation(outcome, helperName, plan.IsFixedWindow);

        return new DelegationDecision(plan.InstanceId, employeeId, submittedAt, outcome, explanation);
    }

    private static string BuildExplanation(DelegationOutcome outcome, string helperName, bool fixedWindow)
    {
        switch (outcome)
        {
            case DelegationOutcome.BestAvailable:
            case DelegationOutcome.CapableAlternative:
                return $"{helperName} has suitable skills and availability. Help is arranged.";

            case DelegationOutcome.Unsuitable:
                return $"{helperName} is not suited to this request. Suitable help could not be arranged.";

            case DelegationOutcome.Unavailable:
                return fixedWindow ? $"{helperName} cannot cover the full requested help window. Help could not be arranged." : $"{helperName} has no confirmed availability between submission and shift end. Help could not be arranged.";

            default:
                throw new ArgumentOutOfRangeException(nameof(outcome));
        }
    }

    private static bool IsAvailable(DayRequestPlan plan, string employeeId, GameTime submittedAt, DayScheduleData day)
    {
        return plan.IsFixedWindow ? DelegationAvailability.CoversWindow(employeeId, plan.HelpWindow.Value, day) : DelegationAvailability.HasAvailabilityBeforeClosing(employeeId, submittedAt, day);
    }
}