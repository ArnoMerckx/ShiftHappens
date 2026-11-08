using System;
using NUnit.Framework;
using UnityEngine;

public class WindowBoundsTests
{
    private static readonly Vector2 Minimum = new Vector2(280, 200);
    private static readonly Vector2 Workspace = new Vector2(1000, 700);

    [Test]
    public void FittingWindow_KeepsItsPositionAndSize()
    {
        var requested = new Rect(20, 30, 480, 360);

        Rect result = WindowBounds.ConstrainNormalRect(
            requested, Minimum, Workspace);

        AssertRect(result, requested);
    }

    [TestCase(-50f, -40f, 0f, 0f)]
    [TestCase(900f, 650f, 520f, 340f)]
    [TestCase(-50f, 650f, 0f, 340f)]
    [TestCase(900f, -40f, 520f, 0f)]
    [TestCase(520f, 340f, 520f, 340f)]
    [TestCase(0f, 0f, 0f, 0f)]
    public void FittingSize_ClampsPositionToEachWorkspaceEdge(
        float x, float y, float expectedX, float expectedY)
    {
        Rect result = WindowBounds.ConstrainNormalRect(
            new Rect(x, y, 480, 360), Minimum, Workspace);

        AssertRect(result, new Rect(expectedX, expectedY, 480, 360));
    }

    [TestCase(100f, 100f, 280f, 200f)]
    [TestCase(100f, 360f, 280f, 360f)]
    [TestCase(480f, 100f, 480f, 200f)]
    public void UndersizedWindow_GrowsOnlyTheDimensionsBelowMinimum(
        float width, float height, float expectedWidth, float expectedHeight)
    {
        Rect result = WindowBounds.ConstrainNormalRect(
            new Rect(20, 20, width, height), Minimum, Workspace);

        AssertRect(result, new Rect(20, 20, expectedWidth, expectedHeight));
    }

    [TestCase(1200f, 900f, 0f, 0f, 1000f, 700f)]
    [TestCase(1200f, 360f, 0f, 340f, 1000f, 360f)]
    [TestCase(480f, 900f, 520f, 0f, 480f, 700f)]
    public void OversizedWindow_ShrinksToWorkspaceWhenMinimumAllows(
        float width, float height, float expectedX, float expectedY,
        float expectedWidth, float expectedHeight)
    {
        Rect result = WindowBounds.ConstrainNormalRect(
            new Rect(900, 650, width, height), Minimum, Workspace);

        AssertRect(result,
            new Rect(expectedX, expectedY, expectedWidth, expectedHeight));
    }

    [Test]
    public void WorkspaceEqualToMinimum_ProducesAnExactFit()
    {
        Rect result = WindowBounds.ConstrainNormalRect(
            new Rect(40, 40, 480, 360), Minimum, Minimum);

        AssertRect(result, new Rect(0, 0, 280, 200));
    }

    [TestCase(320f, 240f, 480f, 360f, -160f, 0f)]
    [TestCase(320f, 700f, 480f, 200f, -160f, 340f)]
    [TestCase(1000f, 240f, 280f, 360f, 520f, 0f)]
    public void NonfittingMinimum_AlignsOversizedAxesRightAndTop(
        float workspaceWidth, float workspaceHeight,
        float minimumWidth, float minimumHeight,
        float expectedX, float expectedY)
    {
        Rect result = WindowBounds.ConstrainNormalRect(
            new Rect(900, 650, 480, 360),
            new Vector2(minimumWidth, minimumHeight),
            new Vector2(workspaceWidth, workspaceHeight));

        AssertRect(result, new Rect(expectedX, expectedY, 480, 360));
    }

    [TestCase(-999f, -999f)]
    [TestCase(999f, 999f)]
    [TestCase(0f, 0f)]
    public void NonfittingWindow_RemainsRecoverableRegardlessOfRequestedPosition(
        float x, float y)
    {
        Rect result = WindowBounds.ConstrainNormalRect(
            new Rect(x, y, 480, 360),
            new Vector2(480, 360), new Vector2(320, 240));

        AssertRect(result, new Rect(-160, 0, 480, 360));
        Assert.That(result.xMax, Is.EqualTo(320f));
        Assert.That(result.y, Is.Zero);
    }

    [Test]
    public void FractionalCanvasUnits_AreNotRoundedToPixels()
    {
        var requested = new Rect(3.25f, 4.5f, 10.5f, 9.25f);

        Rect result = WindowBounds.ConstrainNormalRect(
            requested, new Vector2(1.25f, 2.5f), new Vector2(100.5f, 80.25f));

        AssertRect(result, requested);
    }

    [Test]
    public void UniformlyScaledInputs_ProduceUniformlyScaledBounds()
    {
        var requested = new Rect(900, 650, 480, 360);
        Rect original = WindowBounds.ConstrainNormalRect(
            requested, Minimum, Workspace);

        Rect scaled = WindowBounds.ConstrainNormalRect(
            new Rect(requested.position * 2, requested.size * 2),
            Minimum * 2, Workspace * 2);

        AssertRect(scaled, new Rect(original.position * 2, original.size * 2));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ConstrainingAnAlreadyConstrainedResult_IsIdempotent(int scenario)
    {
        Rect requested;
        Vector2 minimum = Minimum;
        Vector2 workspace = Workspace;

        switch (scenario)
        {
            case 0: requested = new Rect(20, 30, 480, 360); break;
            case 1: requested = new Rect(900, 650, 1200, 900); break;
            case 2:
                requested = new Rect(40, 40, 480, 360);
                minimum = new Vector2(480, 360);
                workspace = new Vector2(320, 240);
                break;
            default: requested = new Rect(900, 650, 100, 100); break;
        }

        Rect first = WindowBounds.ConstrainNormalRect(requested, minimum, workspace);
        Rect second = WindowBounds.ConstrainNormalRect(first, minimum, workspace);

        AssertRect(second, first);
    }

    [Test]
    public void Constraining_DoesNotChangeCallerValues()
    {
        var requested = new Rect(900, 650, 480, 360);
        Vector2 minimum = Minimum;
        Vector2 workspace = Workspace;

        WindowBounds.ConstrainNormalRect(requested, minimum, workspace);

        AssertRect(requested, new Rect(900, 650, 480, 360));
        Assert.That(minimum, Is.EqualTo(Minimum));
        Assert.That(workspace, Is.EqualTo(Workspace));
    }

    [TestCase("x", float.NaN)]
    [TestCase("x", float.PositiveInfinity)]
    [TestCase("x", float.NegativeInfinity)]
    [TestCase("y", float.NaN)]
    [TestCase("y", float.PositiveInfinity)]
    [TestCase("y", float.NegativeInfinity)]
    public void NonfiniteRequestedPosition_IsRejected(string axis, float value)
    {
        var requested = new Rect(20, 30, 480, 360);
        if (axis == "x")
            requested.x = value;
        else
            requested.y = value;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowBounds.ConstrainNormalRect(requested, Minimum, Workspace));
        Assert.That(error.ParamName, Is.EqualTo("requested"));
    }

    [TestCase("width", 0f)]
    [TestCase("width", -1f)]
    [TestCase("width", float.NaN)]
    [TestCase("width", float.PositiveInfinity)]
    [TestCase("width", float.NegativeInfinity)]
    [TestCase("height", 0f)]
    [TestCase("height", -1f)]
    [TestCase("height", float.NaN)]
    [TestCase("height", float.PositiveInfinity)]
    [TestCase("height", float.NegativeInfinity)]
    public void InvalidRequestedSize_IsRejected(string axis, float value)
    {
        var requested = new Rect(20, 30, 480, 360);
        if (axis == "width")
            requested.width = value;
        else
            requested.height = value;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowBounds.ConstrainNormalRect(requested, Minimum, Workspace));
        Assert.That(error.ParamName, Is.EqualTo("requested"));
    }

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
    [TestCase("workAreaSize", "x", 0f)]
    [TestCase("workAreaSize", "x", -1f)]
    [TestCase("workAreaSize", "x", float.NaN)]
    [TestCase("workAreaSize", "x", float.PositiveInfinity)]
    [TestCase("workAreaSize", "x", float.NegativeInfinity)]
    [TestCase("workAreaSize", "y", 0f)]
    [TestCase("workAreaSize", "y", -1f)]
    [TestCase("workAreaSize", "y", float.NaN)]
    [TestCase("workAreaSize", "y", float.PositiveInfinity)]
    [TestCase("workAreaSize", "y", float.NegativeInfinity)]
    public void InvalidMinimumOrWorkspace_IsRejected(
        string parameter, string axis, float value)
    {
        Vector2 minimum = Minimum;
        Vector2 workspace = Workspace;
        Vector2 changed = parameter == "minimumSize" ? minimum : workspace;

        if (axis == "x")
            changed.x = value;
        else
            changed.y = value;

        if (parameter == "minimumSize")
            minimum = changed;
        else
            workspace = changed;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowBounds.ConstrainNormalRect(
                new Rect(20, 30, 480, 360), minimum, workspace));
        Assert.That(error.ParamName, Is.EqualTo(parameter));
    }

    private static void AssertRect(Rect actual, Rect expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.That(actual.width, Is.EqualTo(expected.width).Within(0.0001f));
        Assert.That(actual.height, Is.EqualTo(expected.height).Within(0.0001f));
    }
}
