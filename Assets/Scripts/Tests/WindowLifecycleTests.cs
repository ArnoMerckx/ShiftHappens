using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WindowLifecycleTests : WindowManagementTestBase
{
    private WindowManager Manager(params AppRegistration[] entries)
    {
        return new WindowManager(new AppRegistry(new List<AppRegistration>(entries)),
            new Vector2(1000, 800));
    }

    [Test]
    public void MinimizeOnlyWindow_KeepsItOpenAndListedButClearsFocus()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        manager.MinimizeWindow("inwatch");
        Assert.That(state.IsOpen && state.IsMinimized, Is.True);
        Assert.That(state.IsFocused || state.IsMaximized, Is.False);
        Assert.That(manager.GetWindow("inwatch"), Is.SameAs(state));
        CollectionAssert.AreEqual(new[] { state }, manager.Windows);
        AssertBounds(state, normal, normal);
    }

    [Test]
    public void MinimizeBackgroundWindow_PreservesFocusedWindowAndOrder()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.MinimizeWindow("inwatch");
        Assert.That(a.IsMinimized, Is.True);
        Assert.That(a.IsFocused, Is.False);
        Assert.That(b.IsFocused, Is.True);
        CollectionAssert.AreEqual(new[] { a, b }, manager.Windows);
    }

    [Test]
    public void MinimizeForegroundWindow_FocusesNextVisibleAndSkipsMinimizedWindows()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"), Entry("ticktock"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        var c = manager.Launch("ticktock");
        manager.MinimizeWindow("excess");
        manager.MinimizeWindow("ticktock");
        Assert.That(a.IsFocused, Is.True);
        Assert.That(b.IsFocused || c.IsFocused, Is.False);
        Assert.That(b.IsMinimized && c.IsMinimized, Is.True);
        CollectionAssert.AreEqual(new[] { b, c, a }, manager.Windows);
    }

    [Test]
    public void RepeatedMinimize_DoesNotStealFocusOrDuplicateMembership()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.MinimizeWindow("excess");
        manager.MinimizeWindow("excess");
        Assert.That(a.IsFocused, Is.True);
        Assert.That(b.IsMinimized && !b.IsFocused, Is.True);
        CollectionAssert.AreEqual(new[] { b, a }, manager.Windows);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LaunchMinimized_ReusesMovedResizedStateAndRetainsWindowMode(bool maximized)
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = PrepareWindow(manager, maximized);
        Rect normal = state.NormalRect;
        Rect current = state.CurrentRect;
        manager.MinimizeWindow("inwatch");
        var other = manager.Launch("excess");
        Assert.That(manager.Launch("inwatch"), Is.SameAs(state));
        AssertVisibleFocused(state, maximized);
        AssertBounds(state, normal, current);
        Assert.That(other.IsFocused, Is.False);
        CollectionAssert.AreEqual(new[] { other, state }, manager.Windows);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RestoreMinimized_PreservesNormalAndCurrentBoundsAndFocuses(bool maximized)
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = PrepareWindow(manager, maximized);
        Rect normal = state.NormalRect;
        Rect current = state.CurrentRect;
        manager.MinimizeWindow("inwatch");
        var other = manager.Launch("excess");
        manager.RestoreMinimizedWindow("inwatch");
        AssertVisibleFocused(state, maximized);
        AssertBounds(state, normal, current);
        Assert.That(other.IsFocused, Is.False);
        CollectionAssert.AreEqual(new[] { other, state }, manager.Windows);
    }

    [Test]
    public void RestoreAlreadyVisible_IsRepeatableAndOnlyChangesFocusOrder()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        Rect normal = a.NormalRect;
        manager.RestoreMinimizedWindow("inwatch");
        manager.RestoreMinimizedWindow("inwatch");
        AssertVisibleFocused(a, false);
        AssertBounds(a, normal, normal);
        CollectionAssert.AreEqual(new[] { b, a }, manager.Windows);
        Assert.That(b.IsFocused, Is.False);
    }

    [Test]
    public void AllWindowsMinimized_HaveNoFocusUntilOneIsRestored()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.MinimizeWindow("inwatch");
        manager.MinimizeWindow("excess");
        Assert.That(a.IsFocused || b.IsFocused, Is.False);
        Assert.That(manager.Windows.Count, Is.EqualTo(2));
        manager.RestoreMinimizedWindow("inwatch");
        AssertVisibleFocused(a, false);
        Assert.That(b.IsMinimized && !b.IsFocused, Is.True);
    }

    [Test]
    public void CloseOnlyWindow_RemovesOpenMembershipButRetainsClosedStateAndBounds()
    {
        var manager = Manager(Entry("inwatch"));
        var state = PrepareWindow(manager, false);
        Rect normal = state.NormalRect;
        var windows = manager.Windows;
        manager.CloseWindow("inwatch");
        Assert.That(state.IsOpen || state.IsMinimized || state.IsFocused, Is.False);
        Assert.That(manager.GetWindow("inwatch"), Is.SameAs(state));
        Assert.That(windows, Is.Empty);
        AssertBounds(state, normal, normal);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CloseMinimized_ClearsOpenAndMinimizedButRetainsGeometryAndMode(bool maximized)
    {
        var manager = Manager(Entry("inwatch"));
        var state = PrepareWindow(manager, maximized);
        Rect normal = state.NormalRect;
        Rect current = state.CurrentRect;
        manager.MinimizeWindow("inwatch");
        manager.CloseWindow("inwatch");
        Assert.That(state.IsOpen || state.IsMinimized || state.IsFocused, Is.False);
        Assert.That(state.IsMaximized, Is.EqualTo(maximized));
        Assert.That(manager.Windows, Is.Empty);
        AssertBounds(state, normal, current);
    }

    [Test]
    public void CloseForegroundWindow_FocusesNextVisibleAndSkipsMinimizedWindow()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"), Entry("ticktock"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        var c = manager.Launch("ticktock");
        manager.MinimizeWindow("excess");
        manager.CloseWindow("ticktock");
        Assert.That(a.IsFocused, Is.True);
        Assert.That(b.IsMinimized && !b.IsFocused, Is.True);
        Assert.That(c.IsOpen || c.IsFocused, Is.False);
        CollectionAssert.AreEqual(new[] { b, a }, manager.Windows);
    }

    [Test]
    public void CloseBackgroundWindow_PreservesFocusedWindowAndRelativeOrder()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"), Entry("ticktock"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        var c = manager.Launch("ticktock");
        manager.CloseWindow("excess");
        Assert.That(c.IsFocused, Is.True);
        Assert.That(a.IsFocused || b.IsFocused, Is.False);
        CollectionAssert.AreEqual(new[] { a, c }, manager.Windows);
    }

    [Test]
    public void RepeatedClose_IsIdempotentAndDoesNotDisturbOtherFocus()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.CloseWindow("excess");
        manager.CloseWindow("excess");
        Assert.That(a.IsFocused, Is.True);
        Assert.That(b.IsOpen || b.IsFocused, Is.False);
        CollectionAssert.AreEqual(new[] { a }, manager.Windows);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Reopen_ReusesRetainedStateEvenWhenAuthoredDefaultsChange(bool maximized)
    {
        var entry = Entry("inwatch");
        var manager = Manager(entry, Entry("excess"));
        var state = PrepareWindow(manager, maximized);
        Rect normal = state.NormalRect;
        Rect current = state.CurrentRect;
        manager.CloseWindow("inwatch");
        var other = manager.Launch("excess");
        SetField(entry.Definition, "defaultPosition", new Vector2(200, 220));
        SetField(entry.Definition, "defaultSize", new Vector2(800, 600));
        SetField(entry.Definition, "minimumSize", new Vector2(440, 300));
        Assert.That(manager.Launch("inwatch"), Is.SameAs(state));
        Assert.That(manager.Launch("inwatch"), Is.SameAs(state));
        AssertVisibleFocused(state, maximized);
        AssertBounds(state, normal, current);
        Assert.That(state.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
        Assert.That(other.IsFocused, Is.False);
        CollectionAssert.AreEqual(new[] { other, state }, manager.Windows);
    }

    [Test]
    public void ClosingForeground_CanFocusAnotherMaximizedWindowWithoutRestoringIt()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.MaximizeWindow("inwatch");
        manager.MaximizeWindow("excess");
        manager.CloseWindow("excess");
        AssertVisibleFocused(a, true);
        Assert.That(a.CurrentRect, Is.EqualTo(new Rect(0, 0, 1000, 800)));
        Assert.That(b.IsOpen, Is.False);
    }

    [TestCase("focus")]
    [TestCase("minimize")]
    [TestCase("restoreMinimized")]
    [TestCase("maximize")]
    [TestCase("restoreNormal")]
    [TestCase("drag")]
    [TestCase("resize")]
    public void ClosedWindow_RejectsVisibleOnlyOperationsWithoutChangingOtherFocus(string operation)
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        manager.CloseWindow("inwatch");
        var other = manager.Launch("excess");
        Assert.Throws<InvalidOperationException>(() => Invoke(manager, operation, "inwatch", normal));
        Assert.That(state.IsOpen || state.IsMinimized || state.IsFocused, Is.False);
        AssertBounds(state, normal, normal);
        Assert.That(other.IsFocused, Is.True);
        CollectionAssert.AreEqual(new[] { other }, manager.Windows);
    }

    [TestCase("focus")]
    [TestCase("maximize")]
    [TestCase("restoreNormal")]
    [TestCase("drag")]
    [TestCase("resize")]
    public void MinimizedWindow_RejectsVisibleOnlyOperationsWithoutChangingOtherFocus(string operation)
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        manager.MinimizeWindow("inwatch");
        var other = manager.Launch("excess");
        Assert.Throws<InvalidOperationException>(() => Invoke(manager, operation, "inwatch", normal));
        Assert.That(state.IsOpen && state.IsMinimized && !state.IsFocused, Is.True);
        AssertBounds(state, normal, normal);
        Assert.That(other.IsFocused, Is.True);
        CollectionAssert.AreEqual(new[] { state, other }, manager.Windows);
    }

    [TestCase("minimize")]
    [TestCase("restoreMinimized")]
    [TestCase("close")]
    public void UnlaunchedLifecycleCommand_DoesNotCreateStateOrChangeExistingFocus(string operation)
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = manager.Launch("inwatch");
        Assert.Throws<KeyNotFoundException>(() => Invoke(manager, operation, "excess", state.NormalRect));
        AssertVisibleFocused(state, false);
        CollectionAssert.AreEqual(new[] { state }, manager.Windows);
    }

    [TestCase("minimize")]
    [TestCase("restoreMinimized")]
    [TestCase("close")]
    public void BlankLifecycleId_IsRejectedWithoutCreatingWindows(string operation)
    {
        var manager = Manager(Entry("inwatch"));
        Assert.Throws<ArgumentException>(() => Invoke(manager, operation, " ", new Rect(0, 0, 480, 360)));
        Assert.That(manager.Windows, Is.Empty);
    }

    [Test]
    public void RepeatedLifecycleCycles_RetainOneStateAndExactNormalBounds()
    {
        var manager = Manager(Entry("inwatch"));
        var state = PrepareWindow(manager, false);
        Rect normal = state.NormalRect;
        for (int i = 0; i < 3; i++)
        {
            manager.MinimizeWindow("inwatch");
            Assert.That(manager.Launch("inwatch"), Is.SameAs(state));
            manager.MaximizeWindow("inwatch");
            manager.MinimizeWindow("inwatch");
            manager.RestoreMinimizedWindow("inwatch");
            AssertVisibleFocused(state, true);
            manager.RestoreNormalWindow("inwatch");
            manager.CloseWindow("inwatch");
            Assert.That(manager.Launch("inwatch"), Is.SameAs(state));
            AssertVisibleFocused(state, false);
            AssertBounds(state, normal, normal);
            CollectionAssert.AreEqual(new[] { state }, manager.Windows);
        }
    }

    private static WindowState PrepareWindow(WindowManager manager, bool maximized)
    {
        var state = manager.Launch("inwatch");
        manager.ResizeWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(120, 80),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom);
        manager.DragWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(80, 90));
        if (maximized) manager.MaximizeWindow("inwatch");
        return state;
    }

    private static void AssertVisibleFocused(WindowState state, bool maximized)
    {
        Assert.That(state.IsOpen && state.IsFocused, Is.True);
        Assert.That(state.IsMinimized, Is.False);
        Assert.That(state.IsMaximized, Is.EqualTo(maximized));
    }

    private static void AssertBounds(WindowState state, Rect normal, Rect current)
    {
        Assert.That(state.NormalRect, Is.EqualTo(normal));
        Assert.That(state.CurrentRect, Is.EqualTo(current));
    }

    private static void Invoke(WindowManager manager, string operation, string id, Rect start)
    {
        switch (operation)
        {
            case "focus": manager.Focus(id); break;
            case "minimize": manager.MinimizeWindow(id); break;
            case "restoreMinimized": manager.RestoreMinimizedWindow(id); break;
            case "close": manager.CloseWindow(id); break;
            case "maximize": manager.MaximizeWindow(id); break;
            case "restoreNormal": manager.RestoreNormalWindow(id); break;
            case "drag": manager.DragWindow(id, start, Vector2.zero, Vector2.one); break;
            case "resize": manager.ResizeWindow(id, start, Vector2.zero, Vector2.one, WindowResizeEdges.Right); break;
            default: throw new ArgumentException("Unknown test operation.", nameof(operation));
        }
    }
}
