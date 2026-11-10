using UnityEngine;

public class NewtonCradleInteractable : OfficeInteractable
{
    public Animator cradleAnimator;

    public bool isActive;
    public override string Prompt => "LMB - Use Newtons's Cradle";

    public override void Awake()
    {
        base.Awake();
        isActive = false;
    }
    public override void OnInteract()
    {
        if (isActive)
        {
            cradleAnimator.SetBool("isActive", false);
            isActive = false;
        }
        else
        {
            cradleAnimator.SetBool("isActive", true);
            isActive = true;
        }
    }

}
