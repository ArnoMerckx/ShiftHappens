using UnityEngine;

[CreateAssetMenu(fileName = "New Incident", menuName = "Shift Happens/Incidents/Incident")]
public class IncidentDefinition : ScriptableObject
{
    [SerializeField] private string incidentId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(4, 12)] private string description;
    [SerializeField] private LocationDefinition location;
    [SerializeField] private string relatedItemId;
    [SerializeField, Min(1)] private int reviewDelayMinutes = 10;

    public string IncidentId => incidentId;
    public string DisplayName => displayName;
    public string Description => description;
    public LocationDefinition Location => location;
    public string RelatedItemId => relatedItemId;
    public int ReviewDelayMinutes => reviewDelayMinutes;
}