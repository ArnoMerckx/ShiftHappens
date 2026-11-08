using System;
using UnityEngine;

public static class WindowDrag
{
    public static Rect CalculateRect(Rect startRect, Vector2 startPointer, Vector2 currentPointer, Vector2 workAreaSize)
    {
        if (!IsFinite(startPointer))
            throw new ArgumentOutOfRangeException(nameof(startPointer), "The starting pointer position must be finite.");

        if (!IsFinite(currentPointer))
            throw new ArgumentOutOfRangeException(nameof(currentPointer), "The current pointer position must be finite.");

        Vector2 delta = currentPointer - startPointer;

        Rect requested = new Rect(startRect.position + delta, startRect.size);

        return WindowBounds.ConstrainNormalRect(requested, startRect.size, workAreaSize);
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}