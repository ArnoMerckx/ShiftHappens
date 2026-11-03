public sealed class WhereaboutsClaim : Claim
{
    public PublishedScheduleEntry Assignment { get; }
    public bool RequiresPresence { get; }

    public string LocationId => Assignment.LocationId;
    public TimeRange Period => Assignment.Period;

    public WhereaboutsClaim(string claimId, string employeeId, string locationId, TimeRange period, bool requiresPresence) : base(claimId, employeeId)
    {
        Assignment = new PublishedScheduleEntry(employeeId, locationId, period);

        RequiresPresence = requiresPresence;
    }
}