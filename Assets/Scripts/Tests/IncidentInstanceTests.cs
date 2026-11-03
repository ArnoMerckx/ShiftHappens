using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class IncidentInstanceTests
{
    private IncidentDefinition definition;
    private LocationDefinition location;
    private IncidentScheduleEntry schedule;

    [SetUp]
    public void SetUp()
    {
        var original = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I3MissingSupplies.asset");

        Assert.That(original, Is.Not.Null);
        Assert.That(original.Location, Is.Not.Null);

        definition = UnityEngine.Object.Instantiate(original);
        location = UnityEngine.Object.Instantiate(original.Location);

        Edit(definition, data =>
            data.FindProperty("location").objectReferenceValue = location);

        schedule = new IncidentScheduleEntry(
            "D0_I3", "I3", new TimeRange(0, 815, 820), 840, 900);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(definition);
        UnityEngine.Object.DestroyImmediate(location);
    }

    private static void Edit(
        UnityEngine.Object target,
        Action<SerializedObject> change)
    {
        var data = new SerializedObject(target);
        change(data);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void ValidInstance_CopiesContentAndStartsOpen()
    {
        var instance = new IncidentInstance(definition, schedule);

        Assert.That(instance.Schedule, Is.SameAs(schedule));
        Assert.That(instance.InstanceId, Is.EqualTo("D0_I3"));
        Assert.That(instance.IncidentId, Is.EqualTo("I3"));
        Assert.That(instance.DayIndex, Is.EqualTo(0));
        Assert.That(
            instance.Occurrence, Is.EqualTo(new TimeRange(0, 815, 820)));
        Assert.That(instance.DeliveryMinute, Is.EqualTo(840));
        Assert.That(instance.DeadlineMinute, Is.EqualTo(900));
        Assert.That(instance.DisplayName, Is.EqualTo(definition.DisplayName));
        Assert.That(instance.Description, Is.EqualTo(definition.Description));
        Assert.That(instance.LocationId, Is.EqualTo("supplies_area"));
        Assert.That(instance.RelatedItemId, Is.EqualTo("supplies_carton_01"));
        Assert.That(instance.ReviewDelayMinutes, Is.EqualTo(15));
        Assert.That(instance.Status, Is.EqualTo(IncidentStatus.Open));
    }

    [Test]
    public void AuthoringChanges_DoNotRewriteExistingInstance()
    {
        var instance = new IncidentInstance(definition, schedule);
        string originalTitle = instance.DisplayName;
        string originalDescription = instance.Description;

        Edit(definition, data =>
        {
            data.FindProperty("displayName").stringValue = "Changed title";
            data.FindProperty("description").stringValue = "Changed description";
            data.FindProperty("relatedItemId").stringValue = "another_carton";
            data.FindProperty("reviewDelayMinutes").intValue = 20;
        });

        Edit(location, data =>
            data.FindProperty("locationId").stringValue = "another_location");

        Assert.That(instance.DisplayName, Is.EqualTo(originalTitle));
        Assert.That(instance.Description, Is.EqualTo(originalDescription));
        Assert.That(instance.LocationId, Is.EqualTo("supplies_area"));
        Assert.That(instance.RelatedItemId, Is.EqualTo("supplies_carton_01"));
        Assert.That(instance.ReviewDelayMinutes, Is.EqualTo(15));

        var laterInstance = new IncidentInstance(definition, schedule);

        Assert.That(laterInstance.DisplayName, Is.EqualTo("Changed title"));
        Assert.That(
            laterInstance.Description, Is.EqualTo("Changed description"));
        Assert.That(laterInstance.LocationId, Is.EqualTo("another_location"));
        Assert.That(laterInstance.RelatedItemId, Is.EqualTo("another_carton"));
        Assert.That(laterInstance.ReviewDelayMinutes, Is.EqualTo(20));
    }

    [TestCase("displayName")]
    [TestCase("description")]
    [TestCase("relatedItemId")]
    public void WhitespaceContent_IsRejected(string field)
    {
        Edit(definition, data =>
            data.FindProperty(field).stringValue = "   ");

        Assert.Throws<ArgumentException>(() =>
            new IncidentInstance(definition, schedule));
    }

    [Test]
    public void MismatchedTemplateId_IsRejected()
    {
        Edit(definition, data =>
            data.FindProperty("incidentId").stringValue = "I1");

        Assert.Throws<ArgumentException>(() =>
            new IncidentInstance(definition, schedule));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonpositiveReviewDelay_IsRejected(int delay)
    {
        Edit(definition, data =>
            data.FindProperty("reviewDelayMinutes").intValue = delay);

        Assert.Throws<ArgumentException>(() =>
            new IncidentInstance(definition, schedule));
    }

    [Test]
    public void MissingLocation_IsRejected()
    {
        Edit(definition, data =>
            data.FindProperty("location").objectReferenceValue = null);

        Assert.Throws<ArgumentException>(() =>
            new IncidentInstance(definition, schedule));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void BlankLocationId_IsRejected(string id)
    {
        Edit(location, data =>
            data.FindProperty("locationId").stringValue = id);

        Assert.Throws<ArgumentException>(() =>
            new IncidentInstance(definition, schedule));
    }

    [TestCase(null)]
    [TestCase("")]
    public void EmptyOptionalItem_IsAllowed(string itemId)
    {
        Edit(definition, data =>
            data.FindProperty("relatedItemId").stringValue = itemId);

        var instance = new IncidentInstance(definition, schedule);

        Assert.That(instance.RelatedItemId, Is.EqualTo(string.Empty));
    }

    [Test]
    public void NullInputs_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IncidentInstance(null, schedule));

        Assert.Throws<ArgumentNullException>(() =>
            new IncidentInstance(definition, null));
    }
}