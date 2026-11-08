using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorkdaySummaryView : MonoBehaviour
{
    [SerializeField] private WorkdaySessionDriver sessionDriver;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;

    private WorkdaySession session;
    private bool refreshPending;

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
        session = sessionDriver.Session;

        if (session == null)
            throw new InvalidOperationException("The workday session must exist before its summary.");

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

        session.Workday.StateChanged -= RequestRefresh;
        session.Progress.Changed -= RequestRefresh;
    }

    private void ConnectAndRefresh()
    {
        session.Workday.StateChanged -= RequestRefresh;
        session.Workday.StateChanged += RequestRefresh;

        session.Progress.Changed -= RequestRefresh;
        session.Progress.Changed += RequestRefresh;

        RequestRefresh();
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

        WorkdayController workday = session.Workday;

        if (workday.State == WorkdayState.Working)
        {
            panel.SetActive(false);
            return;
        }

        if (workday.State == WorkdayState.Closing)
        {
            titleLabel.text = "Shift closing";

            bodyLabel.text = $"Credibility: {session.Progress.Credibility}\n\n" + $"Reports awaiting HR: {session.Submissions.PendingReviews.Count}\n" + "New work is closed. HR is finishing pending reviews.";
        }
        else
        {
            WorkdaySummary summary = workday.Summary;

            titleLabel.text = summary.FinalState == WorkdayState.Fired ? "You're fired" : "Shift completed";

            bodyLabel.text =
                $"{summary.Settings.DisplayName}\n" +
                $"Final credibility: {summary.Credibility}\n\n" +
                $"Approved: {Count(summary.Incidents, IncidentStatus.Approved)}\n" +
                $"Rejected: {Count(summary.Incidents, IncidentStatus.Rejected)}\n" +
                $"Overdue: {Count(summary.Incidents, IncidentStatus.Overdue)}\n" +
                $"Open: {Count(summary.Incidents, IncidentStatus.Open)}\n" +
                $"Pending: {Count(summary.Incidents, IncidentStatus.Pending)}";
        }

        panel.SetActive(true);
    }

    private static int Count(IReadOnlyList<IncidentSummary> incidents, IncidentStatus status)
    {
        int count = 0;

        foreach (IncidentSummary incident in incidents)
        {
            if (incident.Status == status)
                count++;
        }

        return count;
    }
}