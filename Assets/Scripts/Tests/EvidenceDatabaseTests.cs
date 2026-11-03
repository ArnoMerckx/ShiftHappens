using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EvidenceDatabaseTests
{
    private readonly List<EvidenceDefinition> created =
        new List<EvidenceDefinition>();

    [TearDown]
    public void TearDown()
    {
        foreach (var document in created)
            UnityEngine.Object.DestroyImmediate(document);

        created.Clear();
    }

    private EvidenceDefinition NewDocument(string id = "E_TEST")
    {
        var document =
            ScriptableObject.CreateInstance<EvidenceDefinition>();

        created.Add(document);

        var data = new SerializedObject(document);
        data.FindProperty("evidenceId").stringValue = id;
        data.FindProperty("displayName").stringValue = "Test document";
        data.FindProperty("bodyText").stringValue = "Test body.";
        data.FindProperty("source").intValue = (int)EvidenceSource.InWatch;
        data.FindProperty("availableDayIndex").intValue = 0;
        data.FindProperty("availableMinute").intValue = 540;
        data.ApplyModifiedPropertiesWithoutUndo();

        return document;
    }

    [Test]
    public void AuthoredDocuments_LoadAllSevenAndTheirClaims()
    {
        const string folder =
            "Assets/Scripts/EvidenceSystem/EvidenceObjects/Documents";

        var definitions = new List<EvidenceDefinition>();

        foreach (var guid in AssetDatabase.FindAssets(
            "t:EvidenceDefinition", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            definitions.Add(
                AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(path));
        }

        var database = new EvidenceDatabase(definitions);
        var ids = new List<string>();
        int claimCount = 0;

        foreach (var document in database.All)
        {
            ids.Add(document.EvidenceId);
            claimCount += document.GetClaims().Count;
        }

        Assert.That(database.All.Count, Is.EqualTo(7));
        Assert.That(ids, Is.EquivalentTo(new[]
        {
            "E_SCHEDULE",
            "E_BOB_DENTIST",
            "E_ALICE_MEETING",
            "E_DAVE_PRINTER",
            "E_KAREN_COLLECTION",
            "E_OFFICE_SOCIAL",
            "E_SUPPLIES_RECEIPT"
        }));
        Assert.That(claimCount, Is.EqualTo(12));

        Assert.That(
            database.GetById("E_SUPPLIES_RECEIPT").Source,
            Is.EqualTo(EvidenceSource.Physical));
    }

    [Test]
    public void Membership_IsCopiedAndReadOnly()
    {
        var document = NewDocument();
        var input = new List<EvidenceDefinition> { document };
        var database = new EvidenceDatabase(input);

        input.Clear();

        Assert.That(database.All.Count, Is.EqualTo(1));
        Assert.That(database.All[0], Is.SameAs(document));
        Assert.That(database.GetById("E_TEST"), Is.SameAs(document));

        Assert.Throws<NotSupportedException>(() =>
            ((IList<EvidenceDefinition>)database.All).Clear());
    }

    [Test]
    public void NullInput_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EvidenceDatabase(null));
    }

    [Test]
    public void NullEntry_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new EvidenceDatabase(
                new List<EvidenceDefinition> { null }));
    }

    [Test]
    public void DuplicateEvidenceIds_AreRejected()
    {
        var first = NewDocument();
        var second = NewDocument();

        Assert.Throws<ArgumentException>(() =>
            new EvidenceDatabase(
                new List<EvidenceDefinition> { first, second }));
    }

    [Test]
    public void EmptyDatabase_IsValid()
    {
        var database = new EvidenceDatabase(
            new List<EvidenceDefinition>());

        Assert.That(database.All, Is.Empty);
    }

    [TestCase("missing")]
    [TestCase("e_test")]
    public void UnknownOrCaseMismatchedId_IsRejected(string id)
    {
        var database = new EvidenceDatabase(
            new List<EvidenceDefinition> { NewDocument() });

        Assert.Throws<KeyNotFoundException>(() =>
            database.GetById(id));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankLookupId_IsRejected(string id)
    {
        var database = new EvidenceDatabase(
            new List<EvidenceDefinition>());

        Assert.Throws<ArgumentException>(() =>
            database.GetById(id));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankDocumentId_IsRejected(string id)
    {
        var document = NewDocument(id);

        Assert.Throws<ArgumentException>(() =>
            new EvidenceDatabase(
                new List<EvidenceDefinition> { document }));
    }

    [TestCase("displayName")]
    [TestCase("bodyText")]
    public void MissingDocumentText_IsRejected(string field)
    {
        var document = NewDocument();
        var data = new SerializedObject(document);
        data.FindProperty(field).stringValue = "   ";
        data.ApplyModifiedPropertiesWithoutUndo();

        Assert.Throws<ArgumentException>(() =>
            new EvidenceDatabase(
                new List<EvidenceDefinition> { document }));
    }

    [TestCase("source", 0)]
    [TestCase("source", 999)]
    [TestCase("availableDayIndex", -1)]
    [TestCase("availableMinute", -1)]
    [TestCase("availableMinute", 1440)]
    public void InvalidMetadata_IsRejected(string field, int value)
    {
        var document = NewDocument();
        var data = new SerializedObject(document);
        data.FindProperty(field).intValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();

        Assert.Throws<ArgumentException>(() =>
            new EvidenceDatabase(
                new List<EvidenceDefinition> { document }));
    }

    private static void SetClaimDefinitions(
        EvidenceDefinition document,
        List<ClaimDefinition> definitions)
    {
        var data = new SerializedObject(document);
        var references = data.FindProperty("claimDefinitions");
        references.arraySize = definitions.Count;

        for (int i = 0; i < definitions.Count; i++)
        {
            references.GetArrayElementAtIndex(i).objectReferenceValue =
                definitions[i];
        }

        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void MissingClaimDefinition_IsRejected()
    {
        var document = NewDocument();

        SetClaimDefinitions(
            document, new List<ClaimDefinition> { null });

        Assert.Throws<InvalidOperationException>(() =>
            new EvidenceDatabase(
                new List<EvidenceDefinition> { document }));
    }

    [Test]
    public void RepeatedClaimWithinDocument_IsRejected()
    {
        var claim = AssetDatabase.LoadAssetAtPath<ClaimDefinition>(
            "Assets/Scripts/EvidenceSystem/EvidenceObjects/Claims/C_SUPPLIES_HANDOVER.asset");

        Assert.That(claim, Is.Not.Null);

        var document = NewDocument();
        SetClaimDefinitions(
            document, new List<ClaimDefinition> { claim, claim });

        Assert.Throws<ArgumentException>(() =>
            new EvidenceDatabase(
                new List<EvidenceDefinition> { document }));
    }

    [Test]
    public void SharedClaimAcrossDocuments_IsAllowed()
    {
        var claim = AssetDatabase.LoadAssetAtPath<ClaimDefinition>(
            "Assets/Scripts/EvidenceSystem/EvidenceObjects/Claims/C_SUPPLIES_HANDOVER.asset");

        Assert.That(claim, Is.Not.Null);

        var first = NewDocument("E_FIRST");
        var second = NewDocument("E_SECOND");

        SetClaimDefinitions(first, new List<ClaimDefinition> { claim });
        SetClaimDefinitions(second, new List<ClaimDefinition> { claim });

        var database = new EvidenceDatabase(
            new List<EvidenceDefinition> { first, second });

        Assert.That(database.All.Count, Is.EqualTo(2));
        Assert.That(
            database.GetById("E_FIRST").GetClaims()[0].ClaimId,
            Is.EqualTo("C_SUPPLIES_HANDOVER"));
        Assert.That(
            database.GetById("E_SECOND").GetClaims()[0].ClaimId,
            Is.EqualTo("C_SUPPLIES_HANDOVER"));
    }
}