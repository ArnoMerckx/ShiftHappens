using TMPro;
using UnityEngine;

public sealed class OfficeInteractionPrompt : MonoBehaviour
{
    [SerializeField] private PlayerInteract playerInteract;
    [SerializeField] private ViewModeController viewModes;
    [SerializeField] private TMP_Text label;

    private void Awake()
    {
        if (playerInteract == null || viewModes == null || label == null)
        {
            Debug.LogError("Assign player interaction, view modes and label.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        OfficeInteractable target = playerInteract.CurrentInteractable as OfficeInteractable;

        bool show = viewModes.CanUseOfficeInput && playerInteract.isActiveAndEnabled && target != null && target.isActiveAndEnabled && target.canInteract;

        label.text = show ? target.Prompt : string.Empty;
        label.enabled = show;
    }

    private void OnDisable()
    {
        if (label != null)
        {
            label.text = string.Empty;
            label.enabled = false;
        }
    }
}