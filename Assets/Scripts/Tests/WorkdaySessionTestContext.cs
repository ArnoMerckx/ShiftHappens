using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

internal sealed class WorkdaySessionTestContext
{
    public DayData Day { get; }
    public WorkdaySession Session { get; }

    public WorkdaySessionTestContext(
        DayData day = null, CredibilityRules rules = null)
    {
        if (day == null)
        {
            var definition = AssetDatabase.LoadAssetAtPath<DayDefinition>(
                "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
            Assert.That(definition, Is.Not.Null);
            day = definition.GetData();
        }

        Day = day;
        Session = new WorkdaySession(day, rules);
    }

    public void AdvanceTo(int minute, int second = 0)
    {
        var target = new GameTime(Session.Settings.DayIndex, minute, second);
        long delta = target.TotalMilliseconds - Session.Now.TotalMilliseconds;
        Assert.That(delta, Is.GreaterThanOrEqualTo(0));
        Session.AdvanceRealSeconds(delta /
            (GameTime.MillisecondsPerSecond * Session.Settings.GameSecondsPerRealSecond));
        Assert.That(Session.Now.TotalMilliseconds, Is.EqualTo(target.TotalMilliseconds));
    }

    public IncidentInstance Submit(
        string instanceId, string suspectId, params string[] evidenceIds)
    {
        foreach (string id in evidenceIds)
        {
            if (!Session.IsEvidenceDiscovered(id))
                Assert.That(Session.TryDiscoverEvidence(id), Is.True);
        }

        ReportValidationResult result = Session.Submissions.TrySubmit(
            instanceId, suspectId, new List<string>(evidenceIds));
        Assert.That(result.IsValid, Is.True, result.Reason);
        IncidentInstance incident = Session.Deliveries.GetDelivered(instanceId);
        Assert.That(incident.Status, Is.EqualTo(IncidentStatus.Pending));
        return incident;
    }
}
