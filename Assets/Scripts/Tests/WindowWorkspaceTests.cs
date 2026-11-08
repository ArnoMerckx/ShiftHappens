using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WindowWorkspaceTests : WindowManagementTestBase
{
    private static readonly Vector2 OriginalArea = new Vector2(1000, 800);
    private static readonly Rect LargeNormal = new Rect(400, 300, 600, 450);

    private WindowManager Manager(params AppRegistration[] entries)
    {
        return new WindowManager(new AppRegistry(new List<AppRegistration>(entries)), OriginalArea);
    }

    private AppRegistration LargeEntry(string id)
    {
        var entry = Entry(id);
        SetField(entry.Definition, "defaultPosition", LargeNormal.position);
        SetField(entry.Definition, "defaultSize", LargeNormal.size);
        return entry;
    }

    [Test]
    public void EmptyManager_UpdatesAreaWithoutLaunchingRegisteredApps()
    {
        var manager = Manager(Entry("inwatch"));
        Assert.That(manager.SetWorkAreaSize(new Vector2(800, 600)), Is.True);
        Assert.That(manager.WorkAreaSize, Is.EqualTo(new Vector2(800, 600)));
        Assert.That(manager.Windows, Is.Empty);
        Assert.Throws<KeyNotFoundException>(() => manager.GetWindow("inwatch"));
    }

    [Test]
    public void EqualArea_ReturnsFalseAndPreservesEveryState()
    {
        var manager = Manager(LargeEntry("inwatch"), LargeEntry("excess"));
        var a = manager.Launch("inwatch");
        manager.MaximizeWindow("inwatch");
        var b = manager.Launch("excess");
        manager.CloseWindow("excess");
        var aBefore = new StateSnapshot(a);
        var bBefore = new StateSnapshot(b);
        var order = new List<WindowState>(manager.Windows);
        Assert.That(manager.SetWorkAreaSize(OriginalArea), Is.False);
        aBefore.AssertUnchanged(manager);
        bBefore.AssertUnchanged(manager);
        CollectionAssert.AreEqual(order, manager.Windows);
    }

    [Test]
    public void ReturnedArea_IsAValueCopy()
    {
        var manager = Manager();
        Vector2 copy = manager.WorkAreaSize;
        copy.x = 1;
        Assert.That(manager.WorkAreaSize, Is.EqualTo(OriginalArea));
    }

    [Test]
    public void FirstLaunchAfterAreaChange_UsesUpdatedWorkspace()
    {
        var entry = Entry("inwatch");
        SetField(entry.Definition, "defaultPosition", new Vector2(900, 700));
        var manager = Manager(entry);
        manager.SetWorkAreaSize(new Vector2(600, 400));
        var state = manager.Launch("inwatch");
        AssertBounds(state, new Rect(120, 40, 480, 360), new Rect(120, 40, 480, 360));
    }

    [Test]
    public void FittingNormalWindow_KeepsExactBoundsAndFocus()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        var before = new StateSnapshot(state);
        Assert.That(manager.SetWorkAreaSize(new Vector2(800, 600)), Is.True);
        before.AssertUnchanged(manager);
    }

    [Test]
    public void NormalWindow_IsClampedInBothRectsWithoutChangingIdentity()
    {
        var manager = Manager(LargeEntry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.SetWorkAreaSize(new Vector2(500, 350));
        AssertBounds(state, new Rect(0, 0, 500, 350), new Rect(0, 0, 500, 350));
        Assert.That(manager.GetWindow("inwatch"), Is.SameAs(state));
        AssertFlags(state, true, false, false, true);
    }

    [TestCase(200f, 100f, -80f, 0f, 280f, 200f)]
    [TestCase(200f, 500f, -80f, 40f, 280f, 360f)]
    [TestCase(800f, 100f, 40f, 0f, 480f, 200f)]
    public void BelowMinimumArea_KeepsRightTopRecoveryWithoutViolatingMinimum(
        float areaWidth, float areaHeight, float x, float y, float width, float height)
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.SetWorkAreaSize(new Vector2(areaWidth, areaHeight));
        Rect expected = new Rect(x, y, width, height);
        AssertBounds(state, expected, expected);
        AssertFlags(state, true, false, false, true);
    }

    [Test]
    public void NormalWindow_RegrowKeepsItsAlreadyClampedArrangement()
    {
        var manager = Manager(LargeEntry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.SetWorkAreaSize(new Vector2(600, 400));
        manager.SetWorkAreaSize(OriginalArea);
        AssertBounds(state, new Rect(0, 0, 600, 400), new Rect(0, 0, 600, 400));
    }

    [Test]
    public void MaximizedWindow_FillsNewAreaWhileKeepingOriginalNormalBounds()
    {
        var manager = Manager(LargeEntry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.MaximizeWindow("inwatch");
        manager.SetWorkAreaSize(new Vector2(600, 400));
        AssertBounds(state, LargeNormal, new Rect(0, 0, 600, 400));
        AssertFlags(state, true, false, true, true);
    }

    [Test]
    public void MaximizedShrinkThenRegrow_RestoresExactOriginalNormalInSameSession()
    {
        var manager = Manager(LargeEntry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.MaximizeWindow("inwatch");
        manager.SetWorkAreaSize(new Vector2(500, 350));
        manager.SetWorkAreaSize(OriginalArea);
        manager.RestoreNormalWindow("inwatch");
        AssertBounds(state, LargeNormal, LargeNormal);
        AssertFlags(state, true, false, false, true);
    }

    [TestCase(700f, 500f, 100f, 50f, 600f, 450f)]
    [TestCase(500f, 350f, 0f, 0f, 500f, 350f)]
    public void RestoreAfterWorkspaceChange_ClampsSavedNormalToFinalArea(
        float areaWidth, float areaHeight, float x, float y, float width, float height)
    {
        var manager = Manager(LargeEntry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.MaximizeWindow("inwatch");
        manager.SetWorkAreaSize(new Vector2(areaWidth, areaHeight));
        Assert.That(state.NormalRect, Is.EqualTo(LargeNormal));
        manager.RestoreNormalWindow("inwatch");
        Rect expected = new Rect(x, y, width, height);
        AssertBounds(state, expected, expected);
        AssertFlags(state, true, false, false, true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MinimizedWindow_IsReconciledWithoutShowingOrFocusingIt(bool maximized)
    {
        var manager = Manager(LargeEntry("inwatch"));
        var state = manager.Launch("inwatch");
        if (maximized) manager.MaximizeWindow("inwatch");
        manager.MinimizeWindow("inwatch");
        manager.SetWorkAreaSize(new Vector2(600, 400));
        Rect normal = maximized ? LargeNormal : new Rect(0, 0, 600, 400);
        Rect current = new Rect(0, 0, 600, 400);
        AssertBounds(state, normal, current);
        AssertFlags(state, true, true, maximized, false);
        CollectionAssert.AreEqual(new[] { state }, manager.Windows);
        manager.RestoreMinimizedWindow("inwatch");
        AssertFlags(state, true, false, maximized, true);
        AssertBounds(state, normal, current);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClosedRetainedWindow_IsReconciledWithoutReopeningAndLaunchReusesState(bool maximized)
    {
        var manager = Manager(LargeEntry("inwatch"));
        var state = manager.Launch("inwatch");
        if (maximized) manager.MaximizeWindow("inwatch");
        manager.CloseWindow("inwatch");
        manager.SetWorkAreaSize(new Vector2(600, 400));
        Rect normal = maximized ? LargeNormal : new Rect(0, 0, 600, 400);
        Rect current = new Rect(0, 0, 600, 400);
        AssertBounds(state, normal, current);
        AssertFlags(state, false, false, maximized, false);
        Assert.That(manager.Windows, Is.Empty);
        Assert.That(manager.Launch("inwatch"), Is.SameAs(state));
        AssertFlags(state, true, false, maximized, true);
        AssertBounds(state, normal, current);
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
    }

    [Test]
    public void MinimizedMaximizedShrinkRegrow_KeepsExactNormalUntilFinalRestore()
    {
        var manager = Manager(LargeEntry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.MaximizeWindow("inwatch");
        manager.MinimizeWindow("inwatch");
        manager.SetWorkAreaSize(new Vector2(500, 350));
        manager.SetWorkAreaSize(OriginalArea);
        manager.RestoreMinimizedWindow("inwatch");
        manager.RestoreNormalWindow("inwatch");
        AssertBounds(state, LargeNormal, LargeNormal);
    }

    [Test]
    public void MixedStates_KeepFlagsFocusOrderAndMembershipWhileAllBoundsReconcile()
    {
        var manager = Manager(LargeEntry("inwatch"), LargeEntry("excess"),
            LargeEntry("ticktock"), LargeEntry("blamepoint"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.MaximizeWindow("excess");
        var c = manager.Launch("ticktock");
        manager.MinimizeWindow("ticktock");
        var d = manager.Launch("blamepoint");
        manager.CloseWindow("blamepoint");
        var order = new List<WindowState>(manager.Windows);
        manager.SetWorkAreaSize(new Vector2(600, 400));
        CollectionAssert.AreEqual(order, manager.Windows);
        AssertFlags(a, true, false, false, false);
        AssertFlags(b, true, false, true, true);
        AssertFlags(c, true, true, false, false);
        AssertFlags(d, false, false, false, false);
        Rect fit = new Rect(0, 0, 600, 400);
        AssertBounds(a, fit, fit);
        AssertBounds(b, LargeNormal, fit);
        AssertBounds(c, fit, fit);
        AssertBounds(d, fit, fit);
        Assert.That(manager.GetWindow("blamepoint"), Is.SameAs(d));
    }

    [TestCase(true, 0f)]
    [TestCase(true, -1f)]
    [TestCase(true, float.NaN)]
    [TestCase(true, float.PositiveInfinity)]
    [TestCase(true, float.NegativeInfinity)]
    [TestCase(false, 0f)]
    [TestCase(false, -1f)]
    [TestCase(false, float.NaN)]
    [TestCase(false, float.PositiveInfinity)]
    [TestCase(false, float.NegativeInfinity)]
    public void InvalidArea_IsRejectedBeforeAnyGeometryFlagsFocusOrMembershipMutation(bool xAxis, float value)
    {
        var manager = Manager(LargeEntry("inwatch"), LargeEntry("excess"), LargeEntry("ticktock"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.MaximizeWindow("excess");
        manager.MinimizeWindow("excess");
        var c = manager.Launch("ticktock");
        manager.MaximizeWindow("ticktock");
        manager.CloseWindow("ticktock");
        var snapshots = new[] { new StateSnapshot(a), new StateSnapshot(b), new StateSnapshot(c) };
        var order = new List<WindowState>(manager.Windows);
        Vector2 invalid = new Vector2(600, 400);
        if (xAxis) invalid.x = value;
        else invalid.y = value;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => manager.SetWorkAreaSize(invalid));
        Assert.That(error.ParamName, Is.EqualTo("newSize"));
        Assert.That(manager.WorkAreaSize, Is.EqualTo(OriginalArea));
        CollectionAssert.AreEqual(order, manager.Windows);
        foreach (var snapshot in snapshots) snapshot.AssertUnchanged(manager);
    }

    [Test]
    public void SmallButRealAreaChange_IsNotDiscardedAsApproximateEquality()
    {
        var manager = new WindowManager(new AppRegistry(new List<AppRegistration>()), Vector2.one);
        var changed = new Vector2(1.000001f, 1f);
        Assert.That(manager.SetWorkAreaSize(changed), Is.True);
        Assert.That(manager.WorkAreaSize.Equals(changed), Is.True);
    }

    [Test]
    public void RepeatedAreaNotifications_ReturnFalseOnlyWhenSizeIsUnchanged()
    {
        var manager = Manager();
        var smaller = new Vector2(800, 600);
        Assert.That(manager.SetWorkAreaSize(smaller), Is.True);
        Assert.That(manager.SetWorkAreaSize(smaller), Is.False);
        Assert.That(manager.SetWorkAreaSize(OriginalArea), Is.True);
        Assert.That(manager.SetWorkAreaSize(OriginalArea), Is.False);
    }

    [Test]
    public void SubsequentDrag_UsesUpdatedWorkspaceBounds()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.SetWorkAreaSize(new Vector2(600, 400));
        manager.DragWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(9000, 9000));
        AssertBounds(state, new Rect(120, 40, 480, 360), new Rect(120, 40, 480, 360));
    }

    [Test]
    public void SubsequentResize_UsesUpdatedWorkspaceBounds()
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        manager.SetWorkAreaSize(new Vector2(600, 400));
        manager.ResizeWindow("inwatch", state.NormalRect, Vector2.zero, new Vector2(9000, 9000),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom);
        AssertBounds(state, new Rect(40, 40, 560, 360), new Rect(40, 40, 560, 360));
    }

    [Test]
    public void Reconciliation_DoesNotRewriteAuthoredDefaultsOrRuntimeMinimum()
    {
        var entry = LargeEntry("inwatch");
        var manager = Manager(entry);
        var state = manager.Launch("inwatch");
        manager.SetWorkAreaSize(new Vector2(200, 100));
        Assert.That(entry.Definition.DefaultPosition, Is.EqualTo(LargeNormal.position));
        Assert.That(entry.Definition.DefaultSize, Is.EqualTo(LargeNormal.size));
        Assert.That(entry.Definition.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
        Assert.That(state.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
    }

    private static void AssertBounds(WindowState state, Rect normal, Rect current)
    {
        Assert.That(state.NormalRect, Is.EqualTo(normal));
        Assert.That(state.CurrentRect, Is.EqualTo(current));
    }

    private static void AssertFlags(WindowState state, bool open, bool minimized, bool maximized, bool focused)
    {
        Assert.That(state.IsOpen, Is.EqualTo(open));
        Assert.That(state.IsMinimized, Is.EqualTo(minimized));
        Assert.That(state.IsMaximized, Is.EqualTo(maximized));
        Assert.That(state.IsFocused, Is.EqualTo(focused));
    }

    private sealed class StateSnapshot
    {
        private readonly WindowState state;
        private readonly Rect normal;
        private readonly Rect current;
        private readonly bool open;
        private readonly bool minimized;
        private readonly bool maximized;
        private readonly bool focused;

        public StateSnapshot(WindowState state)
        {
            this.state = state;
            normal = state.NormalRect;
            current = state.CurrentRect;
            open = state.IsOpen;
            minimized = state.IsMinimized;
            maximized = state.IsMaximized;
            focused = state.IsFocused;
        }

        public void AssertUnchanged(WindowManager manager)
        {
            Assert.That(manager.GetWindow(state.AppId), Is.SameAs(state));
            AssertBounds(state, normal, current);
            AssertFlags(state, open, minimized, maximized, focused);
        }
    }
}
