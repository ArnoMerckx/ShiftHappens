using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WindowStateTests
{
    private AppDefinition definition;

    [SetUp]
    public void SetUp()
    {
        definition = ScriptableObject.CreateInstance<AppDefinition>();
        SetField("appId", "inwatch");
        SetField("displayName", "InWatch");
    }

    [TearDown]
    public void TearDown()
    {
        if (definition != null)
            UnityEngine.Object.DestroyImmediate(definition);
    }

    [Test]
    public void Constructor_RejectsMissingDefinition()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowState(null));
    }

    [Test]
    public void Constructor_RejectsInvalidIdentity()
    {
        SetField("appId", " ");

        Assert.Throws<InvalidOperationException>(() => new WindowState(definition));
    }

    [Test]
    public void Constructor_RejectsInvalidWindowConfiguration()
    {
        SetField("defaultSize", new Vector2(1, 1));

        Assert.Throws<InvalidOperationException>(() => new WindowState(definition));
    }

    [Test]
    public void NewState_IsClosedWithCopiedDefaultBounds()
    {
        var state = new WindowState(definition);

        Assert.That(state.AppId, Is.EqualTo("inwatch"));
        Assert.That(state.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
        Assert.That(state.NormalRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        Assert.That(state.CurrentRect, Is.EqualTo(state.NormalRect));
        AssertClosed(state);
    }

    [Test]
    public void Construction_DoesNotChangeTheAuthoredDefinition()
    {
        var state = new WindowState(definition);

        Assert.That(definition.AppId, Is.EqualTo("inwatch"));
        Assert.That(definition.DefaultPosition, Is.EqualTo(new Vector2(40, 40)));
        Assert.That(definition.DefaultSize, Is.EqualTo(new Vector2(480, 360)));
        Assert.That(definition.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
        AssertClosed(state);
    }

    [Test]
    public void DefinitionChanges_DoNotRewriteExistingRuntimeState()
    {
        var state = new WindowState(definition);

        SetField("appId", "excess");
        SetField("defaultPosition", new Vector2(100, 120));
        SetField("defaultSize", new Vector2(700, 500));
        SetField("minimumSize", new Vector2(440, 300));

        Assert.That(state.AppId, Is.EqualTo("inwatch"));
        Assert.That(state.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
        Assert.That(state.NormalRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        Assert.That(state.CurrentRect, Is.EqualTo(state.NormalRect));
        AssertClosed(state);
    }

    [Test]
    public void RuntimeSnapshot_SurvivesDestructionOfItsSourceDefinition()
    {
        var state = new WindowState(definition);
        UnityEngine.Object.DestroyImmediate(definition);
        definition = null;

        Assert.That(state.AppId, Is.EqualTo("inwatch"));
        Assert.That(state.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
        Assert.That(state.NormalRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        Assert.That(state.CurrentRect, Is.EqualTo(state.NormalRect));
        AssertClosed(state);
    }

    [Test]
    public void ReturnedValueCopies_CannotRewriteRuntimeBounds()
    {
        var state = new WindowState(definition);
        Rect normal = state.NormalRect;
        Rect current = state.CurrentRect;
        Vector2 minimum = state.MinimumSize;

        normal.x = 900;
        current.width = 1;
        minimum.y = 1;

        Assert.That(state.NormalRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        Assert.That(state.CurrentRect, Is.EqualTo(state.NormalRect));
        Assert.That(state.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
    }

    [Test]
    public void EachConstruction_TakesItsOwnSnapshotOfTheCurrentDefinition()
    {
        var first = new WindowState(definition);
        SetField("defaultPosition", new Vector2(80, 90));
        SetField("defaultSize", new Vector2(600, 400));
        var second = new WindowState(definition);

        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(first.NormalRect, Is.EqualTo(new Rect(40, 40, 480, 360)));
        Assert.That(second.NormalRect, Is.EqualTo(new Rect(80, 90, 600, 400)));
        Assert.That(first.CurrentRect, Is.EqualTo(first.NormalRect));
        Assert.That(second.CurrentRect, Is.EqualTo(second.NormalRect));
        AssertClosed(first);
        AssertClosed(second);
    }

    private void SetField(string name, object value)
    {
        FieldInfo field = typeof(AppDefinition).GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing serialized field: " + name);
        field.SetValue(definition, value);
    }

    private static void AssertClosed(WindowState state)
    {
        Assert.That(state.IsOpen, Is.False);
        Assert.That(state.IsMinimized, Is.False);
        Assert.That(state.IsMaximized, Is.False);
        Assert.That(state.IsFocused, Is.False);
    }
}
