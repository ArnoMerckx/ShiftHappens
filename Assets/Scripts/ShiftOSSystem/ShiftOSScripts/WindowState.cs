using System;
using UnityEngine;

public sealed class WindowState
{
    public string AppId { get; }
    public Vector2 MinimumSize { get; }

    public Rect NormalRect { get; private set; }
    public Rect CurrentRect { get; private set; }

    public bool IsOpen { get; private set; }
    public bool IsMinimized { get; private set; }
    public bool IsMaximized { get; private set; }
    public bool IsFocused { get; private set; }

    public WindowState(AppDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        definition.Validate();

        AppId = definition.AppId;
        MinimumSize = definition.MinimumSize;

        NormalRect = new Rect(definition.DefaultPosition, definition.DefaultSize);

        CurrentRect = NormalRect;
    }

    internal void OpenNormally(Rect bounds)
    {
        NormalRect = bounds;
        CurrentRect = bounds;
        IsOpen = true;
        IsMinimized = false;
        IsMaximized = false;
    }

    internal void SetFocused(bool focused)
    {
        IsFocused = focused;
    }

    internal void SetNormalRect(Rect bounds)
    {
        NormalRect = bounds;
        CurrentRect = bounds;
        IsMaximized = false;
    }

    internal void SetMaximizedRect(Rect bounds)
    {
        CurrentRect = bounds;
        IsMaximized = true;
    }

    internal void Show()
    {
        IsOpen = true;
        IsMinimized = false;
    }

    internal void Minimize()
    {
        IsMinimized = true;
        IsFocused = false;
    }

    internal void Close()
    {
        IsOpen = false;
        IsMinimized = false;
        IsFocused = false;
    }
}