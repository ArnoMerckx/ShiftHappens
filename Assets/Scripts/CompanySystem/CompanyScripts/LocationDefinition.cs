using UnityEngine;

[CreateAssetMenu(fileName = "New Location", menuName = "Shift Happens/Company/Location")]
public class LocationDefinition : ScriptableObject
{
    [SerializeField] private string locationId;
    [SerializeField] private string displayName;

    public string LocationId => locationId;
    public string DisplayName => displayName;
}