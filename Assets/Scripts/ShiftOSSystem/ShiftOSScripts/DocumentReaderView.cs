using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DocumentReaderView : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;

    private AppReadingState state;
    private string itemId;
    private bool restorePending = true;
    private bool restoring;
    private Vector2 contentSize;
    private Vector2 viewportSize;

    public void Bind(EvidenceDocumentViewData document, AppReadingState readingState)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        Bind(document.EvidenceId, document.DisplayName, document.BodyText, readingState);
    }

    public void Bind(string itemId, string title, string body, AppReadingState readingState)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("An item ID is required.", nameof(itemId));

        if (readingState == null)
            throw new ArgumentNullException(nameof(readingState));

        if (!string.Equals(itemId,
            readingState.SelectedItemId, StringComparison.Ordinal))
        {
            throw new ArgumentException( "Select the item before binding its reader.", nameof(readingState));
        }

        ValidateConfiguration();

        restorePending = true;
        state = readingState;
        this.itemId = itemId;
        titleLabel.text = title ?? string.Empty;
        bodyLabel.text = body ?? string.Empty;
    }

    public void Clear(string prompt = "Select a document")
    {
        ValidateConfiguration();

        restorePending = true;
        state = null;
        itemId = null;
        scrollRect.StopMovement();
        titleLabel.text = prompt;
        bodyLabel.text = string.Empty;
    }

    private void OnEnable()
    {
        restorePending = true;

        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(OnScrollChanged);
    }

    private void OnDisable()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.RemoveListener(OnScrollChanged);

        restorePending = true;
    }

    private void OnRectTransformDimensionsChange()
    {
        restorePending = true;
    }

    private void LateUpdate()
    {
        if (state == null || state.SelectedItemId != itemId)
            return;

        if (scrollRect.viewport.rect.width <= 0f || scrollRect.viewport.rect.height <= 0f)
        {
            return;
        }

        if (!restorePending && !LayoutChanged())
            return;

        restoring = true;

        try
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = ContentFits() ? 1f : state.ReaderScrollPosition;

            contentSize = scrollRect.content.rect.size;
            viewportSize = scrollRect.viewport.rect.size;
            restorePending = false;
        }
        finally
        {
            restoring = false;
        }
    }

    private void OnScrollChanged(Vector2 position)
    {
        if (restoring || restorePending || state == null || state.SelectedItemId != itemId)
        {
            return;
        }

        if (LayoutChanged())
        {
            restorePending = true;
            return;
        }

        if (ContentFits() || float.IsNaN(position.y) || float.IsInfinity(position.y))
        {
            return;
        }

        state.SetReaderScrollPosition(Mathf.Clamp01(position.y));
    }

    private bool LayoutChanged()
    {
        return contentSize != scrollRect.content.rect.size || viewportSize != scrollRect.viewport.rect.size;
    }

    private bool ContentFits()
    {
        return scrollRect.content.rect.height <= scrollRect.viewport.rect.height;
    }

    private void ValidateConfiguration()
    {
        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null || titleLabel == null || bodyLabel == null)
        {
            throw new InvalidOperationException("Assign the reader ScrollRect, content, viewport and labels.");
        }
    }
}