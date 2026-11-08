using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public sealed class BaseWindow : MonoBehaviour
{
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private Image appIcon;
    [SerializeField] private RectTransform contentRoot;

    [SerializeField] private GameObject resizeHandles;
    [SerializeField] private TMP_Text maximizeLabel;

    private RectTransform windowRect;

    public string AppId { get; private set; }
    public RectTransform ContentRoot => contentRoot;

    public event Action<string> FocusRequested;

    public event Action<string> MaximizeToggleRequested;

    public event Action<string> MinimizeRequested;
    public event Action<string> CloseRequested;

    public void Bind(AppDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        definition.Validate();

        if (titleLabel == null || appIcon == null || contentRoot == null || resizeHandles == null || maximizeLabel == null)
        {
            throw new InvalidOperationException("Assign the title, icon, content root, resize handles and maximize label.");
        }

        windowRect = (RectTransform)transform;
        AppId = definition.AppId;

        titleLabel.text = definition.DisplayName;
        appIcon.sprite = definition.Icon;
        appIcon.gameObject.SetActive(definition.Icon != null);
    }

    public void ApplyState(WindowState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (AppId == null)
            throw new InvalidOperationException("Bind the window first.");

        if (state.AppId != AppId)
        {
            throw new ArgumentException("Window state belongs to a different application.", nameof(state));
        }

        windowRect.anchorMin = new Vector2(0, 1);
        windowRect.anchorMax = new Vector2(0, 1);
        windowRect.pivot = new Vector2(0, 1);

        windowRect.sizeDelta = state.CurrentRect.size;
        windowRect.anchoredPosition = new Vector2(state.CurrentRect.x, -state.CurrentRect.y);

        resizeHandles.SetActive(!state.IsMaximized);
        maximizeLabel.text = state.IsMaximized ? "R" : "+";

        gameObject.SetActive(state.IsOpen && !state.IsMinimized);
    }

    public void RequestFocus()
    {
        if (isActiveAndEnabled && AppId != null)
            FocusRequested?.Invoke(AppId);
    }

    public void RequestToggleMaximize()
    {
        if (isActiveAndEnabled && AppId != null)
            MaximizeToggleRequested?.Invoke(AppId);
    }

    public void RequestMinimize()
    {
        if (isActiveAndEnabled && AppId != null)
            MinimizeRequested?.Invoke(AppId);
    }

    public void RequestClose()
    {
        if (isActiveAndEnabled && AppId != null)
            CloseRequested?.Invoke(AppId);
    }
}