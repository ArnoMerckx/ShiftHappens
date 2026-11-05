using System;
using System.Collections.Generic;

public sealed class DayData
{
    public DayScheduleData ScheduleData { get; }
    public EvidenceDatabase EvidenceDatabase { get; }
    public IReadOnlyList<DayIncidentPlan> Incidents { get; }

    public DayData(DayScheduleData scheduleData, List<EvidenceDefinition> evidence, List<DayIncidentPlan> incidents)
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

            incidentCopy.Add(plan);
        }

        ScheduleData = scheduleData;
        EvidenceDatabase = database;
        Incidents = incidentCopy.AsReadOnly();
    }
}