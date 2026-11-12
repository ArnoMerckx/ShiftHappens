using System;
using NUnit.Framework;

public class TaskRelayAppStateTests
{
    [Test]
    public void RequestChanges_ClearHelperAndFeedbackButPreservePerRequestScroll()
    {
        var state = new TaskRelayAppState();
        Assert.Throws<InvalidOperationException>(() => state.SelectEmployee("dave"));
        state.SelectRequest("R1");
        state.SelectEmployee("dave");
        state.Requests.SetReaderScrollPosition(0.3f);
        state.Employees.SetListScrollPosition(0.4f);
        state.SetFeedback("Recorded feedback");
        state.SelectRequest("R1");
        state.SelectEmployee("dave");
        Assert.That(state.Feedback, Is.EqualTo("Recorded feedback"));
        Assert.That(state.Employees.SelectedItemId, Is.EqualTo("dave"));

        state.SelectRequest("R2");
        Assert.That(state.Employees.SelectedItemId, Is.Null);
        Assert.That(state.Feedback, Is.Empty);
        state.Requests.SetReaderScrollPosition(0.8f);
        state.SelectEmployee("bob");
        state.SetFeedback("Other feedback");
        state.SelectRequest("R1");
        Assert.That(state.Requests.ReaderScrollPosition, Is.EqualTo(0.3f));
        Assert.That(state.Employees.ListScrollPosition, Is.EqualTo(0.4f));
        Assert.That(state.Employees.SelectedItemId, Is.Null);
        Assert.That(state.Feedback, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void InvalidIds_DoNotDamageCurrentDraft(string invalid)
    {
        var state = new TaskRelayAppState();
        state.SelectRequest("R1");
        state.SelectEmployee("dave");
        state.SetFeedback("Keep this");
        Assert.Throws<ArgumentException>(() => state.SelectRequest(invalid));
        Assert.Throws<ArgumentException>(() => state.SelectEmployee(invalid));
        Assert.That(state.Requests.SelectedItemId, Is.EqualTo("R1"));
        Assert.That(state.Employees.SelectedItemId, Is.EqualTo("dave"));
        Assert.That(state.Feedback, Is.EqualTo("Keep this"));
    }

    [Test]
    public void OwnersAndReadingStates_AreIndependentAndDraftClearingRetainsRequest()
    {
        var first = new ShiftOSAppState();
        var second = new ShiftOSAppState();
        Assert.That(first.TaskRelay, Is.SameAs(first.TaskRelay));
        Assert.That(first.TaskRelay, Is.Not.SameAs(second.TaskRelay));
        Assert.That(first.TaskRelay.Requests, Is.Not.SameAs(first.TaskRelay.Employees));
        Assert.That(first.TaskRelay.Requests, Is.Not.SameAs(first.Swarms));
        Assert.That(first.TaskRelay.Employees, Is.Not.SameAs(first.ShareLess));
        first.TaskRelay.SelectRequest("R1");
        first.TaskRelay.SelectEmployee("bob");
        first.TaskRelay.SetFeedback("Message");
        first.TaskRelay.SelectEmployee("dave");
        Assert.That(first.TaskRelay.Feedback, Is.Empty);
        first.TaskRelay.SetFeedback(null);
        Assert.That(first.TaskRelay.Feedback, Is.Empty);
        first.TaskRelay.ClearEmployeeSelection();
        Assert.That(first.TaskRelay.Requests.SelectedItemId, Is.EqualTo("R1"));
        Assert.That(first.TaskRelay.Employees.SelectedItemId, Is.Null);
        Assert.That(second.TaskRelay.Requests.SelectedItemId, Is.Null);
        Assert.That(first.Swarms.SelectedItemId, Is.Null);
        Assert.That(first.ShareLess.SelectedItemId, Is.Null);
    }
}
