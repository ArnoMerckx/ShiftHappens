using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewApprovedScheduleRecord", menuName = "Shift Happens/Company/Approved Schedule Record")]
public class ApprovedScheduleRecordDefinition : ScriptableObject
{
    [SerializeField] private string recordId;
    [SerializeField] private EmployeeDefinition employee;
    [SerializeField] private LocationDefinition location;

    [SerializeField, Min(0)] private int dayIndex;
    [SerializeField, Range(0, 1439)] private int startMinute;
    [SerializeField, Range(1, 1440)] private int endMinute;

    [SerializeField, Min(0)] private int priority;
    [SerializeField] private bool requiresPresence;

    public ApprovedScheduleRecord ToRecord()
    {
        if (employee == null)
        {
            throw new InvalidOperationException("An approved schedule record requires an employee reference.");
        }

        if (location == null)
        {
            throw new InvalidOperationException("An approved schedule record requires a location reference.");
        }

        return new ApprovedScheduleRecord(recordId, employee.EmployeeId, location.LocationId, new TimeRange(dayIndex, startMinute, endMinute), priority, requiresPresence);
    }
}