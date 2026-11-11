using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class ReportFormPresenterTests : EvidencePresentationTestBase
{
    private WorkdaySessionTestContext context;
    private WorkdaySession session;
    private ReportDraftState draft;
    private ReportFormPresenter presenter;

    [SetUp]
    public void SetUp()
    {
        UseDay();
    }

    private void UseDay(DayData day = null, CredibilityRules rules = null)
    {
        context = new WorkdaySessionTestContext(day, rules);
        session = context.Session;
        draft = new ReportDraftState();
        presenter = new ReportFormPresenter(session, draft);
    }

    private void AdvanceTo(int minute, int second = 0, int millisecond = 0)
    {
        var target = new GameTime(0, minute, second, millisecond);
        long delta = target.TotalMilliseconds - session.Now.TotalMilliseconds;
        Assert.That(delta, Is.GreaterThanOrEqualTo(0));
        // A tiny sub-millisecond allowance avoids binary floating-point rounding
        // below the requested integer millisecond. No test makes enough advances
        // for these allowances to accumulate to a whole millisecond.
        session.AdvanceRealSeconds((delta + 0.001) /
            (GameTime.MillisecondsPerSecond * session.Settings.GameSecondsPerRealSecond));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(target.TotalMilliseconds));
    }

    private void OpenDraft()
    {
        AdvanceTo(675);
        Assert.That(presenter.TrySelectIncident("D0I2"), Is.True);
        Assert.That(presenter.TrySelectSuspect("dave"), Is.True);
        Assert.That(session.TryDiscoverEvidence("E_DAVE_PRINTER"), Is.True);
        Assert.That(presenter.TrySetEvidenceSelected("E_DAVE_PRINTER", true), Is.True);
    }

    private void AssertOpenDraft()
    {
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo("D0I2"));
        Assert.That(draft.SelectedSuspectId, Is.EqualTo("dave"));
        Assert.That(draft.EvidenceIds, Is.EqualTo(new[] { "E_DAVE_PRINTER" }));
    }

    private void AssertRejectedWithoutMutation()
    {
        string incident = draft.SelectedIncidentInstanceId;
        string suspect = draft.SelectedSuspectId;
        var evidence = new List<string>(draft.EvidenceIds);
        int queued = session.Submissions.PendingReviews.Count;
        long now = session.Now.TotalMilliseconds;
        ReportValidationResult result = presenter.TrySubmit();
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Reason, Is.Not.Null.And.Not.Empty);
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo(incident));
        Assert.That(draft.SelectedSuspectId, Is.EqualTo(suspect));
        Assert.That(draft.EvidenceIds, Is.EqualTo(evidence));
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(queued));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(now));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Constructor_RejectsMissingDependencies(int dependency)
    {
        Assert.Throws<ArgumentNullException>(() => new ReportFormPresenter(
            dependency == 0 ? null : session, dependency == 1 ? null : draft));
    }

    [Test]
    public void Constructor_KeepsExistingDraftAndDoesNotChooseOptions()
    {
        Assert.That(presenter.Draft, Is.SameAs(draft));
        Assert.That(draft.SelectedIncidentInstanceId, Is.Null);
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.EvidenceIds, Is.Empty);
    }

    [Test]
    public void IncidentChoices_ContainOnlyDeliveredOpenIncidents()
    {
        Assert.That(presenter.GetOpenIncidents(), Is.Empty);
        AdvanceTo(590);
        Assert.That(presenter.GetOpenIncidents().ConvertAll(item => item.InstanceId),
            Is.EqualTo(new[] { "D0I1" }));
        AdvanceTo(675);
        Assert.That(presenter.GetOpenIncidents().ConvertAll(item => item.InstanceId),
            Is.EqualTo(new[] { "D0I2" }));
        AdvanceTo(840);
        Assert.That(presenter.GetOpenIncidents().ConvertAll(item => item.InstanceId),
            Is.EqualTo(new[] { "D0I3" }));
    }

    [Test]
    public void IncidentChoices_CopyAuthoredTextAndAbsoluteDueTime()
    {
        AdvanceTo(675);
        var incident = session.Deliveries.GetDelivered("D0I2");
        var choice = presenter.GetOpenIncidents()[0];
        Assert.That(choice.DisplayName, Is.EqualTo(incident.DisplayName));
        Assert.That(choice.BodyText, Is.EqualTo("Occurred: 10:35–10:40\n\n" + incident.Description));
        Assert.That(choice.DueTimeText, Is.EqualTo("12:00"));
    }

    [TestCase(719, 59, 999, true)]
    [TestCase(720, 0, 0, true)]
    [TestCase(720, 0, 1, false)]
    [TestCase(720, 0, 999, false)]
    public void IncidentChoices_RespectExactDeadlineInstant(
        int minute, int second, int millisecond, bool expected)
    {
        AdvanceTo(minute, second, millisecond);
        Assert.That(presenter.GetOpenIncidents().Exists(item => item.InstanceId == "D0I2"),
            Is.EqualTo(expected));
    }

    [Test]
    public void EmployeeChoices_IncludeEntireRosterDespiteApprovedAbsence()
    {
        AdvanceTo(650);
        var employees = presenter.GetEmployees();
        CollectionAssert.AreEquivalent(context.Day.ScheduleData.EmployeeIds,
            employees.ConvertAll(employee => employee.EmployeeId));
        Assert.That(employees.Count, Is.EqualTo(5));
        Assert.That(employees.Exists(employee => employee.EmployeeId == "bob"), Is.True);
        Assert.That(employees.TrueForAll(employee =>
            !string.IsNullOrWhiteSpace(employee.DisplayName)), Is.True);
    }

    [Test]
    public void Queries_ReturnFreshListsWithoutChangingClockDiscoveryOrDraft()
    {
        AdvanceTo(675);
        draft.SelectIncident("D0I2");
        long now = session.Now.TotalMilliseconds;
        var incidents = presenter.GetOpenIncidents();
        var employees = presenter.GetEmployees();
        var evidence = presenter.GetEvidence();
        incidents.Clear();
        employees.Clear();
        evidence.Clear();
        Assert.That(presenter.GetOpenIncidents().Count, Is.EqualTo(1));
        Assert.That(presenter.GetEmployees().Count, Is.EqualTo(5));
        Assert.That(presenter.GetEvidence(), Is.Empty);
        Assert.That(session.IsEvidenceDiscovered("E_SCHEDULE"), Is.False);
        Assert.That(session.IsEvidenceRead("E_SCHEDULE"), Is.False);
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo("D0I2"));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(now));
    }

    [Test]
    public void PhysicalEvidence_IsEligibleBySharedDiscoveryWithoutRequiringRead()
    {
        Assert.That(presenter.GetEvidence(), Is.Empty);
        Assert.That(session.TryDiscoverEvidence("E_SUPPLIES_RECEIPT"), Is.True);
        var row = presenter.GetEvidence()[0];
        Assert.That(row.EvidenceId, Is.EqualTo("E_SUPPLIES_RECEIPT"));
        Assert.That(row.Source, Is.EqualTo(EvidenceSource.Physical));
        Assert.That(row.IsDiscovered, Is.True);
        Assert.That(row.IsRead, Is.False);
    }

    [Test]
    public void EligibleEvidence_DoesNotFilterOutIrrelevantOrMisleadingDocuments()
    {
        session.TryDiscoverEvidence("E_OFFICE_SOCIAL");
        session.TryDiscoverEvidence("E_SCHEDULE");
        CollectionAssert.AreEquivalent(new[] { "E_OFFICE_SOCIAL", "E_SCHEDULE" },
            Ids(presenter.GetEvidence()));
    }

    [TestCase(0, null)]
    [TestCase(0, "")]
    [TestCase(0, "   ")]
    [TestCase(0, "UNKNOWN")]
    [TestCase(1, null)]
    [TestCase(1, "")]
    [TestCase(1, "   ")]
    [TestCase(1, "UNKNOWN")]
    [TestCase(2, null)]
    [TestCase(2, "")]
    [TestCase(2, "   ")]
    [TestCase(2, "UNKNOWN")]
    public void InvalidSelectionCommands_RejectWithoutChangingValidDraft(int command, string id)
    {
        OpenDraft();
        bool accepted = command == 0 ? presenter.TrySelectIncident(id)
            : command == 1 ? presenter.TrySelectSuspect(id)
            : presenter.TrySetEvidenceSelected(id, true);
        Assert.That(accepted, Is.False);
        AssertOpenDraft();
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void SelectionCommands_UseOrdinalIds(int command)
    {
        OpenDraft();
        bool accepted = command == 0 ? presenter.TrySelectIncident("d0i2")
            : command == 1 ? presenter.TrySelectSuspect("DAVE")
            : presenter.TrySetEvidenceSelected("e_dave_printer", true);
        Assert.That(accepted, Is.False);
        AssertOpenDraft();
    }

    [Test]
    public void UndeliveredIncident_CannotBeSelectedOrSubmittedFromRetainedDraft()
    {
        Assert.That(presenter.TrySelectIncident("D0I2"), Is.False);
        draft.SelectIncident("D0I2");
        draft.SelectSuspect("dave");
        AssertRejectedWithoutMutation();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SuspectAndAttachmentCommands_RequireSelectedOpenIncident(bool attachment)
    {
        AdvanceTo(675);
        session.TryDiscoverEvidence("E_SCHEDULE");
        bool accepted = attachment
            ? presenter.TrySetEvidenceSelected("E_SCHEDULE", true)
            : presenter.TrySelectSuspect("bob");
        Assert.That(accepted, Is.False);
        Assert.That(draft.SelectedIncidentInstanceId, Is.Null);
        Assert.That(draft.EvidenceIds, Is.Empty);
    }

    [Test]
    public void SuspectSelection_DoesNotExcludeEmployeeWithHardAlibi()
    {
        AdvanceTo(675);
        presenter.TrySelectIncident("D0I2");
        Assert.That(presenter.TrySelectSuspect("bob"), Is.True);
        Assert.That(draft.SelectedSuspectId, Is.EqualTo("bob"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UndiscoveredEvidence_IsRejectedWithoutImplicitDiscovery(bool selected)
    {
        AdvanceTo(675);
        presenter.TrySelectIncident("D0I2");
        Assert.That(presenter.TrySetEvidenceSelected("E_SUPPLIES_RECEIPT", selected), Is.False);
        Assert.That(session.IsEvidenceDiscovered("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(session.IsEvidenceRead("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(draft.EvidenceIds, Is.Empty);
    }

    [Test]
    public void FutureEvidence_IsRejectedUntilAvailableAndExplicitlyDiscovered()
    {
        var future = Document("FUTURE", EvidenceSource.InWatch, 0, 700);
        var documents = new List<EvidenceDefinition>(context.Day.EvidenceDatabase.All) { future };
        UseDay(new DayData(context.Day.ScheduleData, documents,
            new List<DayIncidentPlan>(context.Day.Incidents)));
        AdvanceTo(675);
        presenter.TrySelectIncident("D0I2");
        Assert.That(presenter.TrySetEvidenceSelected("FUTURE", true), Is.False);
        Assert.That(session.TryDiscoverEvidence("FUTURE"), Is.False);
        AdvanceTo(700);
        Assert.That(presenter.TrySetEvidenceSelected("FUTURE", true), Is.False);
        Assert.That(session.TryDiscoverEvidence("FUTURE"), Is.True);
        Assert.That(presenter.TrySetEvidenceSelected("FUTURE", true), Is.True);
        Assert.That(draft.EvidenceIds, Is.EqualTo(new[] { "FUTURE" }));
    }

    [TestCase("D0I2", true)]
    [TestCase("SECOND", false)]
    public void IncidentSelection_PreservesSameDraftAndClearsOnSwitch(string id, bool keep)
    {
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I2_PrinterDamage.asset");
        Assert.That(definition, Is.Not.Null);
        var plans = new List<DayIncidentPlan>(context.Day.Incidents)
        {
            new DayIncidentPlan(definition, new IncidentScheduleEntry("SECOND", "I2",
                new TimeRange(0, 635, 640), 675, 720), "dave")
        };
        UseDay(new DayData(context.Day.ScheduleData,
            new List<EvidenceDefinition>(context.Day.EvidenceDatabase.All), plans));
        OpenDraft();
        Assert.That(presenter.TrySelectIncident(id), Is.True);
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo(id));
        if (keep) AssertOpenDraft();
        else
        {
            Assert.That(draft.SelectedSuspectId, Is.Null);
            Assert.That(draft.EvidenceIds, Is.Empty);
        }
    }

    [Test]
    public void AttachmentCommands_AreIdempotentAndDoNotReadOrSubmit()
    {
        OpenDraft();
        long now = session.Now.TotalMilliseconds;
        Assert.That(presenter.TrySetEvidenceSelected("E_DAVE_PRINTER", true), Is.True);
        AssertOpenDraft();
        Assert.That(presenter.TrySetEvidenceSelected("E_DAVE_PRINTER", false), Is.True);
        Assert.That(presenter.TrySetEvidenceSelected("E_DAVE_PRINTER", false), Is.True);
        Assert.That(draft.EvidenceIds, Is.Empty);
        Assert.That(session.IsEvidenceRead("E_DAVE_PRINTER"), Is.False);
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
        Assert.That(session.Deliveries.GetDelivered("D0I2").Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(now));
    }

    [Test]
    public void QueriesDoNotClearPendingDraft_ExplicitReconciliationDoes()
    {
        OpenDraft();
        context.Submit("D0I2", "dave", "E_DAVE_PRINTER");
        Assert.That(presenter.GetOpenIncidents(), Is.Empty);
        AssertOpenDraft();
        presenter.ReconcileDraft();
        Assert.That(draft.SelectedIncidentInstanceId, Is.Null);
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.EvidenceIds, Is.Empty);
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(1));
    }

    [Test]
    public void Reconciliation_PrunesStaleSuspectAndEvidenceWithoutSubmitting()
    {
        OpenDraft();
        draft.SelectSuspect("UNKNOWN");
        draft.SetEvidenceSelected("UNDISCOVERED", true);
        presenter.ReconcileDraft();
        Assert.That(draft.SelectedIncidentInstanceId, Is.EqualTo("D0I2"));
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.EvidenceIds, Is.EqualTo(new[] { "E_DAVE_PRINTER" }));
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Submit_MissingSelections_ReturnsFeedbackWithoutMutation(bool selectIncident)
    {
        AdvanceTo(675);
        if (selectIncident) presenter.TrySelectIncident("D0I2");
        AssertRejectedWithoutMutation();
    }

    [Test]
    public void Submit_UnknownRetainedSuspect_IsRejected()
    {
        OpenDraft();
        draft.SelectSuspect("UNKNOWN");
        AssertRejectedWithoutMutation();
    }

    [TestCase("UNKNOWN")]
    [TestCase("E_SUPPLIES_RECEIPT")]
    [TestCase("e_dave_printer")]
    public void Submit_InvalidAttachment_PreservesWholeAccusationInsteadOfPruning(string id)
    {
        OpenDraft();
        draft.SetEvidenceSelected(id, true);
        AssertRejectedWithoutMutation();
        Assert.That(draft.EvidenceIds.Count, Is.EqualTo(2));
    }

    [TestCase(719, 59, 999, true)]
    [TestCase(720, 0, 0, true)]
    [TestCase(720, 0, 1, false)]
    [TestCase(720, 0, 999, false)]
    public void Submit_UsesExactDeadlineBoundary(
        int minute, int second, int millisecond, bool accepted)
    {
        OpenDraft();
        AdvanceTo(minute, second, millisecond);
        if (!accepted)
        {
            AssertRejectedWithoutMutation();
            return;
        }
        Assert.That(presenter.TrySubmit().IsValid, Is.True);
        Assert.That(session.Deliveries.GetDelivered("D0I2").Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(1));
    }

    [Test]
    public void AcceptedSubmission_UsesExistingPreciseClockAndCopiedIdsThenClearsDraft()
    {
        OpenDraft();
        AdvanceTo(680, 30, 250);
        long now = session.Now.TotalMilliseconds;
        ReportValidationResult result = presenter.TrySubmit();
        Assert.That(result.IsValid, Is.True, result.Reason);
        var queued = session.Submissions.PendingReviews[0];
        Assert.That(queued.SubmittedAt.TotalMilliseconds, Is.EqualTo(now));
        Assert.That(queued.Report.SubmittedMinute, Is.EqualTo(680));
        Assert.That(queued.Report.EvidenceIds, Is.EqualTo(new[] { "E_DAVE_PRINTER" }));
        Assert.That(queued.Incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(queued.Incident.Decision, Is.Null);
        Assert.That(draft.SelectedIncidentInstanceId, Is.Null);
        Assert.That(draft.SelectedSuspectId, Is.Null);
        Assert.That(draft.EvidenceIds, Is.Empty);
        draft.SelectIncident("OTHER");
        draft.SetEvidenceSelected("OTHER_EVIDENCE", true);
        Assert.That(queued.Report.EvidenceIds, Is.EqualTo(new[] { "E_DAVE_PRINTER" }));
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(now));
    }

    [Test]
    public void SubmissionWithNoAttachments_IsAcceptedForLaterHrEvaluation()
    {
        AdvanceTo(675);
        presenter.TrySelectIncident("D0I2");
        presenter.TrySelectSuspect("dave");
        Assert.That(presenter.TrySubmit().IsValid, Is.True);
        var incident = session.Deliveries.GetDelivered("D0I2");
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(incident.SubmittedReport.EvidenceIds, Is.Empty);
        Assert.That(incident.Decision, Is.Null);
    }

    [TestCase("E_DAVE_PRINTER", IncidentStatus.Approved)]
    [TestCase("E_OFFICE_SOCIAL", IncidentStatus.Rejected)]
    public void AcceptedSubmission_RemainsPendingUntilExistingDelayedHrReview(
        string evidenceId, IncidentStatus expected)
    {
        AdvanceTo(675);
        presenter.TrySelectIncident("D0I2");
        presenter.TrySelectSuspect("dave");
        session.TryDiscoverEvidence(evidenceId);
        presenter.TrySetEvidenceSelected(evidenceId, true);
        Assert.That(presenter.TrySubmit().IsValid, Is.True);
        var incident = session.Deliveries.GetDelivered("D0I2");
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        AdvanceTo(689, 59, 999);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        AdvanceTo(690);
        Assert.That(incident.Status, Is.EqualTo(expected));
        Assert.That(session.Submissions.PendingReviews, Is.Empty);
        Assert.That(presenter.GetOpenIncidents(), Is.Empty);
    }

    [Test]
    public void RepeatedSubmitAndStalePendingDraft_CannotQueueAnotherReview()
    {
        OpenDraft();
        Assert.That(presenter.TrySubmit().IsValid, Is.True);
        AssertRejectedWithoutMutation();
        draft.SelectIncident("D0I2");
        draft.SelectSuspect("dave");
        draft.SetEvidenceSelected("E_DAVE_PRINTER", true);
        AssertRejectedWithoutMutation();
        Assert.That(session.Submissions.PendingReviews.Count, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalWorkday_OffersNoOpenChoicesAndRejectsSubmit(bool fired)
    {
        if (fired)
        {
            UseDay(null, new CredibilityRules(1, 25, 35, 0));
            session.AdvanceRealSeconds(10000);
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Fired));
        }
        else
        {
            UseDay(new DayData(context.Day.ScheduleData,
                new List<EvidenceDefinition>(context.Day.EvidenceDatabase.All),
                new List<DayIncidentPlan>()));
            AdvanceTo(1020);
            Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        }
        draft.SelectIncident("D0I2");
        draft.SelectSuspect("dave");
        Assert.That(presenter.GetOpenIncidents(), Is.Empty);
        AssertRejectedWithoutMutation();
    }
}
