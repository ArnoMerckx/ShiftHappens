using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class OpeningScreenView : MonoBehaviour
{
    [SerializeField] private WorkdaySessionDriver sessionDriver;
    [SerializeField] private ViewModeController viewModes;
    [SerializeField] private GameObject panel;

    private void Awake()
    {
        if (sessionDriver == null || sessionDriver.Session == null || viewModes == null || panel == null || panel == gameObject || !panel.transform.IsChildOf(transform))
        {
            throw new InvalidOperationException("Assign the session driver, view modes and a child opening panel.");
        }

        panel.SetActive(!sessionDriver.HasBegunShift);
    }

    private void Start()
    {
        if (!sessionDriver.HasBegunShift)
            viewModes.SetSessionMenuOpen(true);
    }

    public void BeginShift()
    {
        if (!isActiveAndEnabled || !panel.activeInHierarchy || !viewModes.CanUseSessionMenuInput)
        {
            return;
        }

        if (!sessionDriver.TryBeginShift())
            return;

        panel.SetActive(false);
        viewModes.SetSessionMenuOpen(false);
    }

    public void Quit()
    {
        if (!isActiveAndEnabled || !panel.activeInHierarchy || sessionDriver.HasBegunShift || !viewModes.CanUseSessionMenuInput)
        {
            return;
        }

        if (Application.isEditor)
        {
            Debug.Log("Quit requested. The standalone will close.", this);
            return;
        }

        Application.Quit();
    }
}