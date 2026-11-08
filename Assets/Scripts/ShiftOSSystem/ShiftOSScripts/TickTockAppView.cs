using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TickTockAppView : ShiftOSAppView
{
    [SerializeField] private TMP_InputField minutesInput;
    [SerializeField] private TMP_InputField secondsInput;
    [SerializeField] private TMP_Text remainingLabel;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private TMP_Text pauseResumeLabel;

    [SerializeField] private Button startButton;
    [SerializeField] private Button pauseResumeButton;
    [SerializeField] private Button resetButton;

    private WorkdaySession session;
    private ShiftOSAppState appState;
    private bool refreshPending;

    public override void Initialize(
        WorkdaySession session,
        ShiftOSAppState appState)
    {
        if (this.session != null)
            throw new InvalidOperationException("Initialize TickTock only once.");

        if (session == null)
            throw new ArgumentNullException(nameof(session));

        if (appState == null)
            throw new ArgumentNullException(nameof(appState));

        if (minutesInput == null || secondsInput == null || remainingLabel == null || feedbackLabel == null || pauseResumeLabel == null || startButton == null || pauseResumeButton == null || resetButton == null)
        {
            throw new InvalidOperationException("Assign TickTock inputs, labels and buttons.");
        }

        this.session = session;
        this.appState = appState;

        minutesInput.SetTextWithoutNotify(appState.TickTockMinutesText);
        secondsInput.SetTextWithoutNotify(appState.TickTockSecondsText);
        feedbackLabel.text = string.Empty;

        if (isActiveAndEnabled)
            ConnectAndRefresh();
    }

    private void OnEnable()
    {
        if (session != null)
            ConnectAndRefresh();
    }

    private void OnDisable()
    {
        if (session == null)
            return;

        SaveInput();
        session.TimeAdvanced -= RequestRefresh;
    }

    private void ConnectAndRefresh()
    {
        session.TimeAdvanced -= RequestRefresh;
        session.TimeAdvanced += RequestRefresh;
        RequestRefresh();
    }

    private void SaveInput()
    {
        appState.TickTockMinutesText = minutesInput.text;
        appState.TickTockSecondsText = secondsInput.text;
    }

    private void RequestRefresh()
    {
        refreshPending = true;
    }

    private void LateUpdate()
    {
        if (session == null || !refreshPending)
            return;

        refreshPending = false;

        DurationTimerService timer = session.Timer;
        bool canWork = session.Workday.CanAcceptWork;
        bool running = timer.State == DurationTimerState.Running;
        bool paused = timer.State == DurationTimerState.Paused;
        bool canConfigure = canWork && !running && !paused;

        string remaining = TickTockDuration.FormatRemaining(timer.RemainingMilliseconds);

        if (remainingLabel.text != remaining)
            remainingLabel.text = remaining;

        minutesInput.interactable = canConfigure;
        secondsInput.interactable = canConfigure;
        startButton.interactable = canConfigure;
        pauseResumeButton.interactable = canWork && (running || paused);
        resetButton.interactable = canWork;
        pauseResumeLabel.text = paused ? "Resume" : "Pause";
    }

    public void StartTimer()
    {
        if (!isActiveAndEnabled || session == null)
            return;

        SaveInput();

        if (!TickTockDuration.TryParseDuration(minutesInput.text, secondsInput.text, out int seconds, out string error))
        {
            feedbackLabel.text = error;
            return;
        }

        feedbackLabel.text = session.Timer.TryStart(seconds) ? "Timer started." : "Cannot start now. Reset an active timer before changing it.";

        RequestRefresh();
    }

    public void PauseOrResumeTimer()
    {
        if (!isActiveAndEnabled || session == null)
            return;

        bool resume = session.Timer.State == DurationTimerState.Paused;

        bool changed = resume ? session.Timer.TryResume() : session.Timer.TryPause();

        feedbackLabel.text = changed ? (resume ? "Timer resumed." : "Timer paused.") : "The timer cannot change state now.";

        RequestRefresh();
    }

    public void ResetTimer()
    {
        if (!isActiveAndEnabled || session == null)
            return;

        feedbackLabel.text = session.Timer.TryReset() ? "Timer reset." : "The timer cannot reset now.";

        RequestRefresh();
    }
}