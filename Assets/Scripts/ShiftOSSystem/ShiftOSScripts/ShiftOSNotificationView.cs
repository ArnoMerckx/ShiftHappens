using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShiftOSNotificationView : MonoBehaviour
{
    [SerializeField] private WorkdaySessionDriver sessionDriver;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField, Min(1f)] private float displaySeconds = 6f;

    [SerializeField] private AudioSource notificationAudio;
    [SerializeField] private AudioClip timerExpiredClip;
    [SerializeField] private AudioClip hrDecisionClip;

    private ShiftOSNotificationState notifications;
    private bool checkPending;
    private bool showing;
    private float remainingSeconds;

    private void Awake()
    {
        if (sessionDriver == null || panel == null || titleLabel == null || bodyLabel == null || panel == gameObject || !panel.transform.IsChildOf(transform))
        {
            throw new InvalidOperationException("Assign the session driver, child panel and labels.");
        }

        panel.SetActive(false);
    }

    private void Start()
    {
        if (sessionDriver.Session == null)
            throw new InvalidOperationException("The workday session must exist before notifications.");

        notifications = sessionDriver.Session.Notifications;
        Connect();
    }

    private void OnEnable()
    {
        if (notifications == null)
            return;

        panel.SetActive(showing);
        Connect();
    }

    private void OnDisable()
    {
        if (notifications != null)
            notifications.Published -= RequestNext;

        if (panel != null)
            panel.SetActive(false);
    }

    private void Connect()
    {
        notifications.Published -= RequestNext;
        notifications.Published += RequestNext;
        RequestNext();
    }

    private void RequestNext()
    {
        checkPending = true;
    }

    private void Update()
    {
        if (notifications == null || !Application.isFocused)
            return;

        if (showing)
        {
            remainingSeconds -= UnityEngine.Time.unscaledDeltaTime;

            if (remainingSeconds > 0f)
                return;

            showing = false;
            panel.SetActive(false);
            checkPending = true;
        }

        if (!checkPending)
            return;

        checkPending = false;

        if (!notifications.TryTakeNext(out ShiftOSNotification message))
            return;

        titleLabel.text = message.Title;
        bodyLabel.text = message.Body;
        remainingSeconds = displaySeconds;
        showing = true;
        panel.SetActive(true);

        PlayNotificationSound(message);
    }

    private void PlayNotificationSound(ShiftOSNotification message)
    {
        if (notificationAudio == null)
            return;

        AudioClip clip = message.Kind switch
        {
            ShiftOSNotificationKind.TimerExpired => timerExpiredClip,
            ShiftOSNotificationKind.HrDecision => hrDecisionClip,
            _ => null
        };

        if (clip != null)
            notificationAudio.PlayOneShot(clip);
    }
}