using System;
using UnityEngine;

public static class WindowResize
{
    public static Rect CalculateRect(Rect startRect, Vector2 startPointer, Vector2 currentPointer, WindowResizeEdges edges, Vector2 minimumSize, Vector2 workAreaSize)
    {
        const WindowResizeEdges horizontal = WindowResizeEdges.Left | WindowResizeEdges.Right;

        const WindowResizeEdges vertical = WindowResizeEdges.Top | WindowResizeEdges.Bottom;

        const WindowResizeEdges all = horizontal | vertical;

        if (edges == WindowResizeEdges.None || (edges & ~all) != WindowResizeEdges.None || (edges & horizontal) == horizontal || (edges & vertical) == vertical)
        {
            throw new ArgumentOutOfRangeException(nameof(edges), "Choose one edge or two adjacent edges.");
        }

        if (!IsFinite(startPointer))
            throw new ArgumentOutOfRangeException(nameof(startPointer));

        if (!IsFinite(currentPointer))
            throw new ArgumentOutOfRangeException(nameof(currentPointer));

        Vector2 delta = currentPointer - startPointer;

        if (!IsFinite(delta))
            throw new ArgumentOutOfRangeException(nameof(currentPointer), "Pointer movement must remain finite.");

        Rect start = WindowBounds.ConstrainNormalRect(startRect, minimumSize, workAreaSize);

        float left = start.xMin;
        float right = start.xMax;
        float top = start.yMin;
        float bottom = start.yMax;

        if (minimumSize.x <= workAreaSize.x)
        {
            if ((edges & WindowResizeEdges.Left) != WindowResizeEdges.None)
            {
                left = Mathf.Clamp(left + delta.x, 0f, right - minimumSize.x);
            }
            else if ((edges & WindowResizeEdges.Right) != WindowResizeEdges.None)
            {
                right = Mathf.Clamp(right + delta.x, left + minimumSize.x, workAreaSize.x);
            }
        }

        if (minimumSize.y <= workAreaSize.y)
        {
            if ((edges & WindowResizeEdges.Top) != WindowResizeEdges.None)
            {
                top = Mathf.Clamp(top + delta.y, 0f, bottom - minimumSize.y);
            }
            else if ((edges & WindowResizeEdges.Bottom) != WindowResizeEdges.None)
            {
                bottom = Mathf.Clamp(bottom + delta.y, top + minimumSize.y, workAreaSize.y);
            }
        }

        return Rect.MinMaxRect(left, top, right, bottom);
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}