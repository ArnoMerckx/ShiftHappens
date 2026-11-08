using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BlamePointAppView : ShiftOSAppView
{
    [SerializeField] private GameObject createReportRoot;
    [SerializeField] private GameObject overviewRoot;
    [SerializeField] private Button createReportButton;
    [SerializeField] private Button overviewButton;

    [SerializeField] private ReportFormView formView;

    [SerializeField] private ReportOverviewView overviewView;

    private ShiftOSAppState appState;

    public ReportFormPresenter Form { get; private set; }

    public override void Initialize(WorkdaySession session, ShiftOSAppState appState)
    {
        if (this.appState != null)
            throw new InvalidOperationException("Initialize this app view only once.");

        if (session == null)
            throw new ArgumentNullException(nameof(session));

        if (appState == null)
            throw new ArgumentNullException(nameof(appState));

        if (createReportRoot == null || overviewRoot == null || createReportButton == null || overviewButton == null || createReportRoot == overviewRoot || 
            createReportRoot == gameObject || overviewRoot == gameObject || createReportButton == overviewButton || formView == null || overviewView == null)
        {
            throw new InvalidOperationException("Assign two distinct page roots, tab buttons, form view and overview view.");
        }

        this.appState = appState;
        Form = new ReportFormPresenter(session, appState.BlamePointDraft);

        Form.ReconcileDraft();

        formView.Initialize(Form, session, appState.BlamePointFormScroll);

        overviewView.Initialize(session, appState.BlamePointOverview);

        ApplyTab();
    }

    private void OnEnable()
    {
        if (Form == null)
            return;

        Form.ReconcileDraft();
        ApplyTab();
    }

    public void ShowCreateReport()
    {
        SelectTab(BlamePointTab.CreateReport);
    }

    public void ShowOverview()
    {
        SelectTab(BlamePointTab.ReportOverview);
    }

    private void SelectTab(BlamePointTab tab)
    {
        if (!isActiveAndEnabled || appState == null)
            return;

        appState.SelectBlamePointTab(tab);
        ApplyTab();
    }

    private void ApplyTab()
    {
        bool creating = appState.SelectedBlamePointTab == BlamePointTab.CreateReport;

        createReportRoot.SetActive(creating);
        overviewRoot.SetActive(!creating);

        createReportButton.interactable = !creating;
        overviewButton.interactable = creating;
    }
}