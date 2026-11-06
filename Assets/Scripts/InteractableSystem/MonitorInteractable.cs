using UnityEngine;

public sealed class MonitorInteractable : OfficeInteractable
{
    [SerializeField] private ViewModeController viewModes;

    public override string Prompt => "LMB - Use computer";

    public override void OnInteract()
    {
        if (viewModes == null)
        {
            Debug.LogError("Assign the view mode controller.", this);
            return;
        }

        viewModes.TryEnterComputer();
    }
}