using System;
using UnityEngine;

public static class WindowBounds
{
    public static Rect ConstrainNormalRect(
        Rect requested, Vector2 minimumSize, Vector2 workAreaSize)
    {
        if (!IsFinite(requested.x) || !IsFinite(requested.y) || !IsPositiveSize(requested.size))
        {
            throw new ArgumentOutOfRangeException(nameof(requested), "Window bounds must be finite with positive size.");
        }

        if (!IsPositiveSize(minimumSize))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSize), "Minimum size must be finite and positive.");
        }

        if (!IsPositiveSize(workAreaSize))
        {
            throw new ArgumentOutOfRangeException(nameof(workAreaSize), "Work area must be finite and positive.");
        }

        float width = Mathf.Max(minimumSize.x, Mathf.Min(requested.width, workAreaSize.x));

        float height = Mathf.Max(minimumSize.y, Mathf.Min(requested.height, workAreaSize.y));

        float x = width > workAreaSize.x ? workAreaSize.x - width : Mathf.Clamp(requested.x, 0f, workAreaSize.x - width);

        float y = height > workAreaSize.y ? 0f : Mathf.Clamp(requested.y, 0f, workAreaSize.y - height);

        return new Rect(x, y, width, height);
    }

    private static bool IsPositiveSize(Vector2 size)
    {
        return IsFinite(size.x) && IsFinite(size.y) && size.x > 0 && size.y > 0;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}