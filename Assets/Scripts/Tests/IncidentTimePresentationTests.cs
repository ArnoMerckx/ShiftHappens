using NUnit.Framework;
using UnityEditor;

public class IncidentTimePresentationTests
{
    private static void AssertTimeContext(
        IncidentInstance incident, string expectedRange, string expectedDue)
    {
        var notice = new IncidentNoticeViewData(incident);
        var overview = new ReportOverviewViewData(incident);
        string expectedBody = expectedRange + "\n\n" + incident.Description;

        Assert.That(notice.BodyText, Is.EqualTo(expectedBody));
        Assert.That(overview.BodyText, Is.EqualTo(expectedBody));
        Assert.That(notice.DueTimeText, Is.EqualTo(expectedDue));
        Assert.That(overview.DueTimeText, Is.EqualTo(expectedDue));
    }

    [TestCase("D0I1", 590, "Occurred: 09:35–09:40", "10:30")]
    [TestCase("D0I2", 675, "Occurred: 10:35–10:40", "12:00")]
    [TestCase("D0I3", 840, "Occurred: 13:35–13:40", "15:00")]
    public void AuthoredMonday_NoticesAndOverviewExposeOccurrenceAndDistinctDeadline(
        string instanceId, int delivery, string expectedRange, string expectedDue)
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(delivery);
        IncidentInstance incident = context.Session.Deliveries.GetDelivered(instanceId);

        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Open));
        AssertTimeContext(incident, expectedRange, expectedDue);
    }

    [TestCase(5, 65, 70, 125, "Occurred: 00:05–01:05", "02:05")]
    [TestCase(1385, 1390, 1395, 1439, "Occurred: 23:05–23:10", "23:59")]
    public void SameTemplateWithDifferentSchedule_UsesScheduleTimesAndPadsMinutes(
        int start, int end, int delivery, int deadline,
        string expectedRange, string expectedDue)
    {
        var definition = AssetDatabase.LoadAssetAtPath<IncidentDefinition>(
            "Assets/Scripts/IncidentSystem/IncidentObjects/Incidents/I1_CoffeeSpill.asset");
        Assert.That(definition, Is.Not.Null);

        // Reuse the authored template without editing it. Times belong to this instance.
        var incident = new IncidentInstance(definition,
            new IncidentScheduleEntry("time_presentation", definition.IncidentId,
                new TimeRange(0, start, end), delivery, deadline));

        AssertTimeContext(incident, expectedRange, expectedDue);
    }

    [TestCase("bob", IncidentStatus.Approved)]
    [TestCase("alice", IncidentStatus.Rejected)]
    public void ReviewChangesStatus_ButKeepsHistoricalContextAndOldSnapshots(
        string suspect, IncidentStatus expectedStatus)
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        IncidentInstance incident = context.Session.Deliveries.GetDelivered("D0I1");
        var originalNotice = new IncidentNoticeViewData(incident);
        var open = new ReportOverviewViewData(incident);
        string expectedBody = "Occurred: 09:35–09:40\n\n" + incident.Description;

        context.Submit("D0I1", suspect, "E_SCHEDULE");
        var pending = new ReportOverviewViewData(incident);
        Assert.That(pending.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(pending.BodyText, Is.EqualTo(expectedBody));
        AssertTimeContext(incident, "Occurred: 09:35–09:40", "10:30");

        context.AdvanceTo(600);
        var reviewed = new ReportOverviewViewData(incident);
        Assert.That(reviewed.Status, Is.EqualTo(expectedStatus));
        AssertTimeContext(incident, "Occurred: 09:35–09:40", "10:30");

        Assert.That(originalNotice.BodyText, Is.EqualTo(expectedBody));
        Assert.That(originalNotice.DueTimeText, Is.EqualTo("10:30"));
        Assert.That(open.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(open.BodyText, Is.EqualTo(expectedBody));
        Assert.That(open.SuspectId, Is.Null);
        Assert.That(pending.Status, Is.EqualTo(IncidentStatus.Pending));
        Assert.That(pending.BodyText, Is.EqualTo(expectedBody));
        Assert.That(pending.DecisionSummary, Is.Empty);
        Assert.That(reviewed.SuspectId, Is.EqualTo(suspect));
        CollectionAssert.AreEqual(new[] { "E_SCHEDULE" }, reviewed.EvidenceIds);
        Assert.That(reviewed.DecisionReasons, Is.Not.Empty);
    }

    [Test]
    public void MissedDeadline_KeepsHistoricalContextInOverdueOverview()
    {
        var context = new WorkdaySessionTestContext();
        context.AdvanceTo(590);
        IncidentInstance incident = context.Session.Deliveries.GetDelivered("D0I1");
        var open = new ReportOverviewViewData(incident);

        context.AdvanceTo(631);
        var overdue = new ReportOverviewViewData(incident);
        Assert.That(overdue.Status, Is.EqualTo(IncidentStatus.Overdue));
        AssertTimeContext(incident, "Occurred: 09:35–09:40", "10:30");
        Assert.That(overdue.BodyText, Is.EqualTo(open.BodyText));
        Assert.That(open.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(overdue.SuspectId, Is.Null);
    }
}
