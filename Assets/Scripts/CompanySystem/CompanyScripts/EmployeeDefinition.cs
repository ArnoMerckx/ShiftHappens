using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEmployee", menuName = "Shift Happens/Company/Employee")]
public class EmployeeDefinition : ScriptableObject
{
    [SerializeField] private string employeeId;
    [SerializeField] private string displayName;
    [SerializeField] private EmployeeRole role;

    [SerializeField, TextArea(3, 6)]
    private string bio = string.Empty;

    [SerializeField, TextArea(2, 5)]
    private string interestsAndExperience = string.Empty;

    [SerializeField]
    private List<EmployeeCapability> capabilities = new List<EmployeeCapability>();

    public string EmployeeId => employeeId;
    public string DisplayName => displayName;
    public EmployeeRole Role => role;

    public string Bio => bio;
    public string InterestsAndExperience => interestsAndExperience;

    public IReadOnlyList<EmployeeCapability> Capabilities => capabilities.AsReadOnly();
}