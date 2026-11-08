using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WindowTitleBarDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private BaseWindow window;
    private int pointerId;

    public string AppId => window != null ? window.AppId : null;
    public bool IsDragging { get; private set; }

    public event Action<WindowTitleBarDrag, PointerEventData> DragStarted;
    public event Action<WindowTitleBarDrag, PointerEventData> DragMoved;
    public event Action<WindowTitleBarDrag, PointerEventData> DragEnded;
    public event Action<WindowTitleBarDrag> DragCancelled;

    private void Awake()
    {
        window = GetComponentInParent<BaseWindow>(true);

        if (window == null)
            throw new InvalidOperationException("WindowTitleBarDrag needs a parent BaseWindow.");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsDragging || eventData.button != PointerEventData.InputButton.Left || AppId == null)
        {
            return;
        }

        GameObject pressed = eventData.pointerPressRaycast.gameObject;

        if (pressed == null || pressed.GetComponentInParent<Selectable>(true) != null)
        {
            return;
        }

        pointerId = eventData.pointerId;
        IsDragging = true;
        DragStarted?.Invoke(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsDragging && eventData.pointerId == pointerId && eventData.button == PointerEventData.InputButton.Left)
        {
            DragMoved?.Invoke(this, eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsDragging || eventData.pointerId != pointerId)
            return;

        IsDragging = false;
        DragEnded?.Invoke(this, eventData);
    }

    public void CancelDrag()
    {
        if (!IsDragging)
            return;

        IsDragging = false;
        DragCancelled?.Invoke(this);
    }

    private void OnDisable()
    {
        CancelDrag();
    }
}