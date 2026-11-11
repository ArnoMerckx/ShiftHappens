using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRequestProfile", menuName = "Shift Happens/Delegation/Request Profile")]
public sealed class RequestProfileDefinition : ScriptableObject
{
    [SerializeField] private string profileId;
    [SerializeField] private EmployeeCapability requiredCapability;

    [SerializeField]
    private List<RequestProfileTierEntry> rankedEmployees = new List<RequestProfileTierEntry>();

    [SerializeField]
    private List<EmployeeDefinition> unsuitableEmployees = new List<EmployeeDefinition>();

    public RequestProfileData ToData()
    {
        if (rankedEmployees == null || unsuitableEmployees == null)
            throw new InvalidOperationException("Profile employee collections cannot be null.");

        var ranked = new List<RequestSuitabilityEntry>();

        foreach (var entry in rankedEmployees)
        {
            if (entry == null)
                throw new InvalidOperationException("Ranked employees cannot contain missing entries.");

            ranked.Add(entry.ToEntry(requiredCapability));
        }

        var unsuitableIds = new List<string>();

        foreach (var employee in unsuitableEmployees)
        {
            if (employee == null)
                throw new InvalidOperationException("Unsuitable employees cannot contain missing references.");

            unsuitableIds.Add(employee.EmployeeId);
        }

        return new RequestProfileData(profileId, requiredCapability, ranked, unsuitableIds);
    }
}