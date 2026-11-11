using System;
using UnityEngine;

[Serializable]
public sealed class DayAvailabilityEntry
{
    [SerializeField] private EmployeeDefinition employee;

    [SerializeField, Range(0, 1439)]
    private int startMinute = 540;

    [SerializeField, Range(1, 1440)]
    private int endMinute = 1020;

    public EmployeeAvailability ToAvailability(int dayIndex)
    {
        if (employee == null)
            throw new InvalidOperationException("An availability entry requires an employee reference.");

        return new EmployeeAvailability(employee.EmployeeId, new TimeRange(dayIndex, startMinute, endMinute));
    }
}