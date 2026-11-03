using System;
using NUnit.Framework;

public class WhereaboutsClaimTests
{
    [TestCase("bob", "printer_area", 630, 645, false)]
    [TestCase("alice", "accounts_office", 565, 590, true)]
    public void ValidClaim_PreservesAssignmentAndRequirement(string employeeId, string locationId, int start, int end, bool requiresPresence)
    {
        var period = new TimeRange(0, start, end);

        var whereabouts = new WhereaboutsClaim(
            "C_TEST", employeeId, locationId,
            period, requiresPresence);

        Claim claim = whereabouts;

        Assert.That(claim.ClaimId, Is.EqualTo("C_TEST"));
        Assert.That(claim.EmployeeId, Is.EqualTo(employeeId));

        Assert.That(
            whereabouts.Assignment.EmployeeId,
            Is.EqualTo(claim.EmployeeId));

        Assert.That(whereabouts.LocationId, Is.EqualTo(locationId));
        Assert.That(whereabouts.Period, Is.EqualTo(period));
        Assert.That(
            whereabouts.RequiresPresence,
            Is.EqualTo(requiresPresence));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void MissingIds_AreRejected(string invalid)
    {
        var period = new TimeRange(0, 630, 645);

        Assert.Throws<ArgumentException>(() =>
            new WhereaboutsClaim(
                invalid, "bob", "printer_area", period, false));

        Assert.Throws<ArgumentException>(() =>
            new WhereaboutsClaim(
                "claim", invalid, "printer_area", period, false));

        Assert.Throws<ArgumentException>(() =>
            new WhereaboutsClaim(
                "claim", "bob", invalid, period, false));
    }

    [Test]
    public void DefaultPeriod_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new WhereaboutsClaim("claim", "bob", "printer_area", default(TimeRange), false));
    }
}