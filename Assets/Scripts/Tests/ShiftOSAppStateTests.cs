using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ShiftOSAppStateTests
{
    private static List<AppReadingState> States(ShiftOSAppState state)
    {
        return new List<AppReadingState> { state.InWatch, state.Excess, state.Swarms, state.ShareLess };
    }

    [Test]
    public void NewStore_HasFourDistinctInitiallyEmptyReadingStates()
    {
        var states = States(new ShiftOSAppState());
        for (int i = 0; i < states.Count; i++)
        {
            Assert.That(states[i], Is.Not.Null);
            AssertDefault(states[i]);
            for (int j = i + 1; j < states.Count; j++)
                Assert.That(states[i], Is.Not.SameAs(states[j]));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ChangingOneApp_DoesNotChangeOtherApps(int selected)
    {
        var states = States(new ShiftOSAppState());
        states[selected].SelectItem("SAME_ITEM_ID");
        states[selected].SetListScrollPosition(0.25f);
        states[selected].SetReaderScrollPosition(0.75f);
        for (int i = 0; i < states.Count; i++)
            if (i != selected) AssertDefault(states[i]);
    }

    [Test]
    public void SameItemIdInDifferentApps_HasIndependentHistory()
    {
        var state = new ShiftOSAppState();
        state.InWatch.SelectItem("SAME");
        state.Excess.SelectItem("SAME");
        state.InWatch.SetReaderScrollPosition(0.25f);
        state.Excess.SetReaderScrollPosition(0.75f);
        state.InWatch.ClearSelection();
        state.InWatch.SelectItem("SAME");
        Assert.That(state.InWatch.ReaderScrollPosition, Is.EqualTo(0.25f));
        Assert.That(state.Excess.ReaderScrollPosition, Is.EqualTo(0.75f));
    }

    [Test]
    public void SeparateStores_HaveIndependentAppStatesAndHistories()
    {
        var first = States(new ShiftOSAppState());
        var second = States(new ShiftOSAppState());
        for (int i = 0; i < first.Count; i++)
        {
            first[i].SelectItem("ITEM");
            first[i].SetListScrollPosition(0.25f);
            first[i].SetReaderScrollPosition(0.75f);
            Assert.That(first[i], Is.Not.SameAs(second[i]));
            AssertDefault(second[i]);
        }
    }

    [Test]
    public void PersistentControllers_OwnOneIndependentStateWithoutCreatingWindows()
    {
        var firstHost = new GameObject("Temporary first app owner");
        var secondHost = new GameObject("Temporary second app owner");
        firstHost.SetActive(false);
        secondHost.SetActive(false);
        try
        {
            var first = firstHost.AddComponent<WindowManagerController>();
            var second = secondHost.AddComponent<WindowManagerController>();
            ShiftOSAppState saved = first.AppState;
            saved.InWatch.SelectItem("DOCUMENT");
            saved.InWatch.SetReaderScrollPosition(0.25f);
            Assert.That(first.AppState, Is.SameAs(saved));
            Assert.That(first.AppState.InWatch.SelectedItemId, Is.EqualTo("DOCUMENT"));
            Assert.That(first.AppState.InWatch.ReaderScrollPosition, Is.EqualTo(0.25f));
            Assert.That(second.AppState, Is.Not.SameAs(saved));
            AssertDefault(second.AppState.InWatch);
            Assert.That(first.GetOpenWindows(), Is.Empty);
            Assert.That(second.GetOpenWindows(), Is.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstHost);
            UnityEngine.Object.DestroyImmediate(secondHost);
        }
    }

    [Test]
    public void AppOwnershipProperties_HaveNoSetters()
    {
        var owner = typeof(WindowManagerController).GetProperty("AppState");
        Assert.That(owner, Is.Not.Null);
        Assert.That(owner.GetSetMethod(true), Is.Null);
        foreach (string name in new[] { "InWatch", "Excess", "Swarms", "ShareLess" })
        {
            var property = typeof(ShiftOSAppState).GetProperty(name);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.GetSetMethod(true), Is.Null);
        }
    }

    private static void AssertDefault(AppReadingState state)
    {
        Assert.That(state.SelectedItemId, Is.Null);
        Assert.That(state.ListScrollPosition, Is.EqualTo(1f));
        Assert.That(state.ReaderScrollPosition, Is.EqualTo(1f));
    }
}
