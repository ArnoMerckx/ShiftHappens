using UnityEngine;

public abstract class OfficeInteractable : Interactable
{
    public abstract string Prompt { get; }

    public override void OnFocus()
    {
    }

    public override void OnUnFocus()
    {
    }
}