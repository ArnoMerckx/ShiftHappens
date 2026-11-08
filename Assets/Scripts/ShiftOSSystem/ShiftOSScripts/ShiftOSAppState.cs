using System;

public sealed class ShiftOSAppState
{
    public AppReadingState InWatch { get; } = new AppReadingState();
    public AppReadingState Excess { get; } = new AppReadingState();
    public AppReadingState Swarms { get; } = new AppReadingState();
    public AppReadingState ShareLess { get; } = new AppReadingState();
    public ReportDraftState BlamePointDraft { get; } = new ReportDraftState();

    public AppReadingState BlamePointFormScroll { get; } = new AppReadingState();

    public AppReadingState BlamePointOverview { get; } = new AppReadingState();

    public BlamePointTab SelectedBlamePointTab { get; private set; } = BlamePointTab.CreateReport;

    public string TickTockMinutesText { get; set; } = "40";
    public string TickTockSecondsText { get; set; } = "00";

    public void SelectBlamePointTab(BlamePointTab tab)
    {
        if (tab != BlamePointTab.CreateReport && tab != BlamePointTab.ReportOverview)
        {
            throw new ArgumentOutOfRangeException(nameof(tab));
        }

        SelectedBlamePointTab = tab;
    }
}