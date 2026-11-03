using System;
using System.Collections.Generic;

public sealed class EvidenceService
{
    private readonly EvidenceDatabase database;
    private readonly HashSet<string> discoveredIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> readIds = new HashSet<string>(StringComparer.Ordinal);

    public EvidenceService(EvidenceDatabase database)
    {
        if (database == null)
            throw new ArgumentNullException(nameof(database));

        this.database = database;
    }

    public bool IsAvailable(string evidenceId, int dayIndex, int minuteOfDay)
    {
        if (dayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(dayIndex));

        if (minuteOfDay < 0 || minuteOfDay >= 1440)
            throw new ArgumentOutOfRangeException(nameof(minuteOfDay));

        var definition = database.GetById(evidenceId);

        return dayIndex > definition.AvailableDayIndex || (dayIndex == definition.AvailableDayIndex && minuteOfDay >= definition.AvailableMinute);
    }

    public bool MarkDiscovered(
    string evidenceId, int dayIndex, int minuteOfDay)
    {
        if (!IsAvailable(evidenceId, dayIndex, minuteOfDay))
            return false;

        return discoveredIds.Add(evidenceId);
    }

    public bool MarkRead(string evidenceId, int dayIndex, int minuteOfDay)
    {
        if (!IsAvailable(evidenceId, dayIndex, minuteOfDay))
            return false;

        discoveredIds.Add(evidenceId);
        return readIds.Add(evidenceId);
    }

    public bool IsDiscovered(string evidenceId)
    {
        database.GetById(evidenceId);
        return discoveredIds.Contains(evidenceId);
    }

    public bool IsRead(string evidenceId)
    {
        database.GetById(evidenceId);
        return readIds.Contains(evidenceId);
    }

    public bool CanAttach(string evidenceId, int dayIndex, int minuteOfDay)
    {
        return IsAvailable(evidenceId, dayIndex, minuteOfDay)&& discoveredIds.Contains(evidenceId);
    }
}