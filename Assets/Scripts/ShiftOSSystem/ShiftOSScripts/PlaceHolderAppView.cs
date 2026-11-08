using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlaceholderAppView : ShiftOSAppView
{
    [SerializeField] private TMP_Text messageLabel;

    [SerializeField, TextArea(2, 4)]
    private string message = "This app is not available in this prototype.";

    public override void Initialize(WorkdaySession session, ShiftOSAppState appState)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        if (appState == null)
            throw new ArgumentNullException(nameof(appState));

        if (messageLabel == null || string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidOperationException("Assign a label and placeholder message.");
        }

        messageLabel.text = message;
    }
}