using UnityEngine;

[CreateAssetMenu(fileName = "NewEmployee", menuName = "Shift Happens/Company/Employee")]
public class EmployeeDefinition : ScriptableObject
{
    [SerializeField] private string employeeId;
    [SerializeField] private string displayName;
    [SerializeField] private EmployeeRole role;

    public string EmployeeId => employeeId;
    public string DisplayName => displayName;
    public EmployeeRole Role => role;
}