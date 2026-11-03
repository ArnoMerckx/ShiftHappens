using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCustodyClaim", menuName = "Shift Happens/Evidence/Claims/Custody")]
public class CustodyClaimDefinition : ClaimDefinition
{
    [SerializeField] private string claimId;
    [SerializeField] private EmployeeDefinition employee;
    [SerializeField] private string itemId;
    [SerializeField] private LocationDefinition location;
    [SerializeField, Min(0)] private int dayIndex;
    [SerializeField, Range(0, 1439)] private int minuteOfDay;

    public override Claim ToClaim()
    {
        if (employee == null)
            throw new InvalidOperationException("A custody claim requires an employee reference.");

        if (location == null)
            throw new InvalidOperationException("A custody claim requires a location reference.");

        return new CustodyClaim(claimId, employee.EmployeeId, itemId, location.LocationId, dayIndex, minuteOfDay);
    }
}