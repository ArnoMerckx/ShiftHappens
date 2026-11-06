using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class ViewModeController : MonoBehaviour
{
    [SerializeField] private InputActionAsset actions;

    [SerializeField] private Camera viewCamera;
    [SerializeField] private PlayerLook playerLook;

    [SerializeField] private Transform computerPose;
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.25f;
    [SerializeField] private GameObject officeHud;
    [SerializeField] private GameObject desktopRoot;
    [SerializeField] private InputSystemUIInputModule uiInputModule;

    [SerializeField] private WorkdaySessionDriver sessionDriver;
    [SerializeField] private DocumentInspectionView documentView;

    private bool waitingForPointerRelease;

    private Vector3 officePosition;
    private Quaternion officeRotation;

    private Vector3 transitionStartPosition;
    private Quaternion transitionStartRotation;
    private Vector3 transitionEndPosition;
    private Quaternion transitionEndRotation;
    private float transitionElapsed;

    private readonly ViewModeState state = new ViewModeState();

    private InputAction look;
    private InputAction interact;
    private InputAction back;

    public ViewMode CurrentMode => state.CurrentMode;

    private bool CanUseViewInput =>initialized && isActiveAndEnabled && hasFocus && Application.isFocused && !recoveringFocus;

    public bool CanUseOfficeInput => CanUseViewInput && CurrentMode == ViewMode.Office;

    public Vector2 LookDelta => CanUseOfficeInput && UnityEngine.Time.frameCount > ignoreLookThroughFrame ? look.ReadValue<Vector2>() : Vector2.zero;

    public bool InteractPressedThisFrame => CanUseOfficeInput && interact.WasPressedThisFrame();

    private bool initialized;
    private bool hasFocus;
    private bool recoveringFocus;
    private int recoveryStartedFrame;
    private int ignoreLookThroughFrame;

    private void OnEnable()
    {
        if (!initialized)
            return;

        hasFocus = Application.isFocused;
        recoveringFocus = true;
        recoveryStartedFrame = UnityEngine.Time.frameCount;

        ApplyModeState();
    }

    private void OnDisable()
    {
        waitingForPointerRelease = false;

        if (uiInputModule != null)
            uiInputModule.enabled = false;

        if (actions != null)
            actions.Disable();

        if (officeHud != null)
            officeHud.SetActive(false);

        if (desktopRoot != null)
            desktopRoot.SetActive(false);

        if (documentView != null)
            documentView.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Awake()
    {
        if (actions == null || viewCamera == null || playerLook == null)
        {
            Debug.LogError("Assign the input actions, view camera and player look.", this);
            enabled = false;
            return;
        }

        if (actions == null || viewCamera == null || playerLook == null || officeHud == null || desktopRoot == null || uiInputModule == null || sessionDriver == null || documentView == null)
        {
            Debug.LogError("Assign the camera, look, input and UI references.", this);
            enabled = false;
            return;
        }

        if (uiInputModule.actionsAsset != actions)
        {
            Debug.LogError("The view controller and UI module must share input actions.", this);
            enabled = false;
            return;
        }

        look = actions.FindAction("Office/Look", true);
        interact = actions.FindAction("Office/Interact", true);
        back = actions.FindAction("View/Back", true);

        playerLook.Initialize(viewCamera.transform.localRotation);

        initialized = true;
    }

    private void Update()
    {
        if (CurrentMode == ViewMode.Transition)
        {
            AdvanceCameraTransition();
            return;
        }

        if (!hasFocus || !Application.isFocused)
            return;

        if (recoveringFocus)
        {
            if (UnityEngine.Time.frameCount > recoveryStartedFrame && RecoveryControlsReleased())
            {
                recoveringFocus = false;
                ignoreLookThroughFrame = UnityEngine.Time.frameCount + 1;
                ApplyModeState();
            }

            return;
        }

        if (CurrentMode == ViewMode.Computer || CurrentMode == ViewMode.InspectDocument)
        {
            if (!Application.isFocused)
                return;

            if (back.WasPressedThisFrame())
            {
                TryReturnToOffice();
                return;
            }

            if (waitingForPointerRelease && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
            {
                waitingForPointerRelease = false;
                uiInputModule.enabled = true;
            }

            return;
        }

        if (!CanUseOfficeInput)
            return;

        viewCamera.transform.localRotation = playerLook.Evaluate(LookDelta);
    }

    public bool TryEnterComputer()
    {
        if (!CanUseOfficeInput || computerPose == null)
            return false;

        if (computerPose == viewCamera.transform || computerPose.parent != viewCamera.transform.parent)
            return false;

        if (!state.TryEnterComputer())
            return false;

        officePosition = viewCamera.transform.localPosition;
        officeRotation = viewCamera.transform.localRotation;

        BeginCameraTransition(computerPose.localPosition, computerPose.localRotation);

        return true;
    }

    public bool TryReturnToOffice()
    {
        if (!CanUseViewInput)
            return false;

        bool returningFromComputer = CurrentMode == ViewMode.Computer;

        if (!state.TryReturnToOffice())
            return false;

        if (returningFromComputer)
            BeginCameraTransition(officePosition, officeRotation);
        else
            ApplyModeState();

        return true;
    }

    private void BeginCameraTransition(Vector3 destinationPosition, Quaternion destinationRotation)
    {
        transitionStartPosition = viewCamera.transform.localPosition;
        transitionStartRotation = viewCamera.transform.localRotation;

        transitionEndPosition = destinationPosition;
        transitionEndRotation = destinationRotation;
        transitionElapsed = 0f;

        ApplyModeState();
    }

    private void AdvanceCameraTransition()
    {
        transitionElapsed += UnityEngine.Time.unscaledDeltaTime;

        float progress = Mathf.Clamp01(
            transitionElapsed / Mathf.Max(0.01f, transitionDuration));

        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

        viewCamera.transform.localPosition = Vector3.Lerp(transitionStartPosition, transitionEndPosition, easedProgress);

        viewCamera.transform.localRotation = Quaternion.Slerp(transitionStartRotation, transitionEndRotation, easedProgress);

        if (progress >= 1f)
        {
            viewCamera.transform.localPosition = transitionEndPosition;
            viewCamera.transform.localRotation = transitionEndRotation;
            if (state.TryCompleteTransition())
                ApplyModeState();
        }
    }

    private void ApplyModeState()
    {
        uiInputModule.enabled = false;
        actions.Disable();

        bool office = CurrentMode == ViewMode.Office;
        bool computer = CurrentMode == ViewMode.Computer;
        bool transition = CurrentMode == ViewMode.Transition;
        bool inspecting = CurrentMode == ViewMode.InspectDocument;

        officeHud.SetActive(office);
        desktopRoot.SetActive(computer);
        documentView.gameObject.SetActive(inspecting);

        if (!hasFocus || !Application.isFocused || recoveringFocus)
        {
            waitingForPointerRelease = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (office)
        {
            look.Enable();
            interact.Enable();
        }

        if (!transition)
            back.Enable();

        waitingForPointerRelease = computer || inspecting;

        Cursor.lockState = office || transition ? CursorLockMode.Locked : CursorLockMode.None;

        Cursor.visible = !office && !transition;
    }

    public bool TryInspectDocument(EvidenceDefinition document)
    {
        if (!CanUseOfficeInput || document == null || !documentView.IsConfigured)
            return false;

        WorkdaySession session = sessionDriver.Session;

        if (session == null || !session.Workday.CanAcceptWork || !session.IsEvidenceAvailable(document.EvidenceId))
            return false;

        if (!state.TryInspectDocument())
            return false;

        documentView.Show(document);
        session.TryReadEvidence(document.EvidenceId);
        ApplyModeState();

        return true;
    }

    public void CloseDocument()
    {
        if (CurrentMode == ViewMode.InspectDocument)
            TryReturnToOffice();
    }

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;

        if (!initialized || !isActiveAndEnabled)
            return;

        recoveringFocus = true;
        recoveryStartedFrame = UnityEngine.Time.frameCount;

        ApplyModeState();
    }

    private bool RecoveryControlsReleased()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
            return false;

        Mouse mouse = Mouse.current;

        return mouse == null || (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed && !mouse.middleButton.isPressed);
    }
}