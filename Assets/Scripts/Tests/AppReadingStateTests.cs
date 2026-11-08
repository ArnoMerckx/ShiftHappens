using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class AppReadingStateTests
{
    [Test]
    public void NewState_HasNoSelectionAndBothScrollPositionsAtTop()
    {
        var state = new AppReadingState();
        Assert.That(state.SelectedItemId, Is.Null);
        Assert.That(state.ListScrollPosition, Is.EqualTo(1f));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(1f));
    }

    [Test]
    public void FirstSelection_StartsReaderAtTopWithoutChangingListPosition()
    {
        var state = new AppReadingState();
        state.SetListScrollPosition(0.25f);
        state.SelectItem("DOCUMENT");
        Assert.That(state.SelectedItemId, Is.EqualTo("DOCUMENT"));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(1f));
        Assert.That(state.ListScrollPosition, Is.EqualTo(0.25f));
    }

    [Test]
    public void SwitchingItems_RetainsEachReaderPositionIndependently()
    {
        var state = new AppReadingState();
        state.SelectItem("FIRST");
        state.SetReaderScrollPosition(0.25f);
        state.SelectItem("SECOND");
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(1f));
        state.SetReaderScrollPosition(0.75f);
        state.SelectItem("FIRST");
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.25f));
        state.SelectItem("SECOND");
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.75f));
    }

    [Test]
    public void SelectingSameItem_DoesNotResetItsReadingPosition()
    {
        var state = SelectedState();
        state.SelectItem("DOCUMENT");
        AssertSelectedState(state);
    }

    [Test]
    public void ClearSelection_LeavesListPositionAndPerItemHistoryIntact()
    {
        var state = SelectedState();
        state.ClearSelection();
        Assert.That(state.SelectedItemId, Is.Null);
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(1f));
        Assert.That(state.ListScrollPosition, Is.EqualTo(0.25f));
        state.SelectItem("DOCUMENT");
        AssertSelectedState(state);
    }

    [Test]
    public void RepeatedClear_IsSafeWithNoSelection()
    {
        var state = new AppReadingState();
        state.ClearSelection();
        state.ClearSelection();
        Assert.That(state.SelectedItemId, Is.Null);
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(1f));
        Assert.That(state.ListScrollPosition, Is.EqualTo(1f));
    }

    [Test]
    public void ItemKeys_AreOrdinalAndCaseSensitive()
    {
        var state = new AppReadingState();
        state.SelectItem("DOCUMENT");
        state.SetReaderScrollPosition(0.25f);
        state.SelectItem("document");
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(1f));
        state.SetReaderScrollPosition(0.75f);
        state.SelectItem("DOCUMENT");
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.25f));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t")]
    public void BlankSelection_IsRejectedWithoutChangingSavedState(string id)
    {
        var state = SelectedState();
        Assert.Throws<ArgumentException>(() => state.SelectItem(id));
        AssertSelectedState(state);
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(1f)]
    public void ValidListPositions_AreStoredExactlyWithoutChangingReader(float position)
    {
        var state = SelectedState();
        state.SetListScrollPosition(position);
        Assert.That(state.ListScrollPosition, Is.EqualTo(position));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.75f));
        Assert.That(state.SelectedItemId, Is.EqualTo("DOCUMENT"));
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(1f)]
    public void ValidReaderPositions_AreStoredExactlyWithoutChangingList(float position)
    {
        var state = SelectedState();
        state.SetReaderScrollPosition(position);
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(position));
        Assert.That(state.ListScrollPosition, Is.EqualTo(0.25f));
        Assert.That(state.SelectedItemId, Is.EqualTo("DOCUMENT"));
    }

    [TestCase(-0.01f)]
    [TestCase(1.01f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void InvalidListPosition_IsRejectedWithoutChangingState(float position)
    {
        var state = SelectedState();
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => state.SetListScrollPosition(position));
        Assert.That(error.ParamName, Is.EqualTo("position"));
        AssertSelectedState(state);
    }

    [TestCase(-0.01f)]
    [TestCase(1.01f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void InvalidReaderPosition_IsRejectedWithoutChangingState(float position)
    {
        var state = SelectedState();
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => state.SetReaderScrollPosition(position));
        Assert.That(error.ParamName, Is.EqualTo("position"));
        AssertSelectedState(state);
    }

    [Test]
    public void SavingReaderPositionWithoutSelection_IsRejected()
    {
        var state = new AppReadingState();
        Assert.Throws<InvalidOperationException>(() => state.SetReaderScrollPosition(0.5f));
        Assert.That(state.SelectedItemId, Is.Null);
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(1f));
        Assert.That(state.ListScrollPosition, Is.EqualTo(1f));
    }

    [Test]
    public void ListPosition_CanBeSavedWithoutSelectingAnItem()
    {
        var state = new AppReadingState();
        state.SetListScrollPosition(0.5f);
        Assert.That(state.SelectedItemId, Is.Null);
        Assert.That(state.ListScrollPosition, Is.EqualTo(0.5f));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(1f));
    }

    [Test]
    public void ReaderAndListPositions_DoNotOverwriteEachOtherAcrossSelections()
    {
        var state = SelectedState();
        state.SetListScrollPosition(0.5f);
        state.SelectItem("OTHER");
        state.SetReaderScrollPosition(0.1f);
        state.SelectItem("DOCUMENT");
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.75f));
        Assert.That(state.ListScrollPosition, Is.EqualTo(0.5f));
    }

    [Test]
    public void PublicState_ExposesOnlySelectionAndScrollValuesWithoutPublicSetters()
    {
        var names = new List<string>();
        foreach (var property in typeof(AppReadingState).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            names.Add(property.Name);
            Assert.That(property.GetSetMethod(), Is.Null);
            Assert.That(property.PropertyType == typeof(string) || property.PropertyType == typeof(float), Is.True);
        }
        CollectionAssert.AreEquivalent(new[] { "SelectedItemId", "ListScrollPosition", "ReaderScrollPosition" }, names);
        Assert.That(typeof(AppReadingState).GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
    }

    [Test]
    public void SelectingFutureEvidenceId_DoesNotGrantAvailabilityDiscoveryReadingOrAttachment()
    {
        var document = ScriptableObject.CreateInstance<EvidenceDefinition>();
        try
        {
            SetField(document, "evidenceId", "FUTURE");
            SetField(document, "displayName", "Future document");
            SetField(document, "bodyText", "Authored future text");
            SetField(document, "source", EvidenceSource.InWatch);
            SetField(document, "availableMinute", 600);
            var service = new EvidenceService(new EvidenceDatabase(new List<EvidenceDefinition> { document }));
            var state = new AppReadingState();
            state.SelectItem("FUTURE");
            state.SetListScrollPosition(0.25f);
            state.SetReaderScrollPosition(0.5f);
            Assert.That(service.GetAvailableDigitalDocuments(EvidenceSource.InWatch, 0, 540), Is.Empty);
            Assert.That(service.IsDiscovered("FUTURE") || service.IsRead("FUTURE"), Is.False);
            Assert.That(service.GetAttachableDocuments(0, 540), Is.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(document);
        }
    }

    private static AppReadingState SelectedState()
    {
        var state = new AppReadingState();
        state.SelectItem("DOCUMENT");
        state.SetListScrollPosition(0.25f);
        state.SetReaderScrollPosition(0.75f);
        return state;
    }

    private static void AssertSelectedState(AppReadingState state)
    {
        Assert.That(state.SelectedItemId, Is.EqualTo("DOCUMENT"));
        Assert.That(state.ListScrollPosition, Is.EqualTo(0.25f));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(0.75f));
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing serialized field: " + name);
        field.SetValue(target, value);
    }
}
