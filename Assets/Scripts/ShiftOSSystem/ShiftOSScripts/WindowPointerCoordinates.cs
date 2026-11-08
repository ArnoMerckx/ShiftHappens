using System;
using UnityEngine;

public static class WindowPointerCoordinates
{
    public static bool TryScreenToWorkArea(RectTransform workArea, Vector2 screenPoint, Camera eventCamera, out Vector2 point)
    {
        point = Vector2.zero;

        if (workArea == null)
            throw new ArgumentNullException(nameof(workArea));

        if (!IsFinite(screenPoint))
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(workArea, screenPoint, eventCamera, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = workArea.rect;

        Vector2 converted = new Vector2(localPoint.x - rect.xMin, rect.yMax - localPoint.y);

        if (!IsFinite(converted))
            return false;

        point = converted;
        return true;
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}