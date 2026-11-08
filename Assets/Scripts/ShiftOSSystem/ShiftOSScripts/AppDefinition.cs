using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewApp", menuName = "Shift Happens/ShiftOS/Application")]
public sealed class AppDefinition : ScriptableObject
{
    [SerializeField] private string appId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [SerializeField] private Vector2 defaultPosition = new Vector2(40, 40);
    [SerializeField] private Vector2 defaultSize = new Vector2(480, 360);
    [SerializeField] private Vector2 minimumSize = new Vector2(280, 200);

    public string AppId => appId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public Vector2 DefaultPosition => defaultPosition;
    public Vector2 DefaultSize => defaultSize;
    public Vector2 MinimumSize => minimumSize;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new InvalidOperationException("An application ID is required.");

        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("An application name is required.");

        if (!IsFinite(defaultPosition.x) || !IsFinite(defaultPosition.y) || defaultPosition.x < 0 || defaultPosition.y < 0)
        {
            throw new InvalidOperationException(
                "Default position must be finite and nonnegative.");
        }

        if (!IsValidSize(defaultSize) || !IsValidSize(minimumSize))
        {
            throw new InvalidOperationException("Window sizes must be finite and positive.");
        }

        if (defaultSize.x < minimumSize.x || defaultSize.y < minimumSize.y)
        {
            throw new InvalidOperationException("Default size cannot be smaller than minimum size.");
        }
    }

    private static bool IsValidSize(Vector2 size)
    {
        return IsFinite(size.x) && IsFinite(size.y) && size.x > 0 && size.y > 0;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}