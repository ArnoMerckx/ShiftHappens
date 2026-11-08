using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WindowManagerDragTests : WindowManagementTestBase
{
    private WindowManager Manager(params AppRegistration[] entries)
    {
        return new WindowManager(new AppRegistry(new List<AppRegistration>(entries)),
            new Vector2(1000, 800));
    }

    [Test]
    public void GetWindow_ReturnsExistingStateWithoutChangingFocusOrOrder()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        Assert.That(manager.GetWindow("inwatch"), Is.SameAs(a));
        AssertTwoWindowsUnchanged(manager, a, b);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t")]
    public void GetWindow_BlankIdIsRejectedWithoutLaunching(string id)
    {
        var manager = Manager(Entry("inwatch"));
        Assert.Throws<ArgumentException>(() => manager.GetWindow(id));
        Assert.That(manager.Windows, Is.Empty);
    }

    [TestCase("excess")]
    [TestCase("InWatch")]
    public void GetWindow_UnlaunchedOrWrongCaseIdDoesNotChangeExistingState(string id)
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = manager.Launch("inwatch");
        Assert.Throws<KeyNotFoundException>(() => manager.GetWindow(id));
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
        Assert.That(state.IsFocused, Is.True);
    }

    [Test]
    public void Drag_UpdatesBothBoundsWithoutChangingOtherStateFocusOrderOrDefinition()
    {
        var entry = Entry("inwatch");
        var manager = Manager(entry, Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.DragWindow("inwatch", a.NormalRect, new Vector2(10, 20), new Vector2(80, 110));
        Assert.That(a.CurrentRect, Is.EqualTo(new Rect(110, 130, 480, 360)));
        Assert.That(a.NormalRect, Is.EqualTo(a.CurrentRect));
        Assert.That(b.CurrentRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        AssertTwoWindowsUnchanged(manager, a, b);
        Assert.That(entry.Definition.DefaultPosition, Is.EqualTo(new Vector2(40, 40)));
        Assert.That(entry.Definition.DefaultSize, Is.EqualTo(new Vector2(480, 360)));
        Assert.That(a.IsMinimized || a.IsMaximized, Is.False);
    }

    [Test]
    public void DragToBoundaryThenRelaunch_RetainsMovedBoundsAndSameState()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.DragWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(9000, 9000));
        Assert.That(state.NormalRect, Is.EqualTo(new Rect(520, 440, 480, 360)));
        Assert.That(manager.Launch("inwatch"), Is.SameAs(state));
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(520, 440, 480, 360)));
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedDragSamples_UseSavedStartInsteadOfAccumulatingModelPosition()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect start = state.NormalRect;
        manager.DragWindow("inwatch", start, Vector2.zero, new Vector2(9000, -9000));
        manager.DragWindow("inwatch", start, Vector2.zero, new Vector2(70, 90));
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(110, 130, 480, 360)));
        manager.DragWindow("inwatch", start, Vector2.zero, Vector2.zero);
        Assert.That(state.CurrentRect, Is.EqualTo(start));
        Assert.That(state.NormalRect, Is.EqualTo(start));
    }

    [Test]
    public void MismatchedSavedSize_IsRejectedBeforeBoundsOrFocusChange()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        Rect original = a.NormalRect;
        Rect wrongSize = new Rect(original.position, new Vector2(600, 400));
        Assert.Throws<ArgumentException>(() => manager.DragWindow(
            "inwatch", wrongSize, Vector2.zero, new Vector2(70, 90)));
        Assert.That(a.NormalRect, Is.EqualTo(original));
        Assert.That(a.CurrentRect, Is.EqualTo(original));
        AssertTwoWindowsUnchanged(manager, a, b);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void InvalidPointer_IsRejectedBeforeBoundsOrFocusChange(bool starting)
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        Rect original = a.NormalRect;
        Vector2 invalid = new Vector2(float.NaN, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.DragWindow(
            "inwatch", original, starting ? invalid : Vector2.zero,
            starting ? Vector2.zero : invalid));
        Assert.That(a.NormalRect, Is.EqualTo(original));
        Assert.That(a.CurrentRect, Is.EqualTo(original));
        AssertTwoWindowsUnchanged(manager, a, b);
    }

    [Test]
    public void DragUnlaunchedApp_IsRejectedWithoutOpeningIt()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = manager.Launch("inwatch");
        Assert.Throws<KeyNotFoundException>(() => manager.DragWindow(
            "excess", state.NormalRect, Vector2.zero, Vector2.one));
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
        Assert.That(state.IsFocused, Is.True);
        Assert.That(state.NormalRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
    }

    [Test]
    public void DragBlankId_IsRejectedWithoutCreatingState()
    {
        var manager = Manager(Entry("inwatch"));
        Assert.Throws<ArgumentException>(() => manager.DragWindow(
            " ", new Rect(40, 40, 480, 360), Vector2.zero, Vector2.one));
        Assert.That(manager.Windows, Is.Empty);
    }

    private static void AssertTwoWindowsUnchanged(
        WindowManager manager, WindowState a, WindowState b)
    {
        CollectionAssert.AreEqual(new[] { a, b }, manager.Windows);
        Assert.That(a.IsFocused, Is.False);
        Assert.That(b.IsFocused, Is.True);
        Assert.That(a.IsOpen && b.IsOpen, Is.True);
    }
}
