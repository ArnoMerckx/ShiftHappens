using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRequest", menuName = "Shift Happens/Delegation/Request")]
public sealed class RequestDefinition : ScriptableObject
{
    [SerializeField] private string requestId;
    [SerializeField] private string displayName;

    [SerializeField, TextArea(3, 8)]
    private string description;

    [SerializeField] private EmployeeDefinition requester;
    [SerializeField] private EmployeeCapability category;
    [SerializeField] private bool allowSelfHelp;

    public RequestData ToData()
    {
        if (requester == null)
            throw new InvalidOperationException("A request requires a requester reference.");

        return new RequestData(requestId, displayName, description, requester.EmployeeId, category, allowSelfHelp);
    }
}