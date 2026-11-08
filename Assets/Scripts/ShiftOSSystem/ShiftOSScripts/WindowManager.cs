using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WindowManager
{
    private readonly AppRegistry registry;
    private Vector2 workAreaSize;

    public Vector2 WorkAreaSize => workAreaSize;

    private readonly Dictionary<string, WindowState> states = new Dictionary<string, WindowState>(StringComparer.Ordinal);

    private readonly List<WindowState> windows = new List<WindowState>();

    public IReadOnlyList<WindowState> Windows { get; }

    public WindowManager(AppRegistry registry, Vector2 workAreaSize)
    {
        if (registry == null)
            throw new ArgumentNullException(nameof(registry));

        ValidateWorkAreaSize(workAreaSize, nameof(workAreaSize));

        this.registry = registry;
        this.workAreaSize = workAreaSize;
        Windows = windows.AsReadOnly();
    }

    public WindowState Launch(string appId)
    {
        AppRegistration registration = registry.Get(appId);

        if (!states.TryGetValue(appId, out WindowState state))
        {
            state = new WindowState(registration.Definition);

            Rect bounds = WindowBounds.ConstrainNormalRect(state.NormalRect, state.MinimumSize, workAreaSize);

            state.OpenNormally(bounds);
            states.Add(appId, state);
            windows.Add(state);
        }
        else
        {
            if (!state.IsOpen)
                windows.Add(state);

            state.Show();
        }

        Focus(appId);
        return state;
    }

    public void Focus(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException("An application ID is required.", nameof(appId));

        if (!states.TryGetValue(appId, out WindowState state))
            throw new KeyNotFoundException($"Application has not been launched: {appId}.");

        if (!state.IsOpen || state.IsMinimized)
            throw new InvalidOperationException("Only an open, visible window can receive focus.");

        for (int i = 0; i < windows.Count; i++)
            windows[i].SetFocused(false);

        state.SetFocused(true);

        windows.Remove(state);
        windows.Add(state);
    }

    public WindowState GetWindow(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException("An application ID is required.", nameof(appId));

        if (!states.TryGetValue(appId, out WindowState state))
            throw new KeyNotFoundException($"Application has not been launched: {appId}.");

        return state;
    }

    public void DragWindow(string appId, Rect startRect, Vector2 startPointer, Vector2 currentPointer)
    {
        WindowState state = GetWindow(appId);

        if (!state.IsOpen || state.IsMinimized || state.IsMaximized)
            throw new InvalidOperationException("Only an open, visible normal window can be dragged.");

        if (startRect.size != state.NormalRect.size)
            throw new ArgumentException("The saved drag size must match the window size.", nameof(startRect));

        Rect bounds = WindowDrag.CalculateRect(startRect, startPointer, currentPointer, workAreaSize);

        state.SetNormalRect(bounds);
    }

    public void ResizeWindow(string appId, Rect startRect, Vector2 startPointer, Vector2 currentPointer, WindowResizeEdges edges)
    {
        WindowState state = GetWindow(appId);

        if (!state.IsOpen || state.IsMinimized || state.IsMaximized)
            throw new InvalidOperationException("Only an open, visible normal window can be resized.");

        Rect bounds = WindowResize.CalculateRect(startRect, startPointer, currentPointer, edges, state.MinimumSize, workAreaSize);

        state.SetNormalRect(bounds);
    }

    public void MaximizeWindow(string appId)
    {
        WindowState state = GetWindow(appId);

        if (!state.IsOpen || state.IsMinimized)
            throw new InvalidOperationException("Only an open, visible window can be maximized.");

        state.SetMaximizedRect(new Rect(Vector2.zero, workAreaSize));
        Focus(appId);
    }

    public void RestoreNormalWindow(string appId)
    {
        WindowState state = GetWindow(appId);

        if (!state.IsOpen || state.IsMinimized)
            throw new InvalidOperationException("Only an open, visible window can return to normal bounds.");

        Rect bounds = WindowBounds.ConstrainNormalRect(state.NormalRect, state.MinimumSize, workAreaSize);

        state.SetNormalRect(bounds);
        Focus(appId);
    }

    public void MinimizeWindow(string appId)
    {
        WindowState state = GetWindow(appId);

        if (!state.IsOpen)
            throw new InvalidOperationException("Only an open window can be minimized.");

        if (state.IsMinimized)
            return;

        bool wasFocused = state.IsFocused;
        state.Minimize();

        if (wasFocused)
            FocusFrontmostVisibleWindow();
    }

    public void RestoreMinimizedWindow(string appId)
    {
        WindowState state = GetWindow(appId);

        if (!state.IsOpen)
            throw new InvalidOperationException("Launch a closed window before restoring it.");

        state.Show();
        Focus(appId);
    }

    public void CloseWindow(string appId)
    {
        WindowState state = GetWindow(appId);

        if (!state.IsOpen)
            return;

        bool wasFocused = state.IsFocused;
        state.Close();
        windows.Remove(state);

        if (wasFocused)
            FocusFrontmostVisibleWindow();
    }

    private void FocusFrontmostVisibleWindow()
    {
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            WindowState candidate = windows[i];

            if (candidate.IsOpen && !candidate.IsMinimized)
            {
                Focus(candidate.AppId);
                return;
            }
        }
    }

    public bool SetWorkAreaSize(Vector2 newSize)
    {
        ValidateWorkAreaSize(newSize, nameof(newSize));

        if (workAreaSize.Equals(newSize))
            return false;

        workAreaSize = newSize;

        foreach (WindowState state in states.Values)
        {
            if (state.IsMaximized)
            {
                state.SetMaximizedRect(new Rect(Vector2.zero, newSize));
            }
            else
            {
                Rect bounds = WindowBounds.ConstrainNormalRect(state.NormalRect, state.MinimumSize, newSize);

                state.SetNormalRect(bounds);
            }
        }

        return true;
    }

    private static void ValidateWorkAreaSize(Vector2 size, string parameterName)
    {
        if (float.IsNaN(size.x) || float.IsInfinity(size.x) || float.IsNaN(size.y) || float.IsInfinity(size.y) || size.x <= 0f || size.y <= 0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Work area must be finite and positive.");
        }
    }
}