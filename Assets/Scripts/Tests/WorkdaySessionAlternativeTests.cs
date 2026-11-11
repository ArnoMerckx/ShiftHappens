using NUnit.Framework;

public class WorkdaySessionAlternativeTests
{
    [TestCase("karen", "E_KAREN_COLLECTION")]
    [TestCase("steve", "E_SUPPLIES_RECEIPT")]
    public void SupportedAlternative_CompletesMondayWithReportsAtExactDeadlines(
        string finalSuspect, string finalEvidence)
    {
        var context = new WorkdaySessionTestContext();
        WorkdaySession session = context.Session;
        int reviews = 0;
        int overdue = 0;
        session.Reviews.ReviewCompleted += (job, decision) => reviews++;
        session.Deadlines.IncidentOverdue += incident => overdue++;

        context.AdvanceTo(630);
        IncidentInstance first = context.Submit("D0I1", "bob", "E_SCHEDULE");
        Assert.That(first.SubmittedReport.SubmittedMinute, Is.EqualTo(630));
        context.AdvanceTo(640);
        Assert.That(first.Status, Is.EqualTo(IncidentStatus.Approved));

        context.AdvanceTo(720);
        IncidentInstance second = context.Submit("D0I2", "dave", "E_DAVE_PRINTER");
        Assert.That(second.SubmittedReport.SubmittedMinute, Is.EqualTo(720));
        context.AdvanceTo(735);
        Assert.That(second.Status, Is.EqualTo(IncidentStatus.Approved));

        context.AdvanceTo(900);
        IncidentInstance third = context.Submit("D0I3", finalSuspect, finalEvidence);
        Assert.That(third.SubmittedReport.SubmittedMinute, Is.EqualTo(900));
        Assert.That(third.SubmittedReport.SuspectId, Is.EqualTo(finalSuspect));
        CollectionAssert.AreEqual(new[] { finalEvidence }, third.SubmittedReport.EvidenceIds);
        Assert.That(session.IsEvidenceRead(finalEvidence), Is.False);
        context.AdvanceTo(914, 59);
        Assert.That(third.Status, Is.EqualTo(IncidentStatus.Pending));
        context.AdvanceTo(915);
        Assert.That(third.Status, Is.EqualTo(IncidentStatus.Approved));
        Assert.That(third.Decision.Verdict, Is.EqualTo(HrVerdict.Approved));
        Assert.That(reviews, Is.EqualTo(3));
        Assert.That(overdue, Is.Zero);

        // An unselected alternative is not needed to approve this report.
        string unused = finalSuspect == "karen"
            ? "E_SUPPLIES_RECEIPT" : "E_KAREN_COLLECTION";
        Assert.That(session.IsEvidenceAvailable(unused), Is.True);
        Assert.That(session.IsEvidenceDiscovered(unused), Is.False);
        Assert.That(session.IsEvidenceRead(unused), Is.False);
        Assert.That(session.CanAttachEvidence(unused), Is.False);

        context.AdvanceTo(1020);
        WorkdaySummary summary = session.Workday.Summary;
        Assert.That(session.Workday.State, Is.EqualTo(WorkdayState.Completed));
        Assert.That(summary.Credibility, Is.EqualTo(80));
        Assert.That(session.Delegation.Expiries.Count, Is.EqualTo(2));
        Assert.That(session.Workload.History, Is.Empty);
        Assert.That(session.Trust.GetTrust("steve"), Is.EqualTo(finalSuspect == "steve" ? 40 : 50));
        Assert.That(summary.Incidents.Count, Is.EqualTo(3));
        Assert.That(summary.History.Count, Is.EqualTo(3));
        Assert.That(summary.MissedDeadlines, Is.Empty);
        Assert.That(session.Submissions.PendingReviews, Is.Empty);

        var expectedReviewMinutes = new[] { 640, 735, 915 };
        for (int i = 0; i < summary.History.Count; i++)
        {
            ReviewHistoryEntry entry = summary.History[i];
            Assert.That(entry.Decision.Verdict, Is.EqualTo(HrVerdict.Approved));
            Assert.That(entry.ReviewedAt.TotalMilliseconds,
                Is.EqualTo(new GameTime(0, expectedReviewMinutes[i]).TotalMilliseconds));
            Assert.That(entry.CredibilityBefore, Is.EqualTo(90));
            Assert.That(entry.CredibilityAfter, Is.EqualTo(90));
            Assert.That(summary.Incidents[i].Status, Is.EqualTo(IncidentStatus.Approved));
        }
        Assert.That(summary.History[2].Report, Is.SameAs(third.SubmittedReport));
        Assert.That(summary.Incidents[2].Report.SuspectId, Is.EqualTo(finalSuspect));
        CollectionAssert.AreEqual(new[] { finalEvidence },
            summary.Incidents[2].Report.EvidenceIds);
        Assert.That(reviews, Is.EqualTo(3));
        Assert.That(overdue, Is.Zero);
    }
}
