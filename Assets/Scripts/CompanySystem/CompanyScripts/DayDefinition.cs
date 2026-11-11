using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "New Day", menuName = "Shift Happens/Company/Day")]
public class DayDefinition : ScriptableObject
{
    [SerializeField] private string dayId;
    [SerializeField] private string displayName;
    [SerializeField, Min(0)] private int dayIndex;

    [SerializeField, Range(0, 1439)]
    private int startMinute = 540;

    [SerializeField, Range(1, 1440)]
    private int endMinute = 1020;

    [SerializeField, Min(0.001f)]
    private double gameSecondsPerRealSecond = 20;

    [SerializeField]
    private List<EmployeeDefinition> employees = new List<EmployeeDefinition>();

    [SerializeField] 
    private List<DayAvailabilityEntry> ordinaryAvailability = new List<DayAvailabilityEntry>();

    [SerializeField]
    private List<EvidenceDefinition> evidence = new List<EvidenceDefinition>();

    [SerializeField]
    private List<ApprovedScheduleRecordDefinition> worldApprovedRecords = new List<ApprovedScheduleRecordDefinition>();

    [SerializeField]
    private List<ApprovedScheduleRecordDefinition> hrApprovedRecords = new List<ApprovedScheduleRecordDefinition>();

    [SerializeField]
    private List<ClaimDefinition> hrKnownClaims = new List<ClaimDefinition>();

    [SerializeField]
    private List<DayIncidentEntry> incidents = new List<DayIncidentEntry>();

    [SerializeField]
    private List<DayRequestEntry> requests = new List<DayRequestEntry>();

    public string DayId => dayId;
    public string DisplayName => displayName;
    public int DayIndex => dayIndex;
    public int StartMinute => startMinute;
    public int EndMinute => endMinute;
    public double GameSecondsPerRealSecond => gameSecondsPerRealSecond;

    public WorkdaySettings GetSettings()
    {
        return new WorkdaySettings(dayId, displayName, dayIndex, startMinute, endMinute, gameSecondsPerRealSecond);
    }

    public DayScheduleData GetScheduleData()
    {
        WorkdaySettings settings = GetSettings();

        if (employees == null || worldApprovedRecords == null || hrApprovedRecords == null || hrKnownClaims == null || ordinaryAvailability == null)
        {
            throw new InvalidOperationException("Day schedule collections cannot be null.");
        }

        var employeeIds = new List<string>();
        var employeePresentation = new List<EmployeeViewData>();

        foreach (var employee in employees)
        {
            if (employee == null)
            {
                throw new InvalidOperationException("Day employees cannot contain missing references.");
            }

            employeeIds.Add(employee.EmployeeId);
            employeePresentation.Add(new EmployeeViewData(employee.EmployeeId, employee.DisplayName, employee.Role, employee.Bio, employee.InterestsAndExperience));
        }

        var worldRecords = new List<ApprovedScheduleRecord>();

        var recordsByDefinition =new Dictionary<ApprovedScheduleRecordDefinition, ApprovedScheduleRecord>();

        foreach (var definition in worldApprovedRecords)
        {
            if (definition == null)
            {
                throw new InvalidOperationException("World records cannot contain missing references.");
            }

            if (recordsByDefinition.ContainsKey(definition))
            {
                throw new InvalidOperationException("A world record definition is listed more than once.");
            }

            ApprovedScheduleRecord record = definition.ToRecord();

            recordsByDefinition.Add(definition, record);
            worldRecords.Add(record);
        }

        var hrRecords = new List<ApprovedScheduleRecord>();

        foreach (var definition in hrApprovedRecords)
        {
            if (definition == null || !recordsByDefinition.TryGetValue(definition, out var record))
            {
                throw new InvalidOperationException("HR-approved definitions must be selected from world definitions.");
            }

            hrRecords.Add(record);
        }

        var knownClaims = new List<Claim>();

        foreach (var definition in hrKnownClaims)
        {
            if (definition == null)
            {
                throw new InvalidOperationException("HR-known claims cannot contain missing references.");
            }

            knownClaims.Add(definition.ToClaim());
        }
        var availabilityRecords = new List<EmployeeAvailability>();

        foreach (var entry in ordinaryAvailability)
        {
            if (entry == null)
                throw new InvalidOperationException("Day availability cannot contain missing entries.");

            availabilityRecords.Add(entry.ToAvailability(settings.DayIndex));
        }

        return new DayScheduleData(settings, employeeIds, worldRecords, hrRecords, knownClaims, employeePresentation, availabilityRecords);
    }

    public DayData GetData()
    {
        DayScheduleData scheduleData = GetScheduleData();

        if (incidents == null || requests == null)
        {
            throw new InvalidOperationException("Day incident and request collections cannot be null.");
        }

        var plans = new List<DayIncidentPlan>();

        foreach (var entry in incidents)
        {
            if (entry == null)
            {
                throw new InvalidOperationException("Day incidents cannot contain missing entries.");
            }

            plans.Add(entry.ToPlan(scheduleData.Settings.DayIndex));
        }

        var requestPlans = new List<DayRequestPlan>();

        foreach (var entry in requests)
        {
            if (entry == null)
                throw new InvalidOperationException("Day requests cannot contain missing entries.");

            requestPlans.Add(entry.ToPlan(scheduleData.Settings.DayIndex));
        }

        return new DayData(scheduleData, evidence, plans, requestPlans);
    }
}