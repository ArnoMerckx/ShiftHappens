using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public sealed class DocumentListRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text statusLabel;

    private Button button;
    private string itemId;
    private Action<string> clicked;

    public void Bind(EvidenceDocumentViewData document, bool isSelected, Action<string> onClicked)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        string status = document.IsRead ? "Read" : "Unread";

        if (isSelected)
            status += " · Selected";

        Bind(document.EvidenceId, document.DisplayName, status, onClicked);
    }

    public void Bind(string itemId, string title, string status, Action<string> onClicked)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("An item ID is required.", nameof(itemId));

        if (onClicked == null)
            throw new ArgumentNullException(nameof(onClicked));

        if (titleLabel == null || statusLabel == null)
            throw new InvalidOperationException("Assign the title and status labels.");

        button = GetComponent<Button>();
        this.itemId = itemId;
        clicked = onClicked;

        titleLabel.text = title ?? string.Empty;
        statusLabel.text = status ?? string.Empty;

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        if (isActiveAndEnabled && itemId != null)
            clicked?.Invoke(itemId);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);

        clicked = null;
    }
}