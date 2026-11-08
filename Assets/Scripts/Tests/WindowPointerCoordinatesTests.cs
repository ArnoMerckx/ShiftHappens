using System;
using NUnit.Framework;
using UnityEngine;

public class WindowPointerCoordinatesTests
{
    private GameObject root;
    private GameObject cameraRoot;
    private RectTransform area;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Temporary work area", typeof(RectTransform));
        area = root.GetComponent<RectTransform>();
        area.sizeDelta = new Vector2(800, 600);
        area.pivot = new Vector2(0.5f, 0.5f);
        area.position = new Vector3(350, 450, 0);
    }

    [TearDown]
    public void TearDown()
    {
        if (cameraRoot != null)
            UnityEngine.Object.DestroyImmediate(cameraRoot);
        if (root != null)
            UnityEngine.Object.DestroyImmediate(root);
    }

    [TestCase(0f, 1f, 1f)]
    [TestCase(0.5f, 0.5f, 1f)]
    [TestCase(1f, 0f, 1f)]
    [TestCase(0.25f, 0.75f, 1f)]
    [TestCase(0f, 1f, 0.5f)]
    [TestCase(0.5f, 0.5f, 0.5f)]
    [TestCase(0f, 1f, 2f)]
    [TestCase(0.5f, 0.5f, 2f)]
    public void OverlayConversion_UsesLogicalUnitsRegardlessOfPivotAndScale(
        float pivotX, float pivotY, float scale)
    {
        area.pivot = new Vector2(pivotX, pivotY);
        area.localScale = new Vector3(scale, scale, 1);
        AssertPoint(new Vector2(120, 90));
    }

    [TestCase(0f, 0f)]
    [TestCase(800f, 600f)]
    [TestCase(400f, 300f)]
    [TestCase(-25f, 100f)]
    [TestCase(900f, 100f)]
    [TestCase(100f, -25f)]
    [TestCase(100f, 700f)]
    public void CornersCentreAndOutsidePoints_KeepTopLeftCoordinates(float x, float y)
    {
        AssertPoint(new Vector2(x, y));
    }

    [Test]
    public void RotatedAndNonuniformlyScaledArea_UsesItsLocalAxes()
    {
        area.localRotation = Quaternion.Euler(0, 0, 25);
        area.localScale = new Vector3(1.5f, 0.75f, 1);
        AssertPoint(new Vector2(150, 220));
    }

    [Test]
    public void MissingArea_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WindowPointerCoordinates.TryScreenToWorkArea(null, Vector2.zero, null, out _));
    }

    [TestCase(true, float.NaN)]
    [TestCase(true, float.PositiveInfinity)]
    [TestCase(true, float.NegativeInfinity)]
    [TestCase(false, float.NaN)]
    [TestCase(false, float.PositiveInfinity)]
    [TestCase(false, float.NegativeInfinity)]
    public void NonfiniteScreenPoint_ReturnsFalseAndZero(bool xAxis, float value)
    {
        Vector2 screen = new Vector2(350, 450);
        if (xAxis) screen.x = value;
        else screen.y = value;
        bool success = WindowPointerCoordinates.TryScreenToWorkArea(
            area, screen, null, out Vector2 point);
        Assert.That(success, Is.False);
        Assert.That(point, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void PlaneBehindOverlayRay_ReturnsFalseAndZero()
    {
        // Unity's null-camera screen ray starts at z=-100 and points forward.
        area.position = new Vector3(350, 450, -200);
        bool success = WindowPointerCoordinates.TryScreenToWorkArea(
            area, new Vector2(350, 450), null, out Vector2 point);
        Assert.That(success, Is.False);
        Assert.That(point, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void SuppliedEventCamera_ProjectsToTheWorkAreaPlane()
    {
        cameraRoot = new GameObject("Temporary event camera", typeof(Camera));
        Camera camera = cameraRoot.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.pixelRect = new Rect(0, 0, 800, 600);
        camera.aspect = 4f / 3f;
        camera.transform.position = new Vector3(0, 0, -10);
        area.position = Vector3.zero;
        area.localScale = new Vector3(0.01f, 0.01f, 1);
        Vector2 expected = new Vector2(120, 90);
        Vector3 world = WorldPoint(expected);
        Vector3 screen = camera.WorldToScreenPoint(world);
        Assert.That(WindowPointerCoordinates.TryScreenToWorkArea(
            area, new Vector2(screen.x, screen.y), camera, out Vector2 point), Is.True);
        AssertCoordinates(point, expected);
    }

    private void AssertPoint(Vector2 expected)
    {
        Vector3 world = WorldPoint(expected);
        bool success = WindowPointerCoordinates.TryScreenToWorkArea(
            area, new Vector2(world.x, world.y), null, out Vector2 point);
        Assert.That(success, Is.True);
        AssertCoordinates(point, expected);
    }

    private Vector3 WorldPoint(Vector2 point)
    {
        Rect rect = area.rect;
        return area.TransformPoint(new Vector3(rect.xMin + point.x, rect.yMax - point.y, 0));
    }

    private static void AssertCoordinates(Vector2 actual, Vector2 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
    }
}
