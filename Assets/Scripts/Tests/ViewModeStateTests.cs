using System;
using NUnit.Framework;

public class ViewModeStateTests
{
    [Test]
    public void NewState_StartsInOfficeWithoutPendingDestination()
    {
        var state = new ViewModeState();

        AssertState(state, ViewMode.Office, null);
    }

    [TestCase(ViewMode.Office, "Enter", true, ViewMode.Transition, ViewMode.Computer)]
    [TestCase(ViewMode.Office, "Inspect", true, ViewMode.InspectDocument, null)]
    [TestCase(ViewMode.Office, "Return", false, ViewMode.Office, null)]
    [TestCase(ViewMode.Office, "Complete", false, ViewMode.Office, null)]
    [TestCase(ViewMode.Computer, "Enter", false, ViewMode.Computer, null)]
    [TestCase(ViewMode.Computer, "Inspect", false, ViewMode.Computer, null)]
    [TestCase(ViewMode.Computer, "Return", true, ViewMode.Transition, ViewMode.Office)]
    [TestCase(ViewMode.Computer, "Complete", false, ViewMode.Computer, null)]
    [TestCase(ViewMode.InspectDocument, "Enter", false, ViewMode.InspectDocument, null)]
    [TestCase(ViewMode.InspectDocument, "Inspect", false, ViewMode.InspectDocument, null)]
    [TestCase(ViewMode.InspectDocument, "Return", true, ViewMode.Office, null)]
    [TestCase(ViewMode.InspectDocument, "Complete", false, ViewMode.InspectDocument, null)]
    public void StableMode_OnlyAcceptsItsLegalRequests(
        ViewMode initialMode, string request, bool accepted,
        ViewMode expectedMode, ViewMode? expectedPending)
    {
        ViewModeState state = CreateInMode(initialMode);

        bool result = Request(state, request);

        Assert.That(result, Is.EqualTo(accepted));
        AssertState(state, expectedMode, expectedPending);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Transition_BlocksRepeatedAndConflictingRequestsUntilCompletion(
        bool returningToOffice)
    {
        ViewModeState state = CreateInMode(
            returningToOffice ? ViewMode.Computer : ViewMode.Office);

        Assert.That(returningToOffice
            ? state.TryReturnToOffice()
            : state.TryEnterComputer(), Is.True);

        ViewMode destination = returningToOffice
            ? ViewMode.Office : ViewMode.Computer;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.That(state.TryEnterComputer(), Is.False);
            AssertState(state, ViewMode.Transition, destination);
            Assert.That(state.TryInspectDocument(), Is.False);
            AssertState(state, ViewMode.Transition, destination);
            Assert.That(state.TryReturnToOffice(), Is.False);
            AssertState(state, ViewMode.Transition, destination);
        }

        Assert.That(state.TryCompleteTransition(), Is.True);
        AssertState(state, destination, null);
        Assert.That(state.TryCompleteTransition(), Is.False);
        AssertState(state, destination, null);
    }

    [Test]
    public void RepeatedComputerAndDocumentCycles_ReturnToCleanOfficeState()
    {
        var state = new ViewModeState();

        for (int cycle = 0; cycle < 20; cycle++)
        {
            Assert.That(state.TryEnterComputer(), Is.True);
            AssertState(state, ViewMode.Transition, ViewMode.Computer);
            Assert.That(state.TryCompleteTransition(), Is.True);
            AssertState(state, ViewMode.Computer, null);
            Assert.That(state.TryReturnToOffice(), Is.True);
            AssertState(state, ViewMode.Transition, ViewMode.Office);
            Assert.That(state.TryCompleteTransition(), Is.True);
            AssertState(state, ViewMode.Office, null);
            Assert.That(state.TryInspectDocument(), Is.True);
            AssertState(state, ViewMode.InspectDocument, null);
            Assert.That(state.TryReturnToOffice(), Is.True);
            AssertState(state, ViewMode.Office, null);
        }
    }

    [Test]
    public void SeparateInstances_DoNotShareModeOrPendingDestination()
    {
        var first = new ViewModeState();
        var second = new ViewModeState();

        Assert.That(first.TryEnterComputer(), Is.True);
        AssertState(second, ViewMode.Office, null);
        Assert.That(second.TryInspectDocument(), Is.True);
        AssertState(first, ViewMode.Transition, ViewMode.Computer);
        Assert.That(first.TryCompleteTransition(), Is.True);
        AssertState(second, ViewMode.InspectDocument, null);
        Assert.That(second.TryReturnToOffice(), Is.True);
        AssertState(first, ViewMode.Computer, null);
    }

    private static ViewModeState CreateInMode(ViewMode mode)
    {
        var state = new ViewModeState();

        if (mode == ViewMode.Computer)
        {
            Assert.That(state.TryEnterComputer(), Is.True);
            Assert.That(state.TryCompleteTransition(), Is.True);
        }
        else if (mode == ViewMode.InspectDocument)
        {
            Assert.That(state.TryInspectDocument(), Is.True);
        }

        AssertState(state, mode, null);
        return state;
    }

    private static bool Request(ViewModeState state, string request)
    {
        switch (request)
        {
            case "Enter": return state.TryEnterComputer();
            case "Inspect": return state.TryInspectDocument();
            case "Return": return state.TryReturnToOffice();
            case "Complete": return state.TryCompleteTransition();
            default: throw new ArgumentException("Unknown test request.", nameof(request));
        }
    }

    private static void AssertState(
        ViewModeState state, ViewMode mode, ViewMode? pending)
    {
        Assert.That(state.CurrentMode, Is.EqualTo(mode));
        Assert.That(state.PendingMode, Is.EqualTo(pending));
    }
}
