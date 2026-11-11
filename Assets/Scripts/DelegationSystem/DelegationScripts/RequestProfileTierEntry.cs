using System;
using UnityEngine;

[Serializable]
public sealed class RequestProfileTierEntry
{
    [SerializeField] private EmployeeDefinition employee;
    [SerializeField, Min(0)] private int tier;

    public RequestSuitabilityEntry ToEntry(EmployeeCapability requiredCapability)
    {
        if (employee == null)
            throw new InvalidOperationException("A ranked entry requires an employee reference.");

        bool hasCapability = false;

        foreach (var capability in employee.Capabilities)
        {
            if (capability == requiredCapability)
            {
                hasCapability = true;
                break;
            }
        }

        if (!hasCapability)
            throw new InvalidOperationException($"{employee.DisplayName} lacks the required capability.");

        return new RequestSuitabilityEntry(employee.EmployeeId, tier);
    }
}