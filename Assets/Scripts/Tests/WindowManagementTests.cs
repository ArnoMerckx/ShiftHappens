using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public abstract class WindowManagementTestBase
{
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    protected BaseWindow prefab;

    [SetUp]
    public void SetUpWindowData()
    {
        var root = new GameObject("Temporary window", typeof(RectTransform));
        owned.Add(root);
        root.SetActive(false);
        prefab = root.AddComponent<BaseWindow>();
    }

    [TearDown]
    public void DestroyWindowData()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null)
                UnityEngine.Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    protected AppRegistration Entry(string id)
    {
        var definition = ScriptableObject.CreateInstance<AppDefinition>();
        owned.Add(definition);
        SetField(definition, "appId", id);
        SetField(definition, "displayName", id);
        var entry = new AppRegistration();
        SetField(entry, "definition", definition);
        SetField(entry, "windowPrefab", prefab);
        return entry;
    }

    protected static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing serialized field: " + name);
        field.SetValue(target, value);
    }
}

public class AppRegistryTests : WindowManagementTestBase
{
    [Test]
    public void NullList_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new AppRegistry(null));
    }

    [Test]
    public void EmptyRegistry_IsValid()
    {
        var registry = new AppRegistry(new List<AppRegistration>());
        Assert.That(registry.Count, Is.Zero);
        Assert.Throws<KeyNotFoundException>(() => registry.Get("inwatch"));
    }

    [Test]
    public void NullEntry_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new AppRegistry(new List<AppRegistration> { null }));
    }

    [TestCase("definition")]
    [TestCase("windowPrefab")]
    public void MissingReference_IsRejected(string field)
    {
        var entry = Entry("inwatch");
        SetField(entry, field, null);
        Assert.Throws<ArgumentException>(() =>
            new AppRegistry(new List<AppRegistration> { entry }));
    }

    [TestCase("appId")]
    [TestCase("displayName")]
    public void InvalidMetadata_IsRejected(string field)
    {
        var entry = Entry("inwatch");
        SetField(entry.Definition, field, " ");
        Assert.Throws<InvalidOperationException>(() =>
            new AppRegistry(new List<AppRegistration> { entry }));
    }

    [Test]
    public void InvalidGeometry_IsRejected()
    {
        var entry = Entry("inwatch");
        SetField(entry.Definition, "defaultSize", Vector2.one);
        Assert.Throws<InvalidOperationException>(() =>
            new AppRegistry(new List<AppRegistration> { entry }));
    }

    [Test]
    public void DuplicateId_IsRejectedEvenForDifferentDefinitions()
    {
        Assert.Throws<ArgumentException>(() => new AppRegistry(
            new List<AppRegistration> { Entry("inwatch"), Entry("inwatch") }));
    }

    [Test]
    public void Lookup_PreservesRegistrationDefinitionAndSharedPrefabReferences()
    {
        var first = Entry("inwatch");
        var second = Entry("excess");
        var registry = new AppRegistry(new List<AppRegistration> { first, second });
        Assert.That(registry.Count, Is.EqualTo(2));
        Assert.That(registry.Get("inwatch"), Is.SameAs(first));
        Assert.That(registry.Get("excess"), Is.SameAs(second));
        Assert.That(registry.Get("inwatch").Definition, Is.SameAs(first.Definition));
        Assert.That(first.WindowPrefab, Is.SameAs(second.WindowPrefab));
    }

    [Test]
    public void IdComparison_IsOrdinalAndCaseSensitive()
    {
        var lower = Entry("inwatch");
        var upper = Entry("InWatch");
        var registry = new AppRegistry(new List<AppRegistration> { lower, upper });
        Assert.That(registry.Count, Is.EqualTo(2));
        Assert.That(registry.Get("inwatch"), Is.SameAs(lower));
        Assert.That(registry.Get("InWatch"), Is.SameAs(upper));
        Assert.Throws<KeyNotFoundException>(() => registry.Get("INWATCH"));
    }

    [Test]
    public void ChangingInputList_DoesNotChangeRegistryMembership()
    {
        var first = Entry("inwatch");
        var entries = new List<AppRegistration> { first };
        var registry = new AppRegistry(entries);
        entries.Clear();
        entries.Add(Entry("excess"));
        Assert.That(registry.Count, Is.EqualTo(1));
        Assert.That(registry.Get("inwatch"), Is.SameAs(first));
        Assert.Throws<KeyNotFoundException>(() => registry.Get("excess"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t")]
    public void BlankLookup_IsRejected(string id)
    {
        var registry = new AppRegistry(new List<AppRegistration> { Entry("inwatch") });
        Assert.Throws<ArgumentException>(() => registry.Get(id));
    }

    [Test]
    public void UnknownLookup_DoesNotChangeMembership()
    {
        var registry = new AppRegistry(new List<AppRegistration> { Entry("inwatch") });
        Assert.Throws<KeyNotFoundException>(() => registry.Get("missing"));
        Assert.That(registry.Count, Is.EqualTo(1));
    }
}

public class WindowManagerTests : WindowManagementTestBase
{
    private WindowManager Manager(params AppRegistration[] entries)
    {
        return new WindowManager(new AppRegistry(new List<AppRegistration>(entries)),
            new Vector2(1000, 800));
    }

    [Test]
    public void MissingRegistry_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WindowManager(null, new Vector2(1000, 800)));
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
    public void InvalidWorkArea_IsRejected(bool xAxis, float value)
    {
        var size = new Vector2(1000, 800);
        if (xAxis) size.x = value;
        else size.y = value;
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowManager(
            new AppRegistry(new List<AppRegistration>()), size));
    }

    [Test]
    public void NewManager_HasNoWindowsAndDoesNotLaunchRegisteredApps()
    {
        var manager = Manager(Entry("inwatch"));
        Assert.That(manager.Windows, Is.Empty);
    }

    [Test]
    public void FirstLaunch_OpensNormalFocusedWindowWithoutChangingDefinition()
    {
        var entry = Entry("inwatch");
        var manager = Manager(entry);
        var state = manager.Launch("inwatch");
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
        Assert.That(manager.Windows[0], Is.SameAs(state));
        Assert.That(state.IsOpen, Is.True);
        Assert.That(state.IsFocused, Is.True);
        Assert.That(state.IsMinimized, Is.False);
        Assert.That(state.IsMaximized, Is.False);
        Assert.That(state.NormalRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        Assert.That(state.CurrentRect, Is.EqualTo(state.NormalRect));
        Assert.That(entry.Definition.DefaultPosition, Is.EqualTo(new Vector2(40, 40)));
        Assert.That(entry.Definition.DefaultSize, Is.EqualTo(new Vector2(480, 360)));
    }

    [Test]
    public void FirstLaunch_ConstrainsBoundsToWorkArea()
    {
        var entry = Entry("inwatch");
        SetField(entry.Definition, "defaultPosition", new Vector2(900, 700));
        SetField(entry.Definition, "defaultSize", new Vector2(1200, 900));
        var state = Manager(entry).Launch("inwatch");
        Assert.That(state.NormalRect, Is.EqualTo(new Rect(0, 0, 1000, 800)));
        Assert.That(state.CurrentRect, Is.EqualTo(state.NormalRect));
        Assert.That(entry.Definition.DefaultSize, Is.EqualTo(new Vector2(1200, 900)));
    }

    [Test]
    public void WorkAreaSmallerThanMinimum_KeepsRecoverableTopRightBounds()
    {
        var manager = new WindowManager(new AppRegistry(
            new List<AppRegistration> { Entry("inwatch") }), new Vector2(200, 100));
        var state = manager.Launch("inwatch");
        Assert.That(state.NormalRect, Is.EqualTo(new Rect(-80, 0, 280, 200)));
    }

    [Test]
    public void DifferentApps_HaveIndependentStatesAndLastLaunchHasFocus()
    {
        var first = Entry("inwatch");
        var second = Entry("excess");
        SetField(second.Definition, "defaultPosition", new Vector2(100, 120));
        var manager = Manager(first, second);
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        Assert.That(b, Is.Not.SameAs(a));
        Assert.That(a.CurrentRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        Assert.That(b.CurrentRect, Is.EqualTo(new Rect(100, 120, 480, 360)));
        Assert.That(a.IsOpen && b.IsOpen, Is.True);
        Assert.That(a.IsFocused, Is.False);
        Assert.That(b.IsFocused, Is.True);
        CollectionAssert.AreEqual(new[] { a, b }, manager.Windows);
    }

    [Test]
    public void RepeatedLaunch_ReusesStateAndBoundsEvenIfAuthoredDefaultsChange()
    {
        var entry = Entry("inwatch");
        var manager = Manager(entry, Entry("excess"));
        var first = manager.Launch("inwatch");
        var other = manager.Launch("excess");
        SetField(entry.Definition, "defaultPosition", new Vector2(150, 170));
        SetField(entry.Definition, "defaultSize", new Vector2(600, 400));
        Assert.That(manager.Launch("inwatch"), Is.SameAs(first));
        Assert.That(first.CurrentRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        Assert.That(first.NormalRect, Is.EqualTo(first.CurrentRect));
        Assert.That(first.IsFocused, Is.True);
        Assert.That(other.IsFocused, Is.False);
        CollectionAssert.AreEqual(new[] { other, first }, manager.Windows);
    }

    [Test]
    public void Focus_BringsWindowFrontAndPreservesOtherWindowsRelativeOrder()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"), Entry("ticktock"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        var c = manager.Launch("ticktock");
        manager.Focus("inwatch");
        CollectionAssert.AreEqual(new[] { b, c, a }, manager.Windows);
        Assert.That(a.IsFocused, Is.True);
        Assert.That(b.IsFocused || c.IsFocused, Is.False);
        Assert.That(a.CurrentRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
    }

    [Test]
    public void RepeatedFocus_DoesNotDuplicateOrReorderOtherWindows()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var a = manager.Launch("inwatch");
        var b = manager.Launch("excess");
        manager.Focus("excess");
        manager.Focus("excess");
        CollectionAssert.AreEqual(new[] { a, b }, manager.Windows);
        Assert.That(b.IsFocused, Is.True);
        Assert.That(a.IsFocused, Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t")]
    public void BlankLaunch_PreservesExistingWindow(string id)
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Assert.Throws<ArgumentException>(() => manager.Launch(id));
        AssertUnchanged(manager, state);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t")]
    public void BlankFocus_PreservesExistingWindow(string id)
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Assert.Throws<ArgumentException>(() => manager.Focus(id));
        AssertUnchanged(manager, state);
    }

    [TestCase("missing")]
    [TestCase("InWatch")]
    public void UnknownLaunch_PreservesExistingWindow(string id)
    {
        var manager = Manager(Entry("inwatch"));
        var state = manager.Launch("inwatch");
        Assert.Throws<KeyNotFoundException>(() => manager.Launch(id));
        AssertUnchanged(manager, state);
    }

    [Test]
    public void FocusRegisteredButUnlaunchedApp_DoesNotLaunchItOrClearFocus()
    {
        var manager = Manager(Entry("inwatch"), Entry("excess"));
        var state = manager.Launch("inwatch");
        Assert.Throws<KeyNotFoundException>(() => manager.Focus("excess"));
        AssertUnchanged(manager, state);
    }

    [Test]
    public void WindowsCollection_IsReadOnlyAndReflectsLaterLaunches()
    {
        var manager = Manager(Entry("inwatch"));
        var view = manager.Windows;
        var state = manager.Launch("inwatch");
        Assert.That(view.Count, Is.EqualTo(1));
        var list = view as IList<WindowState>;
        Assert.That(list, Is.Not.Null);
        Assert.That(list.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => list.Clear());
        AssertUnchanged(manager, state);
    }

    private static void AssertUnchanged(WindowManager manager, WindowState state)
    {
        Assert.That(manager.Windows.Count, Is.EqualTo(1));
        Assert.That(manager.Windows[0], Is.SameAs(state));
        Assert.That(state.IsOpen && state.IsFocused, Is.True);
        Assert.That(state.CurrentRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
    }
}
