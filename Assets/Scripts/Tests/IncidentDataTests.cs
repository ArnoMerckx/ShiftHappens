using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class IncidentDataTests
{
    private const string Folder =
        "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents";

    [TestCase(
        "I1_CoffeeSpill", "I1", "break_room",
        575, 580, 590, 630, 10, "")]
    [TestCase(
        "I2_PrinterDamage", "I2", "printer_area",
        635, 640, 675, 720, 15, "")]
    [TestCase(
        "I3MissingSupplies", "I3", "supplies_area",
        815, 820, 840, 900, 15, "supplies_carton_01")]
    public void AuthoredCase_HasExpectedContentAndSchedule(
        string filename, string id, string locationId,
        int start, int end, int delivery, int deadline,
        int reviewDelay, string itemId)
    {
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            $"{Folder}/{filename}.asset");

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.IncidentId, Is.EqualTo(id));
        Assert.That(
            string.IsNullOrWhiteSpace(definition.DisplayName), Is.False);
        Assert.That(
            string.IsNullOrWhiteSpace(definition.Description), Is.False);
        Assert.That(definition.Location, Is.Not.Null);
        Assert.That(definition.Location.LocationId, Is.EqualTo(locationId));
        Assert.That(definition.RelatedItemId ?? "", Is.EqualTo(itemId));
        Assert.That(definition.ReviewDelayMinutes, Is.EqualTo(reviewDelay));

        var occurrence = new TimeRange(0, start, end);
        var entry = new IncidentScheduleEntry(
            $"D0_{id}", id, occurrence, delivery, deadline);

        Assert.That(entry.InstanceId, Is.EqualTo($"D0_{id}"));
        Assert.That(entry.IncidentId, Is.EqualTo(definition.IncidentId));
        Assert.That(entry.DayIndex, Is.EqualTo(0));
        Assert.That(entry.Occurrence, Is.EqualTo(occurrence));
        Assert.That(entry.DeliveryMinute, Is.EqualTo(delivery));
        Assert.That(entry.DeadlineMinute, Is.EqualTo(deadline));
    }

    [Test]
    public void AuthoredTemplates_HaveUniqueExpectedIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var guid in AssetDatabase.FindAssets(
            "t:IncidentDefinition", new[] { Folder }))
        {
            var definition =
                AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));

            Assert.That(ids.Add(definition.IncidentId), Is.True);
        }

        Assert.That(ids, Is.EquivalentTo(new[] { "I1", "I2", "I3" }));
    }

    [Test]
    public void MissingSupplies_ItemMatchesReceipt()
    {
        var incident = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            $"{Folder}/I3MissingSupplies.asset");

        var receipt = AssetDatabase.LoadAssetAtPath<CustodyClaimDefinition>(
            "Assets/Scripts/EvidenceSystem/EvidenceObjects/Claims/C_SUPPLIES_HANDOVER.asset");

        Assert.That(incident, Is.Not.Null);
        Assert.That(receipt, Is.Not.Null);

        var custody = (CustodyClaim)receipt.ToClaim();

        Assert.That(incident.RelatedItemId, Is.EqualTo(custody.ItemId));
    }

    [Test]
    public void DeliveryAtOccurrenceEnd_IsAllowed()
    {
        var entry = new IncidentScheduleEntry(
            "instance", "template",
            new TimeRange(3, 570, 580), 580, 581);

        Assert.That(entry.DayIndex, Is.EqualTo(3));
        Assert.That(entry.DeliveryMinute, Is.EqualTo(580));
        Assert.That(entry.DeadlineMinute, Is.EqualTo(581));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankIds_AreRejected(string invalid)
    {
        var occurrence = new TimeRange(0, 575, 580);

        Assert.Throws<ArgumentException>(() =>
            new IncidentScheduleEntry(
                invalid, "I1", occurrence, 590, 630));

        Assert.Throws<ArgumentException>(() =>
            new IncidentScheduleEntry(
                "D0_I1", invalid, occurrence, 590, 630));
    }

    [Test]
    public void DefaultOccurrence_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new IncidentScheduleEntry(
                "D0_I1", "I1", default(TimeRange), 590, 630));
    }

    [TestCase(true, -1)]
    [TestCase(true, 1440)]
    [TestCase(false, -1)]
    [TestCase(false, 1440)]
    public void InvalidTimestamp_IsRejected(
        bool invalidDelivery, int value)
    {
        int delivery = invalidDelivery ? value : 590;
        int deadline = invalidDelivery ? 630 : value;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IncidentScheduleEntry(
                "D0_I1", "I1", new TimeRange(0, 575, 580),
                delivery, deadline));
    }

    [TestCase(579, 630)]
    [TestCase(590, 590)]
    [TestCase(590, 589)]
    public void InvalidOrdering_IsRejected(int delivery, int deadline)
    {
        Assert.Throws<ArgumentException>(() =>
            new IncidentScheduleEntry(
                "D0_I1", "I1", new TimeRange(0, 575, 580),
                delivery, deadline));
    }

    [Test]
    public void LastCurrentDayTimestamps_AreAllowed()
    {
        var entry = new IncidentScheduleEntry(
            "late_instance", "template",
            new TimeRange(0, 1437, 1438), 1438, 1439);

        Assert.That(entry.DeadlineMinute, Is.EqualTo(1439));
    }
}