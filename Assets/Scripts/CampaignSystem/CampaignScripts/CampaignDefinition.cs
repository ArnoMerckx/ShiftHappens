using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Campaign", menuName = "Shift Happens/Campaign/Campaign")]
public sealed class CampaignDefinition : ScriptableObject
{
    [SerializeField] private string campaignId;
    [SerializeField] private string contentId;

    [SerializeField]
    private List<DayDefinition> days = new List<DayDefinition>();

    public CampaignData GetData()
    {
        if (days == null)
            throw new InvalidOperationException("Campaign days cannot be null.");

        var dayData = new List<DayData>();

        foreach (DayDefinition day in days)
        {
            if (day == null)
                throw new InvalidOperationException("Campaign days cannot contain missing references.");

            dayData.Add(day.GetData());
        }

        return new CampaignData(campaignId, contentId, dayData);
    }
}