using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WindowMaximizeRestoreTests : WindowManagementTestBase
{
    private static readonly Vector2 Area = new Vector2(1000, 800);

    private WindowManager Manager(params AppRegistration[] entries)
    {
        return new WindowManager(new AppRegistry(new List<AppRegistration>(entries)), Area);
    }

    [Test]
    public void Maximize_FillsWorkAreaPreservesNormalAndFocusesOnlyRequestedWindow()
    {
        var entry = Entry("inwatch");
        var manager = Manager(entry, Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        Rect normal = a.NormalRect;
        manager.MaximizeWindow("inwatch");
        Assert.That(a.CurrentRect, Is.EqualTo(new Rect(0, 0, 1000, 800)));
        Assert.That(a.NormalRect, Is.EqualTo(normal));
        Assert.That(a.IsMaximized && a.IsOpen && a.IsFocused, Is.True);
        Assert.That(a.IsMinimized, Is.False);
        Assert.That(b.IsMaximized || b.IsFocused, Is.False);
        Assert.That(b.CurrentRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        CollectionAssert.AreEqual(new[] { b, a }, manager.Windows);
        Assert.That(entry.Definition.DefaultPosition, Is.EqualTo(new Vector2(40, 40)));
        Assert.That(entry.Definition.DefaultSize, Is.EqualTo(new Vector2(480, 360)));
    }

    [Test]
    public void Restore_ReturnsExactPreviouslyMovedAndResizedNormalBounds()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.ResizeWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(120, 80),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom);
        manager.DragWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(80, 90));
        Rect normal = new Rect(120, 130, 600, 440);
        Assert.That(state.NormalRect, Is.EqualTo(normal));
        manager.MaximizeWindow("inwatch");
        manager.RestoreNormalWindow("inwatch");
        AssertNormal(state, normal);
        Assert.That(manager.GetWindow("inwatch"), Is.SameAs(state));
    }

    [Test]
    public void RepeatedMaximizeAndRestore_PreserveOriginalNormalAcrossCycles()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        for (int i = 0; i < 3; i++)
        {
            manager.MaximizeWindow("inwatch");
            manager.MaximizeWindow("inwatch");
            Assert.That(state.NormalRect, Is.EqualTo(normal));
            Assert.That(state.CurrentRect, Is.EqualTo(new Rect(0, 0, 1000, 800)));
            manager.RestoreNormalWindow("inwatch");
            manager.RestoreNormalWindow("inwatch");
            AssertNormal(state, normal);
        }
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
    }

    [Test]
    public void RestoreAlreadyNormal_PreservesBoundsAndBringsWindowFront()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        Rect normal = a.NormalRect;
        manager.RestoreNormalWindow("inwatch");
        AssertNormal(a, normal);
        Assert.That(b.IsFocused, Is.False);
        CollectionAssert.AreEqual(new[] { b, a }, manager.Windows);
    }

    [Test]
    public void RelaunchMaximized_ReusesStateAndKeepsBothRectangles()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        manager.MaximizeWindow("inwatch");
        Assert.That(manager.Launch("inwatch"), Is.SameAs(state));
        Assert.That(state.IsMaximized, Is.True);
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(0, 0, 1000, 800)));
        Assert.That(state.NormalRect, Is.EqualTo(normal));
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
    }

    [Test]
    public void WorkspaceBelowMinimum_MaximizeFillsAreaAndRestoreKeepsRecoveryBounds()
    {
        var manager = new WindowManager(new AppRegistry(
            new List<AppRegistration> { Entry("inwatch") }), new Vector2(200, 100));
        var state = manager.Launch("inwatch");
        Rect normal = new Rect(-80, 0, 280, 200);
        Assert.That(state.NormalRect, Is.EqualTo(normal));
        manager.MaximizeWindow("inwatch");
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(0, 0, 200, 100)));
        Assert.That(state.NormalRect, Is.EqualTo(normal));
        manager.RestoreNormalWindow("inwatch");
        AssertNormal(state, normal);
    }

    [Test]
    public void NormalAlreadyFillsWorkspace_StillChangesMaximizedFlagCorrectly()
    {
        var manager = new WindowManager(new AppRegistry(
            new List<AppRegistration> { Entry("inwatch") }), new Vector2(480, 360));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        manager.MaximizeWindow("inwatch");
        Assert.That(state.IsMaximized, Is.True);
        Assert.That(state.CurrentRect, Is.EqualTo(normal));
        manager.RestoreNormalWindow("inwatch");
        AssertNormal(state, normal);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UnlaunchedCommand_DoesNotLaunchOrChangeExistingFocus(bool restore)
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        Assert.Throws<KeyNotFoundException>(() => Command(manager, restore, "excess"));
        AssertNormal(state, normal);
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BlankCommand_DoesNotChangeExistingState(bool restore)
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        Assert.Throws<ArgumentException>(() => Command(manager, restore, " "));
        AssertNormal(state, normal);
    }

    [Test]
    public void MaximizedWindow_RejectsDragWithoutChangingBoundsOrFlags()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        manager.MaximizeWindow("inwatch");
        Assert.Throws<InvalidOperationException>(() => manager.DragWindow(
            "inwatch", normal, Vector2.zero, new Vector2(100, 100)));
        AssertMaximized(state, normal);
    }

    [Test]
    public void MaximizedWindow_RejectsResizeWithoutChangingBoundsOrFlags()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        manager.MaximizeWindow("inwatch");
        Assert.Throws<InvalidOperationException>(() => manager.ResizeWindow(
            "inwatch", normal, Vector2.zero, new Vector2(100, 100), WindowResizeEdges.Right));
        AssertMaximized(state, normal);
    }

    [Test]
    public void RestoredWindow_AcceptsFreshDragAndResize()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.MaximizeWindow("inwatch");
        manager.RestoreNormalWindow("inwatch");
        manager.DragWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(20, 30));
        manager.ResizeWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(50, 60),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom);
        AssertNormal(state, new Rect(60, 70, 530, 420));
    }

    [Test]
    public void ChangedAuthoredDefaults_DoNotReplaceRetainedRuntimeNormalOrMinimum()
    {
        var entry = Entry("inwatch");
        var manager = Manager(entry);
        var state = manager.Launch("inwatch");
        Rect normal = state.NormalRect;
        manager.MaximizeWindow("inwatch");
        SetField(entry.Definition, "defaultPosition", new Vector2(100, 120));
        SetField(entry.Definition, "defaultSize", new Vector2(800, 700));
        SetField(entry.Definition, "minimumSize", new Vector2(440, 300));
        manager.RestoreNormalWindow("inwatch");
        AssertNormal(state, normal);
        Assert.That(state.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
    }

    [Test]
    public void RestoringOneApp_LeavesOtherAppMaximizedAndFocusesRestoredApp()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        Rect aNormal = a.NormalRect;
        Rect bNormal = b.NormalRect;
        manager.MaximizeWindow("inwatch");
        manager.MaximizeWindow("excess");
        manager.RestoreNormalWindow("inwatch");
        AssertNormal(a, aNormal);
        Assert.That(b.IsMaximized, Is.True);
        Assert.That(b.IsFocused, Is.False);
        Assert.That(b.CurrentRect, Is.EqualTo(new Rect(0, 0, 1000, 800)));
        Assert.That(b.NormalRect, Is.EqualTo(bNormal));
        CollectionAssert.AreEqual(new[] { b, a }, manager.Windows);
    }

    private static void Command(WindowManager manager, bool restore, string id)
    {
        if (restore) manager.RestoreNormalWindow(id);
        else manager.MaximizeWindow(id);
    }

    private static void AssertNormal(WindowState state, Rect normal)
    {
        Assert.That(state.CurrentRect, Is.EqualTo(normal));
        Assert.That(state.NormalRect, Is.EqualTo(normal));
        Assert.That(state.IsMaximized || state.IsMinimized, Is.False);
        Assert.That(state.IsOpen && state.IsFocused, Is.True);
    }

    private static void AssertMaximized(WindowState state, Rect normal)
    {
        Assert.That(state.IsMaximized && state.IsOpen && state.IsFocused, Is.True);
        Assert.That(state.IsMinimized, Is.False);
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(0, 0, 1000, 800)));
        Assert.That(state.NormalRect, Is.EqualTo(normal));
    }
}
