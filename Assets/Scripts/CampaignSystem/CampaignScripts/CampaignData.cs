using System;
using System.Collections.Generic;

public sealed class CampaignData
{
    public string CampaignId { get; }
    public string ContentId { get; }
    public IReadOnlyList<DayData> Days { get; }

    public CampaignData(string campaignId, string contentId, List<DayData> days)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("A campaign ID is required.", nameof(campaignId));

        if (string.IsNullOrWhiteSpace(contentId))
            throw new ArgumentException("A content ID is required.", nameof(contentId));

        if (days == null)
            throw new ArgumentNullException(nameof(days));

        if (days.Count == 0)
            throw new ArgumentException("A campaign needs at least one normal day.", nameof(days));

        var copy = new List<DayData>();
        var dayIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> roster = null;
        int previousDayIndex = -1;

        foreach (DayData day in days)
        {
            if (day == null)
                throw new ArgumentException("Campaign days cannot contain null.", nameof(days));

            WorkdaySettings settings = day.ScheduleData.Settings;

            if (!dayIds.Add(settings.DayId))
                throw new ArgumentException($"Duplicate campaign day ID: {settings.DayId}", nameof(days));

            if (settings.DayIndex <= previousDayIndex)
                throw new ArgumentException("Campaign day indices must increase in authored order.", nameof(days));

            if (roster == null)
            {
                roster = new HashSet<string>(day.ScheduleData.EmployeeIds, StringComparer.Ordinal);
            }
            else if (!roster.SetEquals(day.ScheduleData.EmployeeIds))
            {
                throw new ArgumentException("Campaign days must use the same employee IDs.", nameof(days));
            }

            copy.Add(day);
            previousDayIndex = settings.DayIndex;
        }

        CampaignId = campaignId;
        ContentId = contentId;
        Days = copy.AsReadOnly();
    }

    public DayData GetDay(string dayId)
    {
        return Days[FindDayIndex(dayId)];
    }

    public bool TryGetNextDay(string currentDayId, out DayData nextDay)
    {
        int nextIndex = FindDayIndex(currentDayId) + 1;

        nextDay = nextIndex < Days.Count ? Days[nextIndex] : null;
        return nextDay != null;
    }

    private int FindDayIndex(string dayId)
    {
        if (string.IsNullOrWhiteSpace(dayId))
            throw new ArgumentException("A day ID is required.", nameof(dayId));

        for (int i = 0; i < Days.Count; i++)
        {
            if (string.Equals(Days[i].ScheduleData.Settings.DayId, dayId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new KeyNotFoundException($"Unknown campaign day: {dayId}");
    }
}