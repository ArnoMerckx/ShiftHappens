using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EvidenceClaimConversionTests
{
    private WhereaboutsClaimDefinition whereabouts;
    private CustodyClaimDefinition custody;
    private EvidenceDefinition document;

    [SetUp]
    public void SetUp()
    {
        whereabouts = ScriptableObject.CreateInstance<WhereaboutsClaimDefinition>();
        custody = ScriptableObject.CreateInstance<CustodyClaimDefinition>();
        document = ScriptableObject.CreateInstance<EvidenceDefinition>();

        ConfigureIdentity(whereabouts, "C_WHERE");
        ConfigureIdentity(custody, "C_CUSTODY");

        Edit(whereabouts, data =>
        {
            data.FindProperty("startMinute").intValue = 630;
            data.FindProperty("endMinute").intValue = 645;
            data.FindProperty("requiresPresence").boolValue = true;
        });

        Edit(custody, data =>
        {
            data.FindProperty("itemId").stringValue = "test_carton";
            data.FindProperty("minuteOfDay").intValue = 530;
        });

        Edit(document, data =>
        {
            var definitions = data.FindProperty("claimDefinitions");
            definitions.arraySize = 2;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = whereabouts;
            definitions.GetArrayElementAtIndex(1).objectReferenceValue = custody;
        });
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(document);
        UnityEngine.Object.DestroyImmediate(custody);
        UnityEngine.Object.DestroyImmediate(whereabouts);
    }

    private static void Edit(
        UnityEngine.Object target,
        Action<SerializedObject> change)
    {
        var data = new SerializedObject(target);
        change(data);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureIdentity(
        ClaimDefinition definition, string claimId)
    {
        var employee = AssetDatabase.LoadAssetAtPath<EmployeeDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Employees/Bob.asset");

        var location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Locations/PrinterArea.asset");

        Assert.That(employee, Is.Not.Null);
        Assert.That(location, Is.Not.Null);

        Edit(definition, data =>
        {
            data.FindProperty("claimId").stringValue = claimId;
            data.FindProperty("employee").objectReferenceValue = employee;
            data.FindProperty("location").objectReferenceValue = location;
            data.FindProperty("dayIndex").intValue = 0;
        });
    }

    [Test]
    public void MixedClaims_ReturnProtectedSnapshots()
    {
        var claims = document.GetClaims();

        Assert.That(claims.Count, Is.EqualTo(2));
        Assert.That(claims[0], Is.TypeOf<WhereaboutsClaim>());
        Assert.That(claims[1], Is.TypeOf<CustodyClaim>());

        var where = (WhereaboutsClaim)claims[0];
        var held = (CustodyClaim)claims[1];

        Assert.That(where.ClaimId, Is.EqualTo("C_WHERE"));
        Assert.That(where.EmployeeId, Is.EqualTo("bob"));
        Assert.That(where.LocationId, Is.EqualTo("printer_area"));
        Assert.That(where.Period, Is.EqualTo(new TimeRange(0, 630, 645)));
        Assert.That(where.RequiresPresence, Is.True);

        Assert.That(held.ClaimId, Is.EqualTo("C_CUSTODY"));
        Assert.That(held.EmployeeId, Is.EqualTo("bob"));
        Assert.That(held.LocationId, Is.EqualTo("printer_area"));
        Assert.That(held.ItemId, Is.EqualTo("test_carton"));
        Assert.That(held.DayIndex, Is.EqualTo(0));
        Assert.That(held.MinuteOfDay, Is.EqualTo(530));

        Assert.Throws<NotSupportedException>(() =>
            ((IList<Claim>)claims).Clear());

        Edit(custody, data =>
            data.FindProperty("itemId").stringValue = "changed_carton");

        Edit(document, data =>
            data.FindProperty("claimDefinitions").arraySize = 0);

        Assert.That(claims.Count, Is.EqualTo(2));
        Assert.That(held.ItemId, Is.EqualTo("test_carton"));
        Assert.That(document.GetClaims(), Is.Empty);
        Assert.That(
            ((CustodyClaim)custody.ToClaim()).ItemId,
            Is.EqualTo("changed_carton"));
    }

    [TestCase("employee")]
    [TestCase("location")]
    public void MissingReferences_AreRejected(string field)
    {
        Edit(whereabouts, data =>
            data.FindProperty(field).objectReferenceValue = null);

        Edit(custody, data =>
            data.FindProperty(field).objectReferenceValue = null);

        Assert.Throws<InvalidOperationException>(() =>
            whereabouts.ToClaim());

        Assert.Throws<InvalidOperationException>(() =>
            custody.ToClaim());
    }

    [Test]
    public void InvalidWhereaboutsPeriod_IsRejected()
    {
        Edit(whereabouts, data =>
            data.FindProperty("endMinute").intValue = 630);

        Assert.Throws<ArgumentException>(() => document.GetClaims());
    }

    [Test]
    public void InvalidCustodyTimestamp_IsRejected()
    {
        Edit(custody, data =>
            data.FindProperty("minuteOfDay").intValue = 1440);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            document.GetClaims());
    }

    [Test]
    public void EmptyDocument_ReturnsNoClaims()
    {
        Edit(document, data =>
            data.FindProperty("claimDefinitions").arraySize = 0);

        Assert.That(document.GetClaims(), Is.Empty);
    }

    [Test]
    public void MissingDefinition_IsRejected()
    {
        Edit(document, data =>
            data.FindProperty("claimDefinitions")
                .GetArrayElementAtIndex(0).objectReferenceValue = null);

        Assert.Throws<InvalidOperationException>(() =>
            document.GetClaims());
    }
}