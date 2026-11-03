using System;

public sealed class CustodyClaim : Claim
{
    public string ItemId { get; }
    public string LocationId { get; }
    public int DayIndex { get; }
    public int MinuteOfDay { get; }

    public CustodyClaim(string claimId, string employeeId, string itemId, string locationId, int dayIndex, int minuteOfDay) : base(claimId, employeeId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("Item ID is required.", nameof(itemId));

        if (string.IsNullOrWhiteSpace(locationId))
            throw new ArgumentException("Location ID is required.", nameof(locationId));

        if (dayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(dayIndex));

        if (minuteOfDay < 0 || minuteOfDay >= 1440)
            throw new ArgumentOutOfRangeException(nameof(minuteOfDay));

        ItemId = itemId;
        LocationId = locationId;
        DayIndex = dayIndex;
        MinuteOfDay = minuteOfDay;
    }
}