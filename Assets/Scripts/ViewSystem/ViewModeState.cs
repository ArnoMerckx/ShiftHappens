public sealed class ViewModeState
{
    public ViewMode CurrentMode { get; private set; } = ViewMode.Office;
    public ViewMode? PendingMode { get; private set; }

    public bool TryEnterComputer()
    {
        if (CurrentMode != ViewMode.Office)
            return false;

        BeginTransition(ViewMode.Computer);
        return true;
    }

    public bool TryInspectDocument()
    {
        if (CurrentMode != ViewMode.Office)
            return false;

        CurrentMode = ViewMode.InspectDocument;
        return true;
    }

    public bool TryReturnToOffice()
    {
        if (CurrentMode == ViewMode.Computer)
        {
            BeginTransition(ViewMode.Office);
            return true;
        }

        if (CurrentMode == ViewMode.InspectDocument)
        {
            CurrentMode = ViewMode.Office;
            return true;
        }

        return false;
    }

    public bool TryCompleteTransition()
    {
        if (CurrentMode != ViewMode.Transition)
            return false;

        CurrentMode = PendingMode.Value;
        PendingMode = null;
        return true;
    }

    private void BeginTransition(ViewMode destination)
    {
        PendingMode = destination;
        CurrentMode = ViewMode.Transition;
    }
}