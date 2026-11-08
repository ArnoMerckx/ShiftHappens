using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class AppDefinitionTests
{
    private AppDefinition definition;
    private Texture2D texture;
    private Sprite icon;

    [SetUp]
    public void SetUp()
    {
        definition = ScriptableObject.CreateInstance<AppDefinition>();
        SetField(definition, "appId", "inwatch");
        SetField(definition, "displayName", "InWatch");
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(definition);

        if (icon != null)
            UnityEngine.Object.DestroyImmediate(icon);

        if (texture != null)
            UnityEngine.Object.DestroyImmediate(texture);
    }

    [Test]
    public void ValidDefinition_PreservesIdentityDefaultsAndOptionalMissingIcon()
    {
        Assert.DoesNotThrow(() => definition.Validate());

        Assert.That(definition.AppId, Is.EqualTo("inwatch"));
        Assert.That(definition.DisplayName, Is.EqualTo("InWatch"));
        Assert.That(definition.Icon, Is.Null);
        Assert.That(definition.DefaultPosition, Is.EqualTo(new Vector2(40, 40)));
        Assert.That(definition.DefaultSize, Is.EqualTo(new Vector2(480, 360)));
        Assert.That(definition.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
    }

    [Test]
    public void OptionalIcon_RetainsTheAssignedReference()
    {
        texture = new Texture2D(2, 2);
        icon = Sprite.Create(
            texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        SetField(definition, "icon", icon);

        Assert.DoesNotThrow(() => definition.Validate());
        Assert.That(definition.Icon, Is.SameAs(icon));
    }

    [TestCase("appId", null)]
    [TestCase("appId", "")]
    [TestCase("appId", "   ")]
    [TestCase("displayName", null)]
    [TestCase("displayName", "")]
    [TestCase("displayName", "\t\n")]
    public void MissingIdentity_IsRejected(string field, string value)
    {
        SetField(definition, field, value);

        Assert.Throws<InvalidOperationException>(() => definition.Validate());
    }

    [TestCase("x", -1f)]
    [TestCase("x", float.NaN)]
    [TestCase("x", float.PositiveInfinity)]
    [TestCase("x", float.NegativeInfinity)]
    [TestCase("y", -1f)]
    [TestCase("y", float.NaN)]
    [TestCase("y", float.PositiveInfinity)]
    [TestCase("y", float.NegativeInfinity)]
    public void InvalidDefaultPosition_IsRejected(string axis, float value)
    {
        Vector2 position = definition.DefaultPosition;
        SetCoordinate(ref position, axis, value);
        SetField(definition, "defaultPosition", position);

        Assert.Throws<InvalidOperationException>(() => definition.Validate());
    }

    [TestCase("defaultSize", "x", 0f)]
    [TestCase("defaultSize", "x", -1f)]
    [TestCase("defaultSize", "x", float.NaN)]
    [TestCase("defaultSize", "x", float.PositiveInfinity)]
    [TestCase("defaultSize", "x", float.NegativeInfinity)]
    [TestCase("defaultSize", "y", 0f)]
    [TestCase("defaultSize", "y", -1f)]
    [TestCase("defaultSize", "y", float.NaN)]
    [TestCase("defaultSize", "y", float.PositiveInfinity)]
    [TestCase("defaultSize", "y", float.NegativeInfinity)]
    [TestCase("minimumSize", "x", 0f)]
    [TestCase("minimumSize", "x", -1f)]
    [TestCase("minimumSize", "x", float.NaN)]
    [TestCase("minimumSize", "x", float.PositiveInfinity)]
    [TestCase("minimumSize", "x", float.NegativeInfinity)]
    [TestCase("minimumSize", "y", 0f)]
    [TestCase("minimumSize", "y", -1f)]
    [TestCase("minimumSize", "y", float.NaN)]
    [TestCase("minimumSize", "y", float.PositiveInfinity)]
    [TestCase("minimumSize", "y", float.NegativeInfinity)]
    public void InvalidSizeCoordinate_IsRejected(
        string field, string axis, float value)
    {
        Vector2 size = field == "defaultSize"
            ? definition.DefaultSize : definition.MinimumSize;
        SetCoordinate(ref size, axis, value);
        SetField(definition, field, size);

        Assert.Throws<InvalidOperationException>(() => definition.Validate());
    }

    [TestCase(279f, 360f)]
    [TestCase(480f, 199f)]
    [TestCase(279f, 199f)]
    public void DefaultSizeBelowMinimum_IsRejected(float width, float height)
    {
        SetField(definition, "defaultSize", new Vector2(width, height));

        Assert.Throws<InvalidOperationException>(() => definition.Validate());
    }

    [TestCase(0f, 0f, 280f, 200f)]
    [TestCase(0.5f, 1.25f, 0.5f, 0.25f)]
    [TestCase(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue)]
    public void FiniteNonnegativePositionAndEqualMinimum_AreAccepted(
        float x, float y, float width, float height)
    {
        var size = new Vector2(width, height);
        SetField(definition, "defaultPosition", new Vector2(x, y));
        SetField(definition, "defaultSize", size);
        SetField(definition, "minimumSize", size);

        Assert.DoesNotThrow(() => definition.Validate());
        Assert.That(definition.DefaultPosition, Is.EqualTo(new Vector2(x, y)));
        Assert.That(definition.DefaultSize, Is.EqualTo(size));
        Assert.That(definition.MinimumSize, Is.EqualTo(size));
    }

    [Test]
    public void ReturnedVectors_CannotMutateAuthoredConfiguration()
    {
        Vector2 position = definition.DefaultPosition;
        Vector2 size = definition.DefaultSize;
        Vector2 minimum = definition.MinimumSize;

        position.x = 900;
        size.y = 1;
        minimum.x = 1;

        Assert.That(definition.DefaultPosition, Is.EqualTo(new Vector2(40, 40)));
        Assert.That(definition.DefaultSize, Is.EqualTo(new Vector2(480, 360)));
        Assert.That(definition.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
        Assert.DoesNotThrow(() => definition.Validate());
    }

    [Test]
    public void FailedValidation_DoesNotClampOrRewriteConfiguration()
    {
        SetField(definition, "defaultSize", new Vector2(1, 1));

        Assert.Throws<InvalidOperationException>(() => definition.Validate());
        Assert.That(definition.DefaultSize, Is.EqualTo(Vector2.one));
        Assert.That(definition.MinimumSize, Is.EqualTo(new Vector2(280, 200)));
        Assert.That(definition.AppId, Is.EqualTo("inwatch"));
    }

    [Test]
    public void Validation_DoesNotNormalizeNonblankIdentity()
    {
        SetField(definition, "appId", " InWatch ");
        SetField(definition, "displayName", " InWatch Mail ");

        Assert.DoesNotThrow(() => definition.Validate());
        Assert.That(definition.AppId, Is.EqualTo(" InWatch "));
        Assert.That(definition.DisplayName, Is.EqualTo(" InWatch Mail "));
    }

    [Test]
    public void SeparateDefinitions_DoNotShareConfiguration()
    {
        AppDefinition other = ScriptableObject.CreateInstance<AppDefinition>();

        try
        {
            SetField(other, "appId", "excess");
            SetField(other, "displayName", "Excess");
            SetField(definition, "defaultSize", new Vector2(700, 500));

            Assert.DoesNotThrow(() => definition.Validate());
            Assert.DoesNotThrow(() => other.Validate());
            Assert.That(other.AppId, Is.EqualTo("excess"));
            Assert.That(other.DefaultSize, Is.EqualTo(new Vector2(480, 360)));
            Assert.That(definition.DefaultSize, Is.EqualTo(new Vector2(700, 500)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(other);
        }
    }

    private static void SetCoordinate(ref Vector2 vector, string axis, float value)
    {
        if (axis == "x")
            vector.x = value;
        else
            vector.y = value;
    }

    private static void SetField(AppDefinition target, string name, object value)
    {
        FieldInfo field = typeof(AppDefinition).GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing serialized field: " + name);
        field.SetValue(target, value);
    }
}
