using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public sealed class TaskbarButtonView : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image icon;
    [SerializeField] private GameObject activeIndicator;

    private Button button;
    private string appId;
    private Action<string> clicked;

    public void Bind(AppDefinition definition, Action<string> onClicked)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (onClicked == null)
            throw new ArgumentNullException(nameof(onClicked));

        definition.Validate();

        if (label == null || icon == null || activeIndicator == null)
            throw new InvalidOperationException("Assign the taskbar label, icon and active indicator.");

        button = GetComponent<Button>();
        appId = definition.AppId;
        clicked = onClicked;

        label.text = definition.DisplayName;

        label.color = Color.white;

        bool hasIcon = definition.Icon != null;
        icon.sprite = definition.Icon;
        icon.gameObject.SetActive(hasIcon);

        label.rectTransform.offsetMin = new Vector2(hasIcon ? 40f : 8f, 0f);

        label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        activeIndicator.SetActive(false);

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        if (isActiveAndEnabled && appId != null)
            clicked?.Invoke(appId);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);

        clicked = null;
    }

    public void ApplyRunningState(WindowState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (appId == null)
            throw new InvalidOperationException("Bind the button first.");

        if (state.AppId != appId)
            throw new ArgumentException("Window state belongs to another application.", nameof(state));

        activeIndicator.SetActive(
            state.IsOpen && !state.IsMinimized && state.IsFocused);

        label.color = state.IsMinimized ? new Color(0.7f, 0.7f, 0.7f, 1f) : Color.white;
    }
}