using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Parameters")]
    [SerializeField] private Vector3 _interactionRayPoint = new Vector3(0.5f, 0.5f, 0f);
    [SerializeField] private float _interactionDistance = 4;
    [SerializeField] private LayerMask _interactionLayer = 1 << 13;
    [SerializeField] private Interactable currentInteractable;

    [SerializeField] private ViewModeController viewModes;

    [Header("Default Parameters")]
    private Camera _playerCamera;

    public Interactable CurrentInteractable => currentInteractable;

    private void OnDisable()
    {
        SetCurrentInteractable(null);
    }

    private void Awake()
    {
        _playerCamera = GetComponentInChildren<Camera>();

        if (_playerCamera == null || viewModes == null)
        {
            Debug.LogError("Assign view modes and provide a child camera.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        OnPlayerInteract();
    }


    #region Interacting
    private void OnPlayerInteract()
    {
        if (!viewModes.CanUseOfficeInput)
        {
            SetCurrentInteractable(null);
            return;
        }

        Interactable target = null;
        Ray ray = _playerCamera.ViewportPointToRay(_interactionRayPoint);

        if (Physics.Raycast(ray, out RaycastHit hit, _interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            target = hit.collider.GetComponentInParent<Interactable>();

            if (target != null && (!target.isActiveAndEnabled || !target.canInteract || (_interactionLayer.value & (1 << target.gameObject.layer)) == 0))
            {
                target = null;
            }
        }

        SetCurrentInteractable(target);
        HandleInteraction();

        if (!viewModes.CanUseOfficeInput)
            SetCurrentInteractable(null);
    }

    private void SetCurrentInteractable(Interactable target)
    {
        if (currentInteractable == target) return;
        if (currentInteractable != null) currentInteractable.OnUnFocus();
        currentInteractable = target;
        if (currentInteractable != null) currentInteractable.OnFocus();
    }

    private void HandleInteraction()
    {
        if (viewModes.InteractPressedThisFrame && currentInteractable != null && currentInteractable.isActiveAndEnabled && currentInteractable.canInteract)
        {
            currentInteractable.OnInteract();
        }
    }
    #endregion
}
