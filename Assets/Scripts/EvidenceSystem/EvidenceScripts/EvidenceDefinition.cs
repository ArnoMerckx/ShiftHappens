using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEvidence", menuName = "Shift Happens/Evidence/Document")]
public class EvidenceDefinition : ScriptableObject
{
    [SerializeField] private string evidenceId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(4, 12)] private string bodyText;
    [SerializeField] private EvidenceSource source;
    [SerializeField, Min(0)] private int availableDayIndex;
    [SerializeField, Range(0, 1439)] private int availableMinute;

    [SerializeField] private List<ClaimDefinition> claimDefinitions = new List<ClaimDefinition>();

    public string EvidenceId => evidenceId;
    public string DisplayName => displayName;
    public string BodyText => bodyText;
    public EvidenceSource Source => source;
    public int AvailableDayIndex => availableDayIndex;
    public int AvailableMinute => availableMinute;
    public IReadOnlyList<Claim> GetClaims()
    {
        if (claimDefinitions == null)
            throw new InvalidOperationException("Document claim definitions cannot be null.");

        var claims = new List<Claim>();

        foreach (var definition in claimDefinitions)
        {
            if (definition == null)
                throw new InvalidOperationException("A document cannot contain a missing claim definition.");

            claims.Add(definition.ToClaim());
        }

        return claims.AsReadOnly();
    }
}