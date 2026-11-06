using TMPro;
using UnityEngine;

public sealed class DocumentInspectionView : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;

    public bool IsConfigured => title != null && body != null;

    public void Show(EvidenceDefinition document)
    {
        title.text = document.DisplayName;
        body.text = document.BodyText;
    }
}