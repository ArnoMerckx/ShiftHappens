using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public sealed class WindowResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField]
    private WindowResizeEdges edges = WindowResizeEdges.Right;

    private BaseWindow window;
    private int pointerId;

    public string AppId => window != null ? window.AppId : null;
    public WindowResizeEdges Edges => edges;
    public bool IsResizing { get; private set; }

    public event Action<WindowResizeHandle, PointerEventData> ResizeStarted;
    public event Action<WindowResizeHandle, PointerEventData> ResizeMoved;
    public event Action<WindowResizeHandle, PointerEventData> ResizeEnded;
    public event Action<WindowResizeHandle> ResizeCancelled;

    private void Awake()
    {
        window = GetComponentInParent<BaseWindow>(true);

        if (window == null)
            throw new InvalidOperationException("WindowResizeHandle needs a parent BaseWindow.");

        const WindowResizeEdges horizontal = WindowResizeEdges.Left | WindowResizeEdges.Right;

        const WindowResizeEdges vertical = WindowResizeEdges.Top | WindowResizeEdges.Bottom;

        const WindowResizeEdges all = horizontal | vertical;

        if (edges == WindowResizeEdges.None || (edges & ~all) != WindowResizeEdges.None || (edges & horizontal) == horizontal || (edges & vertical) == vertical)
        {
            throw new InvalidOperationException("Choose one resize edge or two adjacent edges.");
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsResizing || eventData.button != PointerEventData.InputButton.Left || AppId == null)
        {
            return;
        }

        GameObject pressed = eventData.pointerPressRaycast.gameObject;

        if (pressed == null || pressed.GetComponentInParent<Selectable>(true) != null)
        {
            return;
        }

        pointerId = eventData.pointerId;
        IsResizing = true;
        ResizeStarted?.Invoke(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsResizing && eventData.pointerId == pointerId && eventData.button == PointerEventData.InputButton.Left)
        {
            ResizeMoved?.Invoke(this, eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsResizing || eventData.pointerId != pointerId)
            return;

        IsResizing = false;
        ResizeEnded?.Invoke(this, eventData);
    }

    public void CancelResize()
    {
        if (!IsResizing)
            return;

        IsResizing = false;
        ResizeCancelled?.Invoke(this);
    }

    private void OnDisable()
    {
        CancelResize();
    }
}