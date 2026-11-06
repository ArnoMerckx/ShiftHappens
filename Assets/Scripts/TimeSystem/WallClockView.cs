using TMPro;
using UnityEngine;

public sealed class WallClockView : MonoBehaviour
{
    [SerializeField] private WorkdaySessionDriver sessionDriver;
    [SerializeField] private TextMeshPro display;

    private int lastDisplayedMinute = -1;

    private void Start()
    {
        if (sessionDriver == null || sessionDriver.Session == null || display == null)
        {
            Debug.LogError("Assign the session driver and physical clock text.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        int minute = sessionDriver.Session.Now.MinuteOfDay;

        if (minute == lastDisplayedMinute)
            return;

        lastDisplayedMinute = minute;

        int hours = minute / 60;
        int minutes = minute % 60;

        display.text = $"{hours:00}:{minutes:00}";
    }
}