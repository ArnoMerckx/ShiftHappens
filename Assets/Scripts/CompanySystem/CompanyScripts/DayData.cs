using System;
using System.Collections.Generic;

public sealed class DayData
{
    public DayScheduleData ScheduleData { get; }
    public EvidenceDatabase EvidenceDatabase { get; }
    public IReadOnlyList<DayIncidentPlan> Incidents { get; }

    public IReadOnlyList<DayRequestPlan> Requests { get; }

    public DayData(DayScheduleData scheduleData, List<EvidenceDefinition> evidence, List<DayIncidentPlan> incidents, List<DayRequestPlan> requests = null)
    {
        if (scheduleData == null)
            throw new ArgumentNullException(nameof(scheduleData));

        if (incidents == null)
            throw new ArgumentNullException(nameof(incidents));

        var database = new EvidenceDatabase(evidence);

        var employeeIds = new HashSet<string>(scheduleData.EmployeeIds, StringComparer.Ordinal);

        foreach (var document in database.All)
        {
            foreach (var claim in document.GetClaims())
            {
                if (!employeeIds.Contains(claim.EmployeeId))
                {
                    throw new ArgumentException($"Unknown employee in document claim: {claim.ClaimId}", nameof(evidence));
                }
            }
        }

        var incidentCopy = new List<DayIncidentPlan>();
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        WorkdaySettings settings = scheduleData.Settings;

        foreach (var plan in incidents)
        {
            if (plan == null)
            {
                throw new ArgumentException("Incident plans cannot contain null.", nameof(incidents));
            }

            IncidentScheduleEntry schedule = plan.Schedule;

            if (!instanceIds.Add(schedule.InstanceId))
            {
                throw new ArgumentException($"Duplicate incident instance ID: {schedule.InstanceId}", nameof(incidents));
            }

            if (schedule.DayIndex != settings.DayIndex)
            {
                throw new ArgumentException("Incident plans must belong to this day.", nameof(incidents));
            }

            if (schedule.DeliveryMinute < settings.WorkHours.StartMinute || schedule.DeliveryMinute >= settings.WorkHours.EndMinute || schedule.DeadlineMinute >= settings.WorkHours.EndMinute)
            {
                throw new ArgumentException("Delivery must occur during work hours and the deadline must precede closing.", nameof(incidents));
            }

            if (string.IsNullOrWhiteSpace(plan.ActualCulpritId) || !employeeIds.Contains(plan.ActualCulpritId))
            {
                throw new ArgumentException("Every incident requires an actual culprit from the day's roster.", nameof(incidents));
            }

            incidentCopy.Add(plan);
        }

        ScheduleData = scheduleData;
        EvidenceDatabase = database;
        Incidents = incidentCopy.AsReadOnly();
        Requests = BuildRequests(requests, scheduleData).AsReadOnly();
    }

    internal string GetActualCulpritId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("An incident instance ID is required.", nameof(instanceId));

        foreach (var plan in Incidents)
        {
            if (string.Equals(plan.Schedule.InstanceId, instanceId, StringComparison.Ordinal))
            {
                return plan.ActualCulpritId;
            }
        }

        throw new KeyNotFoundException($"Unknown incident instance: {instanceId}");
    }

    private static List<DayRequestPlan> BuildRequests(List<DayRequestPlan> requests, DayScheduleData scheduleData)
    {
        var copy = new List<DayRequestPlan>();

        if (requests == null)
            return copy;

        WorkdaySettings settings = scheduleData.Settings;
        var employeeIds = new HashSet<string>(scheduleData.EmployeeIds, StringComparer.Ordinal);
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var plan in requests)
        {
            if (plan == null)
                throw new ArgumentException("Request plans cannot contain null.", nameof(requests));

            if (!instanceIds.Add(plan.InstanceId))
                throw new ArgumentException($"Duplicate request instance ID: {plan.InstanceId}", nameof(requests));

            if (!employeeIds.Contains(plan.Request.RequesterId))
                throw new ArgumentException($"Unknown requester: {plan.Request.RequesterId}", nameof(requests));

            if (plan.DeliveryAt.DayIndex != settings.DayIndex || plan.DeliveryAt.TotalMilliseconds < settings.OpensAt.TotalMilliseconds || plan.DeliveryAt.TotalMilliseconds >= settings.ClosesAt.TotalMilliseconds)
            {
                throw new ArgumentException("Request delivery must occur during this workday.", nameof(requests));
            }

            var profileEmployees = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in plan.Profile.RankedEmployees)
                profileEmployees.Add(entry.EmployeeId);

            profileEmployees.UnionWith(plan.Profile.UnsuitableEmployeeIds);

            if (!profileEmployees.SetEquals(employeeIds))
                throw new ArgumentException("A request profile must classify exactly the day's roster.", nameof(requests));

            if (plan.HelpWindow.HasValue)
            {
                TimeRange window = plan.HelpWindow.Value;

                if (window.StartMinute < settings.WorkHours.StartMinute || window.EndMinute > settings.WorkHours.EndMinute || plan.CutoffAt.Value.TotalMilliseconds >= settings.ClosesAt.TotalMilliseconds)
                {
                    throw new ArgumentException("The help window must fit work hours and its cutoff must precede closing.", nameof(requests));
                }
            }

            copy.Add(plan);
        }

        return copy;
    }
}