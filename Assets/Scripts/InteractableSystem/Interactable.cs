using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [SerializeField] protected GameObject player;
    public bool canInteract;
    public virtual void Awake()
    {
        canInteract = true;
        gameObject.layer = 13;
        player = GameObject.Find("SeatAnchor");
    }
    public abstract void OnInteract();
    public abstract void OnFocus();
    public abstract void OnUnFocus();
}
