using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EvidenceServiceTests
{
    private EvidenceDefinition document;
    private EvidenceDatabase database;

    private EvidenceService NewService(
        EvidenceSource source = EvidenceSource.InWatch,
        int availableDay = 0)
    {
        document =
            ScriptableObject.CreateInstance<EvidenceDefinition>();

        var data = new SerializedObject(document);
        data.FindProperty("evidenceId").stringValue = "E_TEST";
        data.FindProperty("displayName").stringValue = "Test document";
        data.FindProperty("bodyText").stringValue = "Test body.";
        data.FindProperty("source").intValue = (int)source;
        data.FindProperty("availableDayIndex").intValue = availableDay;
        data.FindProperty("availableMinute").intValue = 540;
        data.ApplyModifiedPropertiesWithoutUndo();

        database = new EvidenceDatabase(
            new List<EvidenceDefinition> { document });

        return new EvidenceService(database);
    }

    [TearDown]
    public void TearDown()
    {
        if (document != null)
            UnityEngine.Object.DestroyImmediate(document);
    }

    [TestCase(0, 1439, false)]
    [TestCase(1, 539, false)]
    [TestCase(1, 540, true)]
    [TestCase(1, 541, true)]
    [TestCase(2, 0, true)]
    public void Availability_UsesDayAndInclusiveMinute(
        int day, int minute, bool expected)
    {
        var service = NewService(availableDay: 1);

        Assert.That(
            service.IsAvailable("E_TEST", day, minute),
            Is.EqualTo(expected));

        Assert.That(service.IsDiscovered("E_TEST"), Is.False);
    }

    [TestCase(EvidenceSource.InWatch)]
    [TestCase(EvidenceSource.Excess)]
    [TestCase(EvidenceSource.ShareLess)]
    [TestCase(EvidenceSource.Physical)]
    public void DiscoveryLifecycle_IsSharedAcrossSources(
        EvidenceSource source)
    {
        var service = NewService(source);

        Assert.That(service.MarkDiscovered("E_TEST", 0, 539), Is.False);
        Assert.That(service.MarkRead("E_TEST", 0, 539), Is.False);
        Assert.That(service.IsDiscovered("E_TEST"), Is.False);
        Assert.That(service.IsRead("E_TEST"), Is.False);
        Assert.That(service.CanAttach("E_TEST", 0, 540), Is.False);

        Assert.That(service.MarkDiscovered("E_TEST", 0, 540), Is.True);
        Assert.That(service.MarkDiscovered("E_TEST", 0, 540), Is.False);
        Assert.That(service.IsDiscovered("E_TEST"), Is.True);
        Assert.That(service.IsRead("E_TEST"), Is.False);
        Assert.That(service.CanAttach("E_TEST", 0, 540), Is.True);

        Assert.That(service.MarkRead("E_TEST", 0, 540), Is.True);
        Assert.That(service.MarkRead("E_TEST", 0, 540), Is.False);
        Assert.That(service.IsRead("E_TEST"), Is.True);
        Assert.That(service.CanAttach("E_TEST", 0, 540), Is.True);
    }

    [Test]
    public void ReadingDirectly_AlsoDiscovers()
    {
        var service = NewService(EvidenceSource.Physical);

        Assert.That(service.MarkRead("E_TEST", 0, 540), Is.True);
        Assert.That(service.IsDiscovered("E_TEST"), Is.True);
        Assert.That(service.IsRead("E_TEST"), Is.True);
        Assert.That(service.CanAttach("E_TEST", 0, 540), Is.True);
    }

    [Test]
    public void Sessions_SharingDatabaseHaveIndependentState()
    {
        var first = NewService();
        var second = new EvidenceService(database);

        first.MarkRead("E_TEST", 0, 540);

        Assert.That(first.CanAttach("E_TEST", 0, 540), Is.True);
        Assert.That(second.IsDiscovered("E_TEST"), Is.False);
        Assert.That(second.IsRead("E_TEST"), Is.False);
        Assert.That(second.CanAttach("E_TEST", 0, 540), Is.False);
    }

    [TestCase(-1, 540)]
    [TestCase(0, -1)]
    [TestCase(0, 1440)]
    public void InvalidTime_IsRejectedWithoutChangingState(
        int day, int minute)
    {
        var service = NewService();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.IsAvailable("E_TEST", day, minute));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.MarkDiscovered("E_TEST", day, minute));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.MarkRead("E_TEST", day, minute));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.CanAttach("E_TEST", day, minute));

        Assert.That(service.IsDiscovered("E_TEST"), Is.False);
        Assert.That(service.IsRead("E_TEST"), Is.False);
    }

    [TestCase(null, typeof(ArgumentException))]
    [TestCase("", typeof(ArgumentException))]
    [TestCase("   ", typeof(ArgumentException))]
    [TestCase("missing", typeof(KeyNotFoundException))]
    [TestCase("e_test", typeof(KeyNotFoundException))]
    public void InvalidId_IsRejectedEverywhere(
        string id, Type expectedException)
    {
        var service = NewService();

        Assert.Throws(expectedException, () =>
            service.IsAvailable(id, 0, 540));

        Assert.Throws(expectedException, () =>
            service.MarkDiscovered(id, 0, 540));

        Assert.Throws(expectedException, () =>
            service.MarkRead(id, 0, 540));

        Assert.Throws(expectedException, () =>
            service.CanAttach(id, 0, 540));

        Assert.Throws(expectedException, () =>
            service.IsDiscovered(id));

        Assert.Throws(expectedException, () =>
            service.IsRead(id));

        Assert.That(service.IsDiscovered("E_TEST"), Is.False);
    }

    [Test]
    public void NullDatabase_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EvidenceService(null));
    }
}