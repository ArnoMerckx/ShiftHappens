using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CredibilityView : MonoBehaviour
{
    [SerializeField] private WorkdaySessionDriver sessionDriver;
    [SerializeField] private TMP_Text credibilityLabel;

    private WorkdayProgress progress;

    private void Awake()
    {
        if (sessionDriver == null || credibilityLabel == null)
        {
            throw new InvalidOperationException("Assign the session driver and credibility label.");
        }
    }

    private void Start()
    {
        if (sessionDriver.Session == null)
            throw new InvalidOperationException("The workday session must exist before its display.");

        progress = sessionDriver.Session.Progress;
        ConnectAndRefresh();
    }

    private void OnEnable()
    {
        if (progress != null)
            ConnectAndRefresh();
    }

    private void OnDisable()
    {
        if (progress != null)
            progress.Changed -= Refresh;
    }

    private void ConnectAndRefresh()
    {
        progress.Changed -= Refresh;
        progress.Changed += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        credibilityLabel.text = $"Credibility: {progress.Credibility}";
    }
}