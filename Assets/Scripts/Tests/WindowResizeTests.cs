using System;
using NUnit.Framework;
using UnityEngine;

public class WindowResizeTests
{
    private static readonly Rect Start = new Rect(100, 100, 400, 300);
    private static readonly Vector2 Pointer = new Vector2(50, 60);
    private static readonly Vector2 Minimum = new Vector2(200, 150);
    private static readonly Vector2 Area = new Vector2(1000, 800);

    [TestCase(WindowResizeEdges.Left, 130f, 100f, 370f, 300f)]
    [TestCase(WindowResizeEdges.Right, 100f, 100f, 430f, 300f)]
    [TestCase(WindowResizeEdges.Top, 100f, 140f, 400f, 260f)]
    [TestCase(WindowResizeEdges.Bottom, 100f, 100f, 400f, 340f)]
    [TestCase(WindowResizeEdges.Left | WindowResizeEdges.Top, 130f, 140f, 370f, 260f)]
    [TestCase(WindowResizeEdges.Left | WindowResizeEdges.Bottom, 130f, 100f, 370f, 340f)]
    [TestCase(WindowResizeEdges.Right | WindowResizeEdges.Top, 100f, 140f, 430f, 260f)]
    [TestCase(WindowResizeEdges.Right | WindowResizeEdges.Bottom, 100f, 100f, 430f, 340f)]
    public void EachHandle_MovesOnlyItsEdgesAndHoldsOppositeEdges(
        WindowResizeEdges edges, float x, float y, float width, float height)
    {
        Assert.That(Calculate(edges, 30, 40), Is.EqualTo(new Rect(x, y, width, height)));
    }

    [TestCase(WindowResizeEdges.Left, 1000f, 0f, 300f, 100f, 200f, 300f)]
    [TestCase(WindowResizeEdges.Right, -1000f, 0f, 100f, 100f, 200f, 300f)]
    [TestCase(WindowResizeEdges.Top, 0f, 1000f, 100f, 250f, 400f, 150f)]
    [TestCase(WindowResizeEdges.Bottom, 0f, -1000f, 100f, 100f, 400f, 150f)]
    [TestCase(WindowResizeEdges.Left | WindowResizeEdges.Top, 1000f, 1000f, 300f, 250f, 200f, 150f)]
    [TestCase(WindowResizeEdges.Left | WindowResizeEdges.Bottom, 1000f, -1000f, 300f, 100f, 200f, 150f)]
    [TestCase(WindowResizeEdges.Right | WindowResizeEdges.Top, -1000f, 1000f, 100f, 250f, 200f, 150f)]
    [TestCase(WindowResizeEdges.Right | WindowResizeEdges.Bottom, -1000f, -1000f, 100f, 100f, 200f, 150f)]
    public void EachHandle_StopsAtMinimumWithoutFlippingOrMovingOppositeEdges(
        WindowResizeEdges edges, float dx, float dy, float x, float y, float width, float height)
    {
        Assert.That(Calculate(edges, dx, dy), Is.EqualTo(new Rect(x, y, width, height)));
    }

    [TestCase(WindowResizeEdges.Left, -1000f, 0f, 0f, 100f, 500f, 300f)]
    [TestCase(WindowResizeEdges.Right, 1000f, 0f, 100f, 100f, 900f, 300f)]
    [TestCase(WindowResizeEdges.Top, 0f, -1000f, 100f, 0f, 400f, 400f)]
    [TestCase(WindowResizeEdges.Bottom, 0f, 1000f, 100f, 100f, 400f, 700f)]
    [TestCase(WindowResizeEdges.Left | WindowResizeEdges.Top, -1000f, -1000f, 0f, 0f, 500f, 400f)]
    [TestCase(WindowResizeEdges.Left | WindowResizeEdges.Bottom, -1000f, 1000f, 0f, 100f, 500f, 700f)]
    [TestCase(WindowResizeEdges.Right | WindowResizeEdges.Top, 1000f, -1000f, 100f, 0f, 900f, 400f)]
    [TestCase(WindowResizeEdges.Right | WindowResizeEdges.Bottom, 1000f, 1000f, 100f, 100f, 900f, 700f)]
    public void EachHandle_StopsAtWorkspaceBoundaryWithoutMovingOppositeEdges(
        WindowResizeEdges edges, float dx, float dy, float x, float y, float width, float height)
    {
        Assert.That(Calculate(edges, dx, dy), Is.EqualTo(new Rect(x, y, width, height)));
    }

    [Test]
    public void ZeroMovement_KeepsStartingBounds()
    {
        Assert.That(Calculate(WindowResizeEdges.Right | WindowResizeEdges.Bottom, 0, 0),
            Is.EqualTo(Start));
    }

    [TestCase(WindowResizeEdges.Left, 0f, 1000f)]
    [TestCase(WindowResizeEdges.Top, 1000f, 0f)]
    public void SingleEdge_IgnoresMovementAlongOtherAxis(WindowResizeEdges edges, float dx, float dy)
    {
        Assert.That(Calculate(edges, dx, dy), Is.EqualTo(Start));
    }

    [Test]
    public void ReturningFromMinimumAndWorkspaceClamp_UsesOriginalStart()
    {
        var edges = WindowResizeEdges.Left | WindowResizeEdges.Top;
        Calculate(edges, 5000, 5000);
        Calculate(edges, -5000, -5000);
        Assert.That(Calculate(edges, 20, 30), Is.EqualTo(new Rect(120, 130, 380, 270)));
        Assert.That(Calculate(edges, 0, 0), Is.EqualTo(Start));
    }

    [TestCase(600f, 150f, 500f, 800f, -100f, 100f, 600f, 400f)]
    [TestCase(200f, 900f, 1000f, 800f, 100f, 0f, 500f, 900f)]
    [TestCase(600f, 900f, 500f, 800f, -100f, 0f, 600f, 900f)]
    public void NonfittingMinimum_PreservesRecoveryAxisAndResizesFittingAxis(
        float minWidth, float minHeight, float areaWidth, float areaHeight,
        float x, float y, float width, float height)
    {
        Rect result = WindowResize.CalculateRect(Start, Pointer, Pointer + new Vector2(100, 100),
            WindowResizeEdges.Right | WindowResizeEdges.Bottom,
            new Vector2(minWidth, minHeight), new Vector2(areaWidth, areaHeight));
        Assert.That(result, Is.EqualTo(new Rect(x, y, width, height)));
    }

    [Test]
    public void MinimumEqualToWorkspace_StaysExactFit()
    {
        Rect result = WindowResize.CalculateRect(Start, Pointer, Pointer + new Vector2(900, 900),
            WindowResizeEdges.Left | WindowResizeEdges.Top, Area, Area);
        Assert.That(result, Is.EqualTo(new Rect(0, 0, 1000, 800)));
    }

    [Test]
    public void OutOfBoundsStart_IsNormalizedBeforeApplyingResize()
    {
        Rect result = WindowResize.CalculateRect(new Rect(-50, -60, 1200, 900),
            Pointer, Pointer + new Vector2(100, 100),
            WindowResizeEdges.Left | WindowResizeEdges.Top, Minimum, Area);
        Assert.That(result, Is.EqualTo(new Rect(100, 100, 900, 700)));
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(12)]
    [TestCase(7)]
    [TestCase(11)]
    [TestCase(13)]
    [TestCase(14)]
    [TestCase(15)]
    [TestCase(16)]
    [TestCase(-1)]
    public void MissingOppositeOrUnknownEdges_AreRejected(int flags)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Calculate((WindowResizeEdges)flags, 10, 10));
        Assert.That(error.ParamName, Is.EqualTo("edges"));
    }

    [TestCase(true, true, float.NaN)]
    [TestCase(true, true, float.PositiveInfinity)]
    [TestCase(true, true, float.NegativeInfinity)]
    [TestCase(true, false, float.NaN)]
    [TestCase(true, false, float.PositiveInfinity)]
    [TestCase(true, false, float.NegativeInfinity)]
    [TestCase(false, true, float.NaN)]
    [TestCase(false, true, float.PositiveInfinity)]
    [TestCase(false, true, float.NegativeInfinity)]
    [TestCase(false, false, float.NaN)]
    [TestCase(false, false, float.PositiveInfinity)]
    [TestCase(false, false, float.NegativeInfinity)]
    public void InvalidPointer_IsRejected(bool starting, bool xAxis, float value)
    {
        Vector2 invalid = Pointer;
        if (xAxis) invalid.x = value;
        else invalid.y = value;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => WindowResize.CalculateRect(
            Start, starting ? invalid : Pointer, starting ? Pointer : invalid,
            WindowResizeEdges.Right, Minimum, Area));
        Assert.That(error.ParamName, Is.EqualTo(starting ? "startPointer" : "currentPointer"));
    }

    [TestCase("position")]
    [TestCase("size")]
    [TestCase("minimum")]
    [TestCase("workspace")]
    public void InvalidGeometry_IsRejected(string invalid)
    {
        Rect start = Start;
        Vector2 minimum = Minimum;
        Vector2 area = Area;
        if (invalid == "position") start.x = float.NaN;
        if (invalid == "size") start.height = 0;
        if (invalid == "minimum") minimum.x = 0;
        if (invalid == "workspace") area.y = float.PositiveInfinity;
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowResize.CalculateRect(
            start, Pointer, Pointer, WindowResizeEdges.Right, minimum, area));
    }

    [Test]
    public void OverflowingPointerDelta_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowResize.CalculateRect(
            Start, new Vector2(-float.MaxValue, 0), new Vector2(float.MaxValue, 0),
            WindowResizeEdges.Right, Minimum, Area));
    }

    private static Rect Calculate(WindowResizeEdges edges, float dx, float dy)
    {
        return WindowResize.CalculateRect(Start, Pointer, Pointer + new Vector2(dx, dy),
            edges, Minimum, Area);
    }
}
