using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ReportDraftStateTests
{
    private static ReportDraftState Populated()
    {
        var draft = new ReportDraftState();
        draft.SelectIncident("D0I3");
        draft.SelectSuspect("steve");
        draft.SetEvidenceSelected("E_SUPPLIES_RECEIPT", true);
        draft.SetEvidenceSelected("E_SCHEDULE", true);
        return draft;
    }

    private static void AssertPopulated(ReportDraftState draft)
    {
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo("D0I3"));
        Assert.That(draft.SelectedSuspectId, Is.EqualTo("steve"));
        Assert.That(draft.EvidenceIds, Is.EqualTo(new[]
            { "E_SUPPLIES_RECEIPT", "E_SCHEDULE" }));
    }

    private static void AssertEmpty(ReportDraftState draft)
    {
        Assert.That(draft.SelectedIncidentInstanceId, Is.Null);
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.EvidenceIds, Is.Empty);
    }

    private static List<List<string>> Eligible()
    {
        return new List<List<string>>
        {
            new List<string> { "D0I3", "D0I2" },
            new List<string> { "steve", "karen" },
            new List<string> { "E_SUPPLIES_RECEIPT", "E_SCHEDULE" }
        };
    }

    private static void Reconcile(ReportDraftState draft, List<List<string>> ids)
    {
        draft.Reconcile(ids[0], ids[1], ids[2]);
    }

    [Test]
    public void NewDraft_HasNoAutomaticSelections()
    {
        AssertEmpty(new ReportDraftState());
    }

    [Test]
    public void SelectingSameIncident_PreservesAccusation()
    {
        var draft = Populated();
        draft.SelectIncident("D0I3");
        AssertPopulated(draft);
    }

    [TestCase("D0I2")]
    [TestCase("d0i3")]
    public void SelectingDifferentIncident_ClearsSuspectAndEvidence(string id)
    {
        var draft = Populated();
        draft.SelectIncident(id);
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo(id));
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.EvidenceIds, Is.Empty);
    }

    [Test]
    public void SwitchingBack_DoesNotResurrectPreviousAccusation()
    {
        var draft = Populated();
        draft.SelectIncident("D0I2");
        draft.SelectIncident("D0I3");
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.EvidenceIds, Is.Empty);
    }

    [Test]
    public void ChangingSuspect_PreservesIncidentAndAttachments()
    {
        var draft = Populated();
        draft.SelectSuspect("karen");
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo("D0I3"));
        Assert.That(draft.SelectedSuspectId, Is.EqualTo("karen"));
        Assert.That(draft.EvidenceIds.Count, Is.EqualTo(2));
    }

    [Test]
    public void ClearSuspect_IsIdempotentAndPreservesAttachments()
    {
        var draft = Populated();
        draft.ClearSuspect();
        draft.ClearSuspect();
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo("D0I3"));
        Assert.That(draft.EvidenceIds.Count, Is.EqualTo(2));
    }

    [Test]
    public void SelectingExistingEvidence_IsIdempotentAndPreservesOrder()
    {
        var draft = Populated();
        draft.SetEvidenceSelected("E_SUPPLIES_RECEIPT", true);
        AssertPopulated(draft);
    }

    [Test]
    public void RemovingAbsentEvidence_IsHarmless()
    {
        var draft = Populated();
        draft.SetEvidenceSelected("E_OFFICE_SOCIAL", false);
        AssertPopulated(draft);
    }

    [Test]
    public void RemovingAndReselectingEvidence_AppendsItAfterRemainingItems()
    {
        var draft = Populated();
        draft.SetEvidenceSelected("E_SUPPLIES_RECEIPT", false);
        draft.SetEvidenceSelected("E_SUPPLIES_RECEIPT", true);
        Assert.That(draft.EvidenceIds, Is.EqualTo(new[]
            { "E_SCHEDULE", "E_SUPPLIES_RECEIPT" }));
    }

    [Test]
    public void AttachmentIdentity_IsOrdinal()
    {
        var draft = Populated();
        draft.SetEvidenceSelected("e_schedule", true);
        draft.SetEvidenceSelected("E_SCHEDULE", false);
        Assert.That(draft.EvidenceIds, Is.EqualTo(new[]
            { "E_SUPPLIES_RECEIPT", "e_schedule" }));
    }

    [Test]
    public void Clear_CanBeRepeatedAndDraftCanBeReused()
    {
        var draft = Populated();
        draft.Clear();
        draft.Clear();
        AssertEmpty(draft);
        draft.SelectIncident("D0I1");
        draft.SelectSuspect("bob");
        Assert.That(draft.SelectedSuspectId, Is.EqualTo("bob"));
    }

    [Test]
    public void AttachmentOutput_CannotBeMutatedThroughCollectionInterfaces()
    {
        var draft = Populated();
        var list = (IList<string>)draft.EvidenceIds;
        Assert.Throws<NotSupportedException>(() => list.Add("OTHER"));
        Assert.Throws<NotSupportedException>(() => list[0] = "OTHER");
        Assert.Throws<NotSupportedException>(() => list.Clear());
        AssertPopulated(draft);
    }

    [Test]
    public void SubmittedReportSnapshot_IsIndependentOfFurtherDraftEdits()
    {
        var draft = Populated();
        var report = new IncidentReport(draft.SelectedIncidentInstanceId,
            draft.SelectedSuspectId, new List<string>(draft.EvidenceIds), 0, 850);
        draft.Clear();
        Assert.That(report.IncidentInstanceId, Is.EqualTo("D0I3"));
        Assert.That(report.SuspectId, Is.EqualTo("steve"));
        Assert.That(report.EvidenceIds, Is.EqualTo(new[]
            { "E_SUPPLIES_RECEIPT", "E_SCHEDULE" }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void InvalidIncidentId_DoesNotEraseDraft(string invalid)
    {
        var draft = Populated();
        Assert.Throws<ArgumentException>(() => draft.SelectIncident(invalid));
        AssertPopulated(draft);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void InvalidSuspectId_DoesNotChangeDraft(string invalid)
    {
        var draft = Populated();
        Assert.Throws<ArgumentException>(() => draft.SelectSuspect(invalid));
        AssertPopulated(draft);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void InvalidEvidenceId_IsRejectedForBothSelectionDirections(string invalid)
    {
        var draft = Populated();
        Assert.Throws<ArgumentException>(() => draft.SetEvidenceSelected(invalid, true));
        Assert.Throws<ArgumentException>(() => draft.SetEvidenceSelected(invalid, false));
        AssertPopulated(draft);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void AccusationCommands_RequireAnIncident(int command)
    {
        var draft = new ReportDraftState();
        Assert.Throws<InvalidOperationException>(() =>
        {
            if (command == 0) draft.SelectSuspect("steve");
            else draft.SetEvidenceSelected("E_SCHEDULE", command == 1);
        });
        AssertEmpty(draft);
    }

    [Test]
    public void Reconcile_AllSelectionsEligible_PreservesDraft()
    {
        var draft = Populated();
        Reconcile(draft, Eligible());
        AssertPopulated(draft);
    }

    [Test]
    public void Reconcile_IncidentLeavesOpenChoices_ClearsEntireAccusation()
    {
        var draft = Populated();
        var ids = Eligible();
        ids[0].Remove("D0I3");
        Reconcile(draft, ids);
        AssertEmpty(draft);
    }

    [Test]
    public void Reconcile_UnknownSuspect_ClearsOnlySuspect()
    {
        var draft = Populated();
        var ids = Eligible();
        ids[1].Remove("steve");
        Reconcile(draft, ids);
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo("D0I3"));
        Assert.That(draft.EvidenceIds.Count, Is.EqualTo(2));
    }

    [Test]
    public void Reconcile_PrunesUnavailableAttachmentsWithoutReorderingSurvivors()
    {
        var draft = Populated();
        draft.SetEvidenceSelected("E_OFFICE_SOCIAL", true);
        var ids = Eligible();
        ids[2] = new List<string> { "E_OFFICE_SOCIAL", "E_SUPPLIES_RECEIPT" };
        Reconcile(draft, ids);
        Assert.That(draft.EvidenceIds, Is.EqualTo(new[]
            { "E_SUPPLIES_RECEIPT", "E_OFFICE_SOCIAL" }));
        Assert.That(draft.SelectedSuspectId, Is.EqualTo("steve"));
    }

    [Test]
    public void Reconcile_NoAttachableEvidence_LeavesSuspectAndIncident()
    {
        var draft = Populated();
        var ids = Eligible();
        ids[2].Clear();
        Reconcile(draft, ids);
        Assert.That(draft.EvidenceIds, Is.Empty);
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo("D0I3"));
        Assert.That(draft.SelectedSuspectId, Is.EqualTo("steve"));
    }

    [Test]
    public void Reconcile_DoesNotAutomaticallyChooseAnyOptions()
    {
        var draft = new ReportDraftState();
        Reconcile(draft, Eligible());
        AssertEmpty(draft);
        draft.SelectIncident("D0I3");
        Reconcile(draft, Eligible());
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.EvidenceIds, Is.Empty);
    }

    [Test]
    public void Reconcile_DuplicateOptionsAreHarmless()
    {
        var draft = Populated();
        var ids = Eligible();
        foreach (List<string> options in ids) options.Add(options[0]);
        Reconcile(draft, ids);
        AssertPopulated(draft);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Reconcile_MembershipUsesOrdinalIds(int option)
    {
        var draft = Populated();
        var ids = Eligible();
        ids[option][0] = ids[option][0].ToLowerInvariant();
        if (option == 1) ids[1][0] = "STEVE";
        Reconcile(draft, ids);
        if (option == 0) AssertEmpty(draft);
        else if (option == 1) Assert.That(draft.SelectedSuspectId, Is.Null);
        else Assert.That(draft.EvidenceIds, Is.EqualTo(new[] { "E_SCHEDULE" }));
    }

    [Test]
    public void Reconcile_DoesNotRetainCallerOwnedOptionLists()
    {
        var draft = Populated();
        var ids = Eligible();
        Reconcile(draft, ids);
        foreach (List<string> options in ids) options.Clear();
        AssertPopulated(draft);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Reconcile_NullInputFailsBeforeAnyDraftMutation(int option)
    {
        var draft = Populated();
        var ids = new List<List<string>>
            { new List<string>(), new List<string>(), new List<string>() };
        ids[option] = null;
        Assert.Throws<ArgumentNullException>(() => Reconcile(draft, ids));
        AssertPopulated(draft);
    }

    [TestCase(0, null)]
    [TestCase(0, "")]
    [TestCase(0, "   ")]
    [TestCase(1, null)]
    [TestCase(1, "")]
    [TestCase(1, "   ")]
    [TestCase(2, null)]
    [TestCase(2, "")]
    [TestCase(2, "   ")]
    public void Reconcile_InvalidOptionFailsBeforeAnyDraftMutation(int option, string invalid)
    {
        var draft = Populated();
        var ids = new List<List<string>>
            { new List<string>(), new List<string>(), new List<string>() };
        ids[option].Add(invalid);
        Assert.Throws<ArgumentException>(() => Reconcile(draft, ids));
        AssertPopulated(draft);
    }

    [Test]
    public void Reconcile_EmptyDraftStillValidatesAllInputs()
    {
        var draft = new ReportDraftState();
        var ids = Eligible();
        ids[2] = null;
        Assert.Throws<ArgumentNullException>(() => Reconcile(draft, ids));
        ids[2] = new List<string> { " " };
        Assert.Throws<ArgumentException>(() => Reconcile(draft, ids));
        AssertEmpty(draft);
    }

    [Test]
    public void PublicSelectionsHaveNoPublicSetters_AndAttachmentsHaveNoSetter()
    {
        foreach (string name in new[] { "SelectedIncidentInstanceId", "SelectedSuspectId" })
            Assert.That(typeof(ReportDraftState).GetProperty(name).GetSetMethod(), Is.Null);
        Assert.That(typeof(ReportDraftState).GetProperty("EvidenceIds").GetSetMethod(true), Is.Null);
    }
}
