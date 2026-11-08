using System;
using System.Collections.Generic;

public sealed class AppReadingState
{
    private readonly Dictionary<string, float> readerPositions = new Dictionary<string, float>(StringComparer.Ordinal);

    public string SelectedItemId { get; private set; }
    public float ListScrollPosition { get; private set; } = 1f;

    public float ReaderScrollPosition
    {
        get
        {
            if (SelectedItemId != null && readerPositions.TryGetValue(SelectedItemId, out float position))
            {
                return position;
            }

            return 1f;
        }
    }

    public void SelectItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("An item ID is required.", nameof(itemId));

        SelectedItemId = itemId;
    }

    public void ClearSelection()
    {
        SelectedItemId = null;
    }

    public void SetListScrollPosition(float position)
    {
        ValidatePosition(position);
        ListScrollPosition = position;
    }

    public void SetReaderScrollPosition(float position)
    {
        if (SelectedItemId == null)
            throw new InvalidOperationException("Select an item before saving its reading position.");

        ValidatePosition(position);
        readerPositions[SelectedItemId] = position;
    }

    private static void ValidatePosition(float position)
    {
        if (float.IsNaN(position) || float.IsInfinity(position) || position < 0f || position > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Normalized scroll position must be between zero and one.");
        }
    }
}