using System;
using NUnit.Framework;
using UnityEngine;

public class WindowDragTests
{
    private static readonly Rect Start = new Rect(40, 50, 480, 360);
    private static readonly Vector2 Pointer = new Vector2(120, 80);
    private static readonly Vector2 Area = new Vector2(1000, 800);

    [TestCase(0f, 0f, 40f, 50f)]
    [TestCase(25f, 35f, 65f, 85f)]
    [TestCase(-20f, -30f, 20f, 20f)]
    [TestCase(-1000f, 0f, 0f, 50f)]
    [TestCase(1000f, 0f, 520f, 50f)]
    [TestCase(0f, -1000f, 40f, 0f)]
    [TestCase(0f, 1000f, 40f, 440f)]
    [TestCase(1000f, -1000f, 520f, 0f)]
    public void Translation_UsesTopLeftCoordinatesAndPreservesSize(
        float dx, float dy, float expectedX, float expectedY)
    {
        Rect result = WindowDrag.CalculateRect(
            Start, Pointer, Pointer + new Vector2(dx, dy), Area);
        Assert.That(result, Is.EqualTo(new Rect(expectedX, expectedY, 480, 360)));
        Assert.That(Start, Is.EqualTo(new Rect(40, 50, 480, 360)));
    }

    [Test]
    public void ReturningToStartingPointer_AfterClampingRestoresStartingBounds()
    {
        Rect edge = WindowDrag.CalculateRect(Start, Pointer, new Vector2(5000, 5000), Area);
        Assert.That(edge.position, Is.EqualTo(new Vector2(520, 440)));
        Rect returned = WindowDrag.CalculateRect(Start, Pointer, Pointer, Area);
        Assert.That(returned, Is.EqualTo(Start));
    }

    [Test]
    public void FinalPosition_IsIndependentOfIntermediatePointerSamples()
    {
        Vector2 final = Pointer + new Vector2(70, 90);
        Rect direct = WindowDrag.CalculateRect(Start, Pointer, final, Area);
        WindowDrag.CalculateRect(Start, Pointer, new Vector2(-500, 7000), Area);
        WindowDrag.CalculateRect(Start, Pointer, new Vector2(3000, -900), Area);
        Rect sampled = WindowDrag.CalculateRect(Start, Pointer, final, Area);
        Assert.That(sampled, Is.EqualTo(new Rect(110, 140, 480, 360)));
        Assert.That(sampled, Is.EqualTo(direct));
    }

    [TestCase(300f, 200f, -180f, 0f)]
    [TestCase(300f, 800f, -180f, 140f)]
    [TestCase(1000f, 200f, 110f, 0f)]
    public void NonfittingWindow_PreservesSizeAndKeepsRightTopRecovery(
        float width, float height, float expectedX, float expectedY)
    {
        Rect result = WindowDrag.CalculateRect(Start, Pointer,
            Pointer + new Vector2(70, 90), new Vector2(width, height));
        Assert.That(result, Is.EqualTo(new Rect(expectedX, expectedY, 480, 360)));
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
    public void NonfinitePointer_IsRejectedWithCorrectParameter(
        bool starting, bool xAxis, float value)
    {
        Vector2 invalid = Pointer;
        if (xAxis) invalid.x = value;
        else invalid.y = value;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowDrag.CalculateRect(Start, starting ? invalid : Pointer,
                starting ? Pointer : invalid, Area));
        Assert.That(error.ParamName, Is.EqualTo(starting ? "startPointer" : "currentPointer"));
    }

    [TestCase(0f, 360f)]
    [TestCase(480f, -1f)]
    public void InvalidStartingSize_IsRejected(float width, float height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowDrag.CalculateRect(
            new Rect(40, 50, width, height), Pointer, Pointer, Area));
    }

    [TestCase(0f, 800f)]
    [TestCase(1000f, float.NaN)]
    public void InvalidWorkArea_IsRejected(float width, float height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowDrag.CalculateRect(
            Start, Pointer, Pointer, new Vector2(width, height)));
    }

    [Test]
    public void FinitePointersWhoseDeltaOverflows_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowDrag.CalculateRect(
            Start, new Vector2(-float.MaxValue, 0), new Vector2(float.MaxValue, 0), Area));
    }
}
