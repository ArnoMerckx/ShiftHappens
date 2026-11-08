using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

[DisallowMultipleComponent]
public sealed class WindowManagerController : MonoBehaviour
{
    [SerializeField]
    private List<AppRegistration> applications = new List<AppRegistration>();

    [SerializeField] private RectTransform workArea;
    [SerializeField] private RectTransform windowLayer;

    [SerializeField] private WorkdaySessionDriver sessionDriver;

    private AppRegistry registry;
    private WindowManager manager;

    private WindowTitleBarDrag activeDrag;

    private Rect dragStartRect;
    private Vector2 dragStartPointer;
    private Camera dragEventCamera;

    private WindowResizeHandle activeResize;
    private Rect resizeStartRect;
    private Vector2 resizeStartPointer;
    private Camera resizeEventCamera;
    private WindowResizeEdges resizeEdges;

    private Canvas desktopCanvas;
    private bool hasWorkspaceSnapshot;
    private bool workspaceWasVisible;

    private Vector2Int capturedScreenSize;
    private float capturedCanvasScale;
    private Rect capturedWorkAreaRect;
    private Matrix4x4 capturedWorkAreaMatrix;

    private bool CanHandlePointerEvents => isActiveAndEnabled && Application.isFocused && workArea.gameObject.activeInHierarchy && windowLayer.gameObject.activeInHierarchy;

    private readonly Dictionary<string, BaseWindow> views = new Dictionary<string, BaseWindow>(StringComparer.Ordinal);

    public event Action WindowsChanged;

    public ShiftOSAppState AppState { get; } = new ShiftOSAppState();

    private void OnDisable()
    {
        TryCancelPointerOperation();
    }

    private void Awake()
    {
        if (workArea == null || windowLayer == null)
            throw new InvalidOperationException("Assign the desktop work area and window layer.");

        if (windowLayer != workArea && !windowLayer.IsChildOf(workArea))
        {
            throw new InvalidOperationException("The window layer must belong to the work area.");
        }

        desktopCanvas = workArea.GetComponentInParent<Canvas>(true);

        if (desktopCanvas == null)
            throw new InvalidOperationException("The work area needs a parent Canvas.");

        desktopCanvas = desktopCanvas.rootCanvas;

        registry = new AppRegistry(applications);
    }

    public void Launch(string appId)
    {
        RequireVisibleDesktop();

        AppRegistration registration = registry.Get(appId);

        if (registration.ContentPrefab != null && (sessionDriver == null || sessionDriver.Session == null))
        {
            throw new InvalidOperationException("Assign the existing workday session driver before creating app content.");
        }

        if (manager == null)
        {
            Canvas.ForceUpdateCanvases();
            manager = new WindowManager(registry, workArea.rect.size);
        }

        CaptureWorkspaceSnapshot();

        WindowState state = manager.Launch(appId);

        if (!views.ContainsKey(state.AppId))
        {

            BaseWindow view = Instantiate(registration.WindowPrefab, windowLayer, false);

            view.name = registration.Definition.DisplayName + " Window";
            view.Bind(registration.Definition);

            if (registration.ContentPrefab != null)
            {
                for (int i = 0; i < view.ContentRoot.childCount; i++)
                    view.ContentRoot.GetChild(i).gameObject.SetActive(false);

                ShiftOSAppView content = Instantiate(registration.ContentPrefab, view.ContentRoot, false);

                content.Initialize(sessionDriver.Session, AppState);
            }

            view.FocusRequested += Focus;
            view.MaximizeToggleRequested += ToggleMaximize;
            view.MinimizeRequested += Minimize;
            view.CloseRequested += Close;

            WindowTitleBarDrag drag = view.GetComponentInChildren<WindowTitleBarDrag>(true);

            if (drag == null)
                throw new InvalidOperationException("The window prefab needs a WindowTitleBarDrag component.");

            drag.DragStarted += OnDragStarted;
            drag.DragMoved += OnDragMoved;
            drag.DragEnded += OnDragEnded;
            drag.DragCancelled += OnDragCancelled;

            WindowResizeHandle[] handles = view.GetComponentsInChildren<WindowResizeHandle>(true);

            if (handles.Length != 8)
                throw new InvalidOperationException("The window prefab needs eight resize handles.");

            var registeredEdges = new HashSet<WindowResizeEdges>();

            for (int i = 0; i < handles.Length; i++)
            {
                WindowResizeHandle handle = handles[i];

                if (!registeredEdges.Add(handle.Edges))
                    throw new InvalidOperationException("Resize handles must have distinct edge selections.");

                handle.ResizeStarted += OnResizeStarted;
                handle.ResizeMoved += OnResizeMoved;
                handle.ResizeEnded += OnResizeEnded;
                handle.ResizeCancelled += OnResizeCancelled;
            }

            views.Add(state.AppId, view);
        }

        RefreshViews();
    }

    public void Focus(string appId)
    {
        RequireVisibleDesktop();

        if (manager == null)
            throw new InvalidOperationException("Launch a window before requesting focus.");

        manager.Focus(appId);
        RefreshViews();
    }

    public void ToggleMaximize(string appId)
    {
        RequireVisibleDesktop();

        if (manager == null)
            throw new InvalidOperationException("Launch a window before changing its window mode.");

        WindowState state = manager.GetWindow(appId);
        TryCancelPointerOperation();

        if (state.IsMaximized)
            manager.RestoreNormalWindow(appId);
        else
            manager.MaximizeWindow(appId);

        RefreshViews();
    }

    public void Minimize(string appId)
    {
        RequireVisibleDesktop();

        if (manager == null)
            throw new InvalidOperationException("Launch a window before minimizing it.");

        manager.GetWindow(appId);
        TryCancelPointerOperation();

        manager.MinimizeWindow(appId);
        RefreshViews();
    }

    public void Close(string appId)
    {
        RequireVisibleDesktop();

        if (manager == null)
            throw new InvalidOperationException("Launch a window before closing it.");

        manager.GetWindow(appId);
        TryCancelPointerOperation();

        manager.CloseWindow(appId);

        if (views.TryGetValue(appId, out BaseWindow view))
        {
            views.Remove(appId);
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        RefreshViews();
    }

    private void RefreshViews()
    {
        for (int i = 0; i < manager.Windows.Count; i++)
        {
            WindowState state = manager.Windows[i];
            BaseWindow view = views[state.AppId];

            view.ApplyState(state);
            view.transform.SetAsLastSibling();
        }

        WindowsChanged?.Invoke();
    }

    private void RequireVisibleDesktop()
    {
        if (!isActiveAndEnabled || !workArea.gameObject.activeInHierarchy || !windowLayer.gameObject.activeInHierarchy)
        {
            throw new InvalidOperationException("Window commands require the desktop to be visible.");
        }

        SynchronizeWorkspace();
    }

    public bool TryConsumeBack()
    {
        if (!CanHandlePointerEvents)
            return false;

        if (TryCancelPointerOperation())
            return true;

        if (manager == null)
            return false;

        for (int i = manager.Windows.Count - 1; i >= 0; i--)
        {
            WindowState state = manager.Windows[i];

            if (!state.IsOpen || state.IsMinimized || !views.TryGetValue(state.AppId, out BaseWindow view))
            {
                continue;
            }

            foreach (TMP_Dropdown dropdown in view.GetComponentsInChildren<TMP_Dropdown>())
            {
                if (!dropdown.IsExpanded)
                    continue;

                dropdown.Hide();
                return true;
            }
        }

        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
        {
            return false;
        }

        TMP_InputField input = eventSystem.currentSelectedGameObject.GetComponentInParent<TMP_InputField>();

        if (input == null || !input.isActiveAndEnabled || !input.isFocused || !input.transform.IsChildOf(windowLayer))
        {
            return false;
        }

        input.OnCancel(new BaseEventData(eventSystem));
        eventSystem.SetSelectedGameObject(null);
        return true;
    }

    public bool TryCancelPointerOperation()
    {
        WindowTitleBarDrag drag = activeDrag;
        WindowResizeHandle resize = activeResize;

        activeDrag = null;
        activeResize = null;
        dragEventCamera = null;
        resizeEventCamera = null;

        bool cancelled = false;

        if (drag != null)
        {
            cancelled |= drag.IsDragging;
            drag.CancelDrag();
        }

        if (resize != null)
        {
            cancelled |= resize.IsResizing;
            resize.CancelResize();
        }

        return cancelled;
    }

    private void OnDragStarted(WindowTitleBarDrag source, PointerEventData eventData)
    {
        if (!CanHandlePointerEvents || manager == null || SynchronizeWorkspace())
        {
            source.CancelDrag();
            return;
        }

        TryCancelPointerOperation();

        WindowState state = manager.GetWindow(source.AppId);

        if (!state.IsOpen || state.IsMinimized || state.IsMaximized)
        {
            source.CancelDrag();
            return;
        }

        Camera eventCamera = eventData.pressEventCamera;

        if (!WindowPointerCoordinates.TryScreenToWorkArea(workArea, eventData.pressPosition, eventCamera, out Vector2 startPointer))
        {
            source.CancelDrag();
            return;
        }

        dragStartRect = state.NormalRect;
        dragStartPointer = startPointer;
        dragEventCamera = eventCamera;
        activeDrag = source;

        Focus(source.AppId);
    }

    private void OnDragMoved(WindowTitleBarDrag source, PointerEventData eventData)
    {
        if (activeDrag != source)
            return;

        if (!CanHandlePointerEvents || SynchronizeWorkspace())
        {
            TryCancelPointerOperation();
            return;
        }

        if (!WindowPointerCoordinates.TryScreenToWorkArea(workArea, eventData.position, dragEventCamera, out Vector2 currentPointer))
        {
            TryCancelPointerOperation();
            return;
        }

        manager.DragWindow(source.AppId, dragStartRect, dragStartPointer, currentPointer);

        RefreshViews();
    }

    private void OnDragEnded(WindowTitleBarDrag source, PointerEventData eventData)
    {
        if (activeDrag != source)
            return;

        OnDragMoved(source, eventData);
        OnDragCancelled(source);
    }

    private void OnDragCancelled(WindowTitleBarDrag source)
    {
        if (activeDrag != source)
            return;

        activeDrag = null;
        dragEventCamera = null;
    }

    private void OnResizeStarted(WindowResizeHandle source, PointerEventData eventData)
    {
        if (!CanHandlePointerEvents || manager == null || SynchronizeWorkspace())
        {
            source.CancelResize();
            return;
        }

        TryCancelPointerOperation();

        WindowState state = manager.GetWindow(source.AppId);

        if (!state.IsOpen || state.IsMinimized || state.IsMaximized)
        {
            source.CancelResize();
            return;
        }

        Camera eventCamera = eventData.pressEventCamera;

        if (!WindowPointerCoordinates.TryScreenToWorkArea(workArea, eventData.pressPosition, eventCamera, out Vector2 startPointer))
        {
            source.CancelResize();
            return;
        }

        resizeStartRect = state.NormalRect;
        resizeStartPointer = startPointer;
        resizeEventCamera = eventCamera;
        resizeEdges = source.Edges;
        activeResize = source;

        Focus(source.AppId);
    }

    private void OnResizeMoved(WindowResizeHandle source, PointerEventData eventData)
    {
        if (activeResize != source)
            return;

        if (!CanHandlePointerEvents || SynchronizeWorkspace())
        {
            TryCancelPointerOperation();
            return;
        }

        if (!WindowPointerCoordinates.TryScreenToWorkArea(workArea, eventData.position, resizeEventCamera, out Vector2 currentPointer))
        {
            TryCancelPointerOperation();
            return;
        }

        manager.ResizeWindow(source.AppId, resizeStartRect, resizeStartPointer, currentPointer, resizeEdges);

        RefreshViews();
    }

    private void OnResizeEnded(WindowResizeHandle source, PointerEventData eventData)
    {
        if (activeResize != source)
            return;

        OnResizeMoved(source, eventData);
        OnResizeCancelled(source);
    }

    private void OnResizeCancelled(WindowResizeHandle source)
    {
        if (activeResize != source)
            return;

        activeResize = null;
        resizeEventCamera = null;
    }

    public List<AppDefinition> GetApplicationDefinitions()
    {
        if (registry == null)
            throw new InvalidOperationException("Application definitions are available after Awake.");

        var definitions = new List<AppDefinition>(applications.Count);

        for (int i = 0; i < applications.Count; i++)
            definitions.Add(applications[i].Definition);

        return definitions;
    }

    public List<WindowState> GetOpenWindows()
    {
        if (manager == null)
            return new List<WindowState>();

        return new List<WindowState>(manager.Windows);
    }

    public void ActivateRunningWindow(string appId)
    {
        RequireVisibleDesktop();

        if (manager == null)
            throw new InvalidOperationException("Launch a window before activating a running entry.");

        WindowState state = manager.GetWindow(appId);

        if (!state.IsOpen)
            return;

        TryCancelPointerOperation();

        if (state.IsMinimized)
            manager.RestoreMinimizedWindow(appId);
        else
            manager.Focus(appId);

        RefreshViews();
    }

    private void LateUpdate()
    {
        SynchronizeWorkspace();
    }

    private void CaptureWorkspaceSnapshot()
    {
        capturedScreenSize = new Vector2Int(Screen.width, Screen.height);

        capturedCanvasScale = desktopCanvas.scaleFactor;
        capturedWorkAreaRect = workArea.rect;
        capturedWorkAreaMatrix = workArea.localToWorldMatrix;

        hasWorkspaceSnapshot = true;
        workspaceWasVisible = true;
    }

    private bool SynchronizeWorkspace()
    {
        if (manager == null || !workArea.gameObject.activeInHierarchy || !windowLayer.gameObject.activeInHierarchy)
        {
            workspaceWasVisible = false;
            return false;
        }

        var screenSize = new Vector2Int(Screen.width, Screen.height);
        float scale = desktopCanvas.scaleFactor;
        Rect rect = workArea.rect;
        Matrix4x4 matrix = workArea.localToWorldMatrix;

        bool changed = !hasWorkspaceSnapshot || !workspaceWasVisible || screenSize != capturedScreenSize || !scale.Equals(capturedCanvasScale) || !rect.Equals(capturedWorkAreaRect) || !matrix.Equals(capturedWorkAreaMatrix) || !rect.size.Equals(manager.WorkAreaSize);

        if (!changed)
            return false;

        TryCancelPointerOperation();
        Canvas.ForceUpdateCanvases();

        rect = workArea.rect;
        scale = desktopCanvas.scaleFactor;

        if (!IsFinitePositive(rect.width) || !IsFinitePositive(rect.height) || !IsFinitePositive(scale))
        {
            hasWorkspaceSnapshot = false;
            workspaceWasVisible = false;
            return true;
        }

        bool boundsChanged = manager.SetWorkAreaSize(rect.size);
        CaptureWorkspaceSnapshot();

        if (boundsChanged)
            RefreshViews();

        return true;
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}