using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WindowManagerResizeTests : WindowManagementTestBase
{
    private WindowManager Manager(params AppRegistration[] entries)
    {
        return new WindowManager(new AppRegistry(new List<AppRegistration>(entries)),
            new Vector2(1000, 800));
    }

    [Test]
    public void Resize_UpdatesBothBoundsWithoutChangingOtherWindowFocusOrderOrDefinition()
    {
        var entry = Entry("inwatch");
        var manager = Manager(entry, Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.ResizeWindow("inwatch", a.NormalRect, Vector2.zero, new Vector2(120, 80),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom);
        Assert.That(a.CurrentRect, Is.EqualTo(new Rect(40, 40, 600, 440)));
        Assert.That(a.NormalRect, Is.EqualTo(a.CurrentRect));
        Assert.That(b.CurrentRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        Assert.That(a.IsFocused || a.IsMaximized || a.IsMinimized, Is.False);
        Assert.That(a.IsOpen && b.IsFocused, Is.True);
        CollectionAssert.AreEqual(new[] { a, b }, manager.Windows);
        Assert.That(entry.Definition.DefaultSize, Is.EqualTo(new Vector2(480, 360)));
        Assert.That(entry.Definition.DefaultPosition, Is.EqualTo(new Vector2(40, 40)));
    }

    [Test]
    public void Shrink_UsesRuntimeMinimumSnapshotInsteadOfChangedAuthoredMinimum()
    {
        var entry = Entry("inwatch");
        var manager = Manager(entry);
        var state = manager.Launch("inwatch");
        SetField(entry.Definition, "minimumSize", new Vector2(440, 300));
        manager.ResizeWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(-9000, -9000),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom);
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(40, 40, 280, 200)));
        Assert.That(state.NormalRect, Is.EqualTo(state.CurrentRect));
        Assert.That(entry.Definition.MinimumSize, Is.EqualTo(new Vector2(440, 300)));
    }

    [Test]
    public void Grow_UsesManagerWorkspaceBoundary()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.ResizeWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(9000, 9000),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom);
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(40, 40, 960, 760)));
        Assert.That(state.NormalRect, Is.EqualTo(state.CurrentRect));
    }

    [Test]
    public void WorkspaceBelowMinimum_KeepsRecoveryBoundsWithoutShrinkingBelowMinimum()
    {
        var entry = Entry("inwatch");
        SetField(entry.Definition, "minimumSize", new Vector2(600, 900));
        SetField(entry.Definition, "defaultSize", new Vector2(600, 900));
        var manager = new WindowManager(new AppRegistry(
            new List<AppRegistration> { entry }), new Vector2(500, 800));
        var state = manager.Launch("inwatch");
        manager.ResizeWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(9000, 9000),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom);
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(-100, 0, 600, 900)));
        Assert.That(state.NormalRect, Is.EqualTo(state.CurrentRect));
    }

    [Test]
    public void RepeatedSamples_KeepOriginalOppositeEdgesAndReturnToOriginalSize()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect start = state.NormalRect;
        var edges = WindowResizeEdges.Left | WindowResizeEdges.Top;
        manager.ResizeWindow("inwatch", start, Vector2.zero, new Vector2(9000, 9000), edges);
        manager.ResizeWindow("inwatch", start, Vector2.zero, new Vector2(70, 90), edges);
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(110, 130, 410, 270)));
        manager.ResizeWindow("inwatch", start, Vector2.zero, Vector2.zero, edges);
        Assert.That(state.CurrentRect, Is.EqualTo(start));
        Assert.That(state.NormalRect, Is.EqualTo(start));
    }

    [Test]
    public void Relaunch_UsesSameResizedStateWithoutResettingBounds()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.ResizeWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(100, 100),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom);
        Rect resized = state.CurrentRect;
        Assert.That(manager.Launch("inwatch"), Is.SameAs(state));
        Assert.That(state.CurrentRect, Is.EqualTo(resized));
        Assert.That(state.NormalRect, Is.EqualTo(resized));
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NonfinitePointer_IsRejectedBeforeMutatingState(bool starting)
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        Vector2 invalid = new Vector2(float.NaN, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.ResizeWindow(
            "inwatch", normal, starting ? invalid : Vector2.zero,
            starting ? Vector2.zero : invalid, WindowResizeEdges.Right));
        AssertUnchanged(state, normal);
    }

    [Test]
    public void InvalidEdges_AreRejectedBeforeMutatingState()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.ResizeWindow(
            "inwatch", normal, Vector2.zero, Vector2.one, WindowResizeEdges.None));
        AssertUnchanged(state, normal);
    }

    [Test]
    public void InvalidStartingRect_IsRejectedBeforeMutatingState()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        Rect invalid = normal;
        invalid.x = float.NaN;
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.ResizeWindow(
            "inwatch", invalid, Vector2.zero, Vector2.one, WindowResizeEdges.Right));
        AssertUnchanged(state, normal);
    }

    [Test]
    public void UnlaunchedApp_IsRejectedWithoutCreatingOrChangingWindows()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        Assert.Throws<KeyNotFoundException>(() => manager.ResizeWindow(
            "excess", normal, Vector2.zero, Vector2.one, WindowResizeEdges.Right));
        AssertUnchanged(state, normal);
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
    }

    [Test]
    public void BlankId_IsRejectedWithoutCreatingState()
    {
        var manager = Manager(Entry("inwatch"));
        Assert.Throws<ArgumentException>(() => manager.ResizeWindow(" ",
            new Rect(40, 40, 480, 360), Vector2.zero, Vector2.one, WindowResizeEdges.Right));
        Assert.That(manager.Windows, Is.Empty);
    }

    private static void AssertUnchanged(WindowState state, Rect normal)
    {
        Assert.That(state.CurrentRect, Is.EqualTo(normal));
        Assert.That(state.NormalRect, Is.EqualTo(normal));
        Assert.That(state.IsMaximized || state.IsMinimized, Is.False);
        Assert.That(state.IsOpen && state.IsFocused, Is.True);
    }
}
