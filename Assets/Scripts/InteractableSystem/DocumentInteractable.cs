using UnityEngine;

public sealed class DocumentInteractable : OfficeInteractable
{
    [SerializeField] private EvidenceDefinition document;
    [SerializeField] private ViewModeController viewModes;

    public override string Prompt => "LMB - Read document";

    public override void OnInteract()
    {
        if (document == null || viewModes == null)
        {
            Debug.LogError("Assign the document and view modes.", this);
            return;
        }

        viewModes.TryInspectDocument(document);
    }
}