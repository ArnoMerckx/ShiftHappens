using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class WindowPointerFocus : MonoBehaviour, IPointerDownHandler
{
    private BaseWindow window;

    private void Awake()
    {
        window = GetComponentInParent<BaseWindow>(true);

        if (window == null)
            throw new InvalidOperationException("WindowPointerFocus needs a parent BaseWindow.");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            window.RequestFocus();
    }
}