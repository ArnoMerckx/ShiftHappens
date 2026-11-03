using System;
using NUnit.Framework;

public class CustodyClaimTests
{
    [TestCase(0, 530)]
    [TestCase(0, 0)]
    [TestCase(2, 1439)]
    public void ValidClaim_StoresSharedAndCustodyValues(
        int dayIndex, int minuteOfDay)
    {
        var custody = new CustodyClaim(
            "C_SUPPLIES_HANDOVER",
            "steve",
            "supplies_carton_01",
            "supplies_area",
            dayIndex,
            minuteOfDay);

        Claim claim = custody;

        Assert.That(claim.ClaimId, Is.EqualTo("C_SUPPLIES_HANDOVER"));
        Assert.That(claim.EmployeeId, Is.EqualTo("steve"));
        Assert.That(custody.ItemId, Is.EqualTo("supplies_carton_01"));
        Assert.That(custody.LocationId, Is.EqualTo("supplies_area"));
        Assert.That(custody.DayIndex, Is.EqualTo(dayIndex));
        Assert.That(custody.MinuteOfDay, Is.EqualTo(minuteOfDay));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void MissingIds_AreRejected(string invalid)
    {
        Assert.Throws<ArgumentException>(() => new CustodyClaim(invalid, "steve", "carton", "supplies_area", 0, 530));

        Assert.Throws<ArgumentException>(() => new CustodyClaim("claim", invalid, "carton", "supplies_area", 0, 530));

        Assert.Throws<ArgumentException>(() => new CustodyClaim("claim", "steve", invalid, "supplies_area", 0, 530));

        Assert.Throws<ArgumentException>(() => new CustodyClaim("claim", "steve", "carton", invalid, 0, 530));
    }

    [Test]
    public void NegativeDay_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CustodyClaim("claim", "steve", "carton", "supplies_area", -1, 530));
    }

    [TestCase(-1)]
    [TestCase(1440)]
    public void InvalidMinute_IsRejected(int minuteOfDay)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CustodyClaim("claim", "steve", "carton", "supplies_area", 0, minuteOfDay));
    }
}