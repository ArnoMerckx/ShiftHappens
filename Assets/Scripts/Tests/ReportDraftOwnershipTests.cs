using NUnit.Framework;

public class ReportDraftOwnershipTests
{
    [Test]
    public void RepeatedAccess_ReturnsTheSameInitiallyEmptyDraft()
    {
        var state = new ShiftOSAppState();
        Assert.That(state.BlamePointDraft, Is.Not.Null);
        Assert.That(state.BlamePointDraft, Is.SameAs(state.BlamePointDraft));
        Assert.That(state.BlamePointDraft.SelectedIncidentInstanceId, Is.Null);
        Assert.That(state.BlamePointDraft.SelectedSuspectId, Is.Null);
        Assert.That(state.BlamePointDraft.EvidenceIds, Is.Empty);
        state.BlamePointDraft.SelectIncident("D0I3");
        Assert.That(state.BlamePointDraft.SelectedIncidentInstanceId, Is.EqualTo("D0I3"));
    }

    [Test]
    public void DifferentStores_HaveIndependentDrafts()
    {
        var first = new ShiftOSAppState();
        var second = new ShiftOSAppState();
        first.BlamePointDraft.SelectIncident("D0I3");
        first.BlamePointDraft.SelectSuspect("steve");
        first.BlamePointDraft.SetEvidenceSelected("E_SUPPLIES_RECEIPT", true);
        Assert.That(second.BlamePointDraft, Is.Not.SameAs(first.BlamePointDraft));
        Assert.That(second.BlamePointDraft.SelectedIncidentInstanceId, Is.Null);
        Assert.That(second.BlamePointDraft.EvidenceIds, Is.Empty);
    }

    [Test]
    public void DraftAndReadingSelections_DoNotChangeEachOther()
    {
        var state = new ShiftOSAppState();
        state.BlamePointDraft.SelectIncident("D0I3");
        state.BlamePointDraft.SelectSuspect("steve");
        state.BlamePointDraft.SetEvidenceSelected("E_SUPPLIES_RECEIPT", true);
        foreach (AppReadingState reading in new[]
            { state.InWatch, state.Excess, state.Swarms, state.ShareLess })
        {
            Assert.That(reading.SelectedItemId, Is.Null);
            reading.SelectItem("OTHER");
            reading.SetReaderScrollPosition(0.25f);
        }
        Assert.That(state.BlamePointDraft.SelectedIncidentInstanceId, Is.EqualTo("D0I3"));
        Assert.That(state.BlamePointDraft.SelectedSuspectId, Is.EqualTo("steve"));
        Assert.That(state.BlamePointDraft.EvidenceIds, Is.EqualTo(new[] { "E_SUPPLIES_RECEIPT" }));
        state.BlamePointDraft.Clear();
        Assert.That(state.InWatch.SelectedItemId, Is.EqualTo("OTHER"));
    }

    [Test]
    public void PersistentOwner_DoesNotExposeDraftReplacement()
    {
        Assert.That(typeof(ShiftOSAppState).GetProperty("BlamePointDraft").GetSetMethod(true), Is.Null);
    }
}
