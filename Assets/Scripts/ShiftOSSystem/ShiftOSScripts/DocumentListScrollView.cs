using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DocumentListScrollView : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;

    private AppReadingState state;
    private bool restorePending = true;
    private bool restoring;
    private Vector2 contentSize;
    private Vector2 viewportSize;

    public void Bind(AppReadingState readingState)
    {
        if (readingState == null)
            throw new ArgumentNullException(nameof(readingState));

        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
        {
            throw new InvalidOperationException("Assign the list ScrollRect, content and viewport.");
        }

        restorePending = true;
        state = readingState;
    }

    public void RequestRestore()
    {
        restorePending = true;
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
        if (state == null)
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
            scrollRect.verticalNormalizedPosition = ContentFits() ? 1f : state.ListScrollPosition;

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
        if (restoring || restorePending || state == null)
            return;

        if (LayoutChanged())
        {
            restorePending = true;
            return;
        }

        if (ContentFits() || float.IsNaN(position.y) || float.IsInfinity(position.y))
        {
            return;
        }

        state.SetListScrollPosition(Mathf.Clamp01(position.y));
    }

    private bool LayoutChanged()
    {
        return contentSize != scrollRect.content.rect.size || viewportSize != scrollRect.viewport.rect.size;
    }

    private bool ContentFits()
    {
        return scrollRect.content.rect.height <= scrollRect.viewport.rect.height;
    }
}