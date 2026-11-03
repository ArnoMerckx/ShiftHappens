using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewWhereaboutsClaim", menuName = "Shift Happens/Evidence/Claims/Whereabouts")]
public class WhereaboutsClaimDefinition : ClaimDefinition
{
    [SerializeField] private string claimId;
    [SerializeField] private EmployeeDefinition employee;
    [SerializeField] private LocationDefinition location;
    [SerializeField, Min(0)] private int dayIndex;
    [SerializeField, Range(0, 1439)] private int startMinute;
    [SerializeField, Range(1, 1440)] private int endMinute;
    [SerializeField] private bool requiresPresence;

    public override Claim ToClaim()
    {
        if (employee == null)
            throw new InvalidOperationException("A whereabouts claim requires an employee reference.");

        if (location == null)
            throw new InvalidOperationException("A whereabouts claim requires a location reference.");

        return new WhereaboutsClaim(claimId, employee.EmployeeId, location.LocationId, new TimeRange(dayIndex, startMinute, endMinute), requiresPresence);
    }
}