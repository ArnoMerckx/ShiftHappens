using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public abstract class EvidencePresentationTestBase
{
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

    protected T Own<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        owned.Add(value);
        return value;
    }

    protected EvidenceDefinition Document(string id, EvidenceSource source = EvidenceSource.InWatch,
        int day = 0, int minute = 540)
    {
        var document = Own<EvidenceDefinition>();
        SetField(document, "evidenceId", id);
        SetField(document, "displayName", "Title " + id);
        SetField(document, "bodyText", "Authored text " + id);
        SetField(document, "source", source);
        SetField(document, "availableDayIndex", day);
        SetField(document, "availableMinute", minute);
        return document;
    }

    protected static EvidenceService Service(params EvidenceDefinition[] documents)
    {
        return new EvidenceService(new EvidenceDatabase(new List<EvidenceDefinition>(documents)));
    }

    protected static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing serialized field: " + name);
        field.SetValue(target, value);
    }

    protected void AddClaim(EvidenceDefinition document, string claimId)
    {
        var employee = Own<EmployeeDefinition>();
        SetField(employee, "employeeId", "steve");
        var location = Own<LocationDefinition>();
        SetField(location, "locationId", "document_claim_place");
        var claim = Own<WhereaboutsClaimDefinition>();
        SetField(claim, "claimId", claimId);
        SetField(claim, "employee", employee);
        SetField(claim, "location", location);
        SetField(claim, "dayIndex", 0);
        SetField(claim, "startMinute", 540);
        SetField(claim, "endMinute", 600);
        SetField(claim, "requiresPresence", true);
        SetField(document, "claimDefinitions", new List<ClaimDefinition> { claim });
    }

    protected static List<string> Ids(List<EvidenceDocumentViewData> documents)
    {
        var ids = new List<string>();
        foreach (var document in documents) ids.Add(document.EvidenceId);
        return ids;
    }

    [TearDown]
    public void DestroyTemporaryDocuments()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
}

public class EvidencePresentationTests : EvidencePresentationTestBase
{
    [TestCase(EvidenceSource.InWatch)]
    [TestCase(EvidenceSource.Excess)]
    [TestCase(EvidenceSource.ShareLess)]
    public void DigitalQuery_FiltersSourceAndFutureDocuments(EvidenceSource source)
    {
        var documents = new List<EvidenceDefinition>();
        foreach (EvidenceSource channel in new[] { EvidenceSource.InWatch, EvidenceSource.Excess,
            EvidenceSource.ShareLess, EvidenceSource.Physical })
            documents.Add(Document(channel.ToString(), channel));
        documents.Add(Document("FUTURE", source, 0, 600));
        var service = new EvidenceService(new EvidenceDatabase(documents));
        CollectionAssert.AreEqual(new[] { source.ToString() },
            Ids(service.GetAvailableDigitalDocuments(source, 0, 540)));
    }

    [TestCase(0, 1439, false)]
    [TestCase(1, 599, false)]
    [TestCase(1, 600, true)]
    [TestCase(1, 601, true)]
    [TestCase(2, 0, true)]
    public void DigitalAvailability_UsesInclusiveDayAndMinute(int day, int minute, bool expected)
    {
        var service = Service(Document("FUTURE", EvidenceSource.InWatch, 1, 600));
        Assert.That(service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, day, minute).Count,
            Is.EqualTo(expected ? 1 : 0));
        Assert.That(service.IsDiscovered("FUTURE") || service.IsRead("FUTURE"), Is.False);
    }

    [TestCase(0, 1439, false)]
    [TestCase(1, 599, false)]
    [TestCase(1, 600, true)]
    [TestCase(1, 601, true)]
    [TestCase(2, 0, true)]
    public void AttachmentAvailability_StillFiltersAnAlreadyDiscoveredDocument(
        int day, int minute, bool expected)
    {
        var service = Service(Document("FUTURE", EvidenceSource.Physical, 1, 600));
        service.MarkDiscovered("FUTURE", 1, 600);
        Assert.That(service.GetAttachableDocuments(day, minute).Count, Is.EqualTo(expected ? 1 : 0));
        Assert.That(service.IsDiscovered("FUTURE"), Is.True);
        Assert.That(service.IsRead("FUTURE"), Is.False);
    }

    [TestCase(EvidenceSource.None)]
    [TestCase(EvidenceSource.Physical)]
    [TestCase((EvidenceSource)(-1))]
    [TestCase((EvidenceSource)999)]
    public void UnsupportedDigitalSource_IsRejectedEvenForEmptyDatabase(EvidenceSource source)
    {
        var service = Service();
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.GetAvailableDigitalDocuments(source, 0, 540));
        Assert.That(error.ParamName, Is.EqualTo("source"));
    }

    [TestCase(EvidenceSource.InWatch)]
    [TestCase(EvidenceSource.Excess)]
    [TestCase(EvidenceSource.ShareLess)]
    public void EmptyDatabase_ReturnsEmptyListsForSupportedSources(EvidenceSource source)
    {
        var service = Service();
        Assert.That(service.GetAvailableDigitalDocuments(source, 0, 540), Is.Empty);
        Assert.That(service.GetAttachableDocuments(0, 540), Is.Empty);
    }

    [TestCase(true, -1, 540)]
    [TestCase(true, 0, -1)]
    [TestCase(true, 0, 1440)]
    [TestCase(false, -1, 540)]
    [TestCase(false, 0, -1)]
    [TestCase(false, 0, 1440)]
    public void BothQueries_ValidateTimeBeforeEnumeratingOrChangingState(bool empty, int day, int minute)
    {
        var service = empty ? Service() : Service(Document("KNOWN"));
        if (!empty) service.MarkDiscovered("KNOWN", 0, 540);
        var digitalError = Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, day, minute));
        var attachError = Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.GetAttachableDocuments(day, minute));
        string parameter = day < 0 ? "dayIndex" : "minuteOfDay";
        Assert.That(digitalError.ParamName, Is.EqualTo(parameter));
        Assert.That(attachError.ParamName, Is.EqualTo(parameter));
        if (!empty)
        {
            Assert.That(service.IsDiscovered("KNOWN"), Is.True);
            Assert.That(service.IsRead("KNOWN"), Is.False);
        }
    }

    [Test]
    public void RepeatedQueries_DoNotDiscoverReadOrMakeAvailableDocumentsAttachable()
    {
        var service = Service(Document("EMAIL"), Document("RECEIPT", EvidenceSource.Physical));
        for (int i = 0; i < 3; i++)
        {
            var rows = service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540);
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].IsDiscovered || rows[0].IsRead, Is.False);
            Assert.That(service.GetAttachableDocuments(0, 540), Is.Empty);
        }
        foreach (string id in new[] { "EMAIL", "RECEIPT" })
        {
            Assert.That(service.IsDiscovered(id) || service.IsRead(id), Is.False);
            Assert.That(service.CanAttach(id, 0, 540), Is.False);
        }
    }

    [Test]
    public void Filtering_PreservesAuthoredOrderInsteadOfDiscoveryOrder()
    {
        var service = Service(Document("EMAIL_B"), Document("SCHEDULE", EvidenceSource.Excess),
            Document("EMAIL_A"), Document("RECEIPT", EvidenceSource.Physical));
        CollectionAssert.AreEqual(new[] { "EMAIL_B", "EMAIL_A" },
            Ids(service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540)));
        foreach (string id in new[] { "RECEIPT", "EMAIL_A", "SCHEDULE", "EMAIL_B" })
            service.MarkDiscovered(id, 0, 540);
        CollectionAssert.AreEqual(new[] { "EMAIL_B", "SCHEDULE", "EMAIL_A", "RECEIPT" },
            Ids(service.GetAttachableDocuments(0, 540)));
    }

    [Test]
    public void PhysicalReceipt_IsAbsentFromDigitalListsAndAppearsWithSameIdTextAfterDiscovery()
    {
        var receipt = Document("E_SUPPLIES_RECEIPT", EvidenceSource.Physical);
        var service = Service(receipt);
        foreach (var source in new[] { EvidenceSource.InWatch, EvidenceSource.Excess, EvidenceSource.ShareLess })
            Assert.That(service.GetAvailableDigitalDocuments(source, 0, 540), Is.Empty);
        Assert.That(service.GetAttachableDocuments(0, 540), Is.Empty);
        service.MarkDiscovered(receipt.EvidenceId, 0, 540);
        var rows = service.GetAttachableDocuments(0, 540);
        Assert.That(rows.Count, Is.EqualTo(1));
        Assert.That(rows[0].EvidenceId, Is.EqualTo("E_SUPPLIES_RECEIPT"));
        Assert.That(rows[0].DisplayName, Is.EqualTo(receipt.DisplayName));
        Assert.That(rows[0].BodyText, Is.EqualTo(receipt.BodyText));
        Assert.That(rows[0].Source, Is.EqualTo(EvidenceSource.Physical));
        Assert.That(rows[0].IsDiscovered, Is.True);
        Assert.That(rows[0].IsRead, Is.False);
    }

    [Test]
    public void DiscoveredUnreadDigitalDocument_RemainsAttachableWithoutQueryMarkingItRead()
    {
        var service = Service(Document("EMAIL"));
        service.MarkDiscovered("EMAIL", 0, 540);
        var row = service.GetAttachableDocuments(0, 540)[0];
        Assert.That(row.IsDiscovered, Is.True);
        Assert.That(row.IsRead, Is.False);
        Assert.That(service.IsRead("EMAIL"), Is.False);
    }

    [Test]
    public void ExistingSnapshots_KeepTheirOldFlagsWhenServiceStateChanges()
    {
        var service = Service(Document("EMAIL"));
        var old = service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540)[0];
        service.MarkRead("EMAIL", 0, 540);
        var fresh = service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540)[0];
        Assert.That(old.IsRead || old.IsDiscovered, Is.False);
        Assert.That(fresh.IsRead && fresh.IsDiscovered, Is.True);
    }

    [Test]
    public void SnapshotText_DoesNotFollowLaterAuthoredTextChanges()
    {
        var document = Document("EMAIL");
        var service = Service(document);
        var old = service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540)[0];
        SetField(document, "displayName", "Changed title");
        SetField(document, "bodyText", "Changed authored text");
        var fresh = service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540)[0];
        Assert.That(old.DisplayName, Is.EqualTo("Title EMAIL"));
        Assert.That(old.BodyText, Is.EqualTo("Authored text EMAIL"));
        Assert.That(fresh.DisplayName, Is.EqualTo("Changed title"));
        Assert.That(fresh.BodyText, Is.EqualTo("Changed authored text"));
    }

    [Test]
    public void ReturnedLists_AreIndependentOfServiceMembershipAndEachOther()
    {
        var service = Service(Document("EMAIL"));
        service.MarkRead("EMAIL", 0, 540);
        var digital = service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540);
        var attachments = service.GetAttachableDocuments(0, 540);
        digital.Clear();
        attachments.Clear();
        CollectionAssert.AreEqual(new[] { "EMAIL" },
            Ids(service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540)));
        CollectionAssert.AreEqual(new[] { "EMAIL" }, Ids(service.GetAttachableDocuments(0, 540)));
    }

    [Test]
    public void Snapshot_SurvivesDestructionOfItsSourceAsset()
    {
        var document = Document("EMAIL");
        var service = Service(document);
        var row = service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540)[0];
        UnityEngine.Object.DestroyImmediate(document);
        Assert.That(row.EvidenceId, Is.EqualTo("EMAIL"));
        Assert.That(row.DisplayName, Is.EqualTo("Title EMAIL"));
        Assert.That(row.BodyText, Is.EqualTo("Authored text EMAIL"));
        Assert.That(row.Source, Is.EqualTo(EvidenceSource.InWatch));
    }

    [Test]
    public void PublicSnapshotApi_ExposesOnlyApprovedReadOnlyPresentationValues()
    {
        Type type = typeof(EvidenceDocumentViewData);
        var names = new List<string>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            names.Add(property.Name);
            Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
            Assert.That(property.PropertyType == typeof(string) || property.PropertyType == typeof(bool) ||
                property.PropertyType == typeof(EvidenceSource), Is.True, property.Name);
        }
        CollectionAssert.AreEquivalent(new[] { "EvidenceId", "DisplayName", "BodyText", "Source",
            "IsDiscovered", "IsRead" }, names);
        Assert.That(type.GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
        Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(type), Is.False);
    }

    [TestCase(EvidenceSource.InWatch)]
    [TestCase(EvidenceSource.Excess)]
    [TestCase(EvidenceSource.ShareLess)]
    [TestCase(EvidenceSource.Physical)]
    public void ReadDocumentFromAnySource_UsesSameAttachmentSnapshotRoute(EvidenceSource source)
    {
        var service = Service(Document("DOCUMENT", source));
        service.MarkRead("DOCUMENT", 0, 540);
        var row = service.GetAttachableDocuments(0, 540)[0];
        Assert.That(row.EvidenceId, Is.EqualTo("DOCUMENT"));
        Assert.That(row.Source, Is.EqualTo(source));
        Assert.That(row.IsRead && row.IsDiscovered, Is.True);
    }
}
