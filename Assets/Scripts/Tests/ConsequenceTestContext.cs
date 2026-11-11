using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

internal sealed class ConsequenceTestContext
{
    public DayData Day { get; }
    public WorkdaySession Session { get; }
    public EventScheduler Scheduler { get; }

    public ConsequenceTestContext(DayData day = null, int credibility = 80, int trust = 50,
        CredibilityRules scoreRules = null, ConsequenceRules rules = null,
        WorkdayOpeningState opening = null)
    {
        Day = day ?? CreateDay();
        var entries = new List<EmployeeTrustEntry>();
        foreach (string id in Day.ScheduleData.EmployeeIds)
            entries.Add(new EmployeeTrustEntry(id, trust));
        Session = new WorkdaySession(Day, scoreRules, rules,
            opening ?? new WorkdayOpeningState(credibility, entries, new List<AssignmentHistoryEntry>()));
        var field = typeof(WorkdaySession).GetField("scheduler", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        Scheduler = (EventScheduler)field.GetValue(Session);
    }

    public void Advance(int minute, int second = 0, int millisecond = 0)
    {
        Scheduler.AdvanceTo(new GameTime(Session.Settings.DayIndex, minute, second, millisecond));
    }

    public static DayData CreateDay(bool daveBlocked = false, bool selfHelp = false,
        bool fixedWindow = true, int requestCount = 1)
    {
        var ids = new List<string> { "alice", "bob", "dave", "steve", "karen" };
        var records = new List<ApprovedScheduleRecord> {
            new ApprovedScheduleRecord("bob_absence", "bob", "offsite",
                new TimeRange(3, 630, 645), 100, true, true) };
        if (daveBlocked)
            records.Add(new ApprovedScheduleRecord("dave_commitment", "dave", "printer",
                new TimeRange(3, 630, 645), 100, false, true));
        var availability = new List<EmployeeAvailability>();
        foreach (string id in ids)
            availability.Add(new EmployeeAvailability(id, new TimeRange(3, 540, 1020)));
        var schedule = new DayScheduleData(
            new WorkdaySettings("consequence_day", "Consequence day", 3, 540, 1020, 40),
            ids, records, new List<ApprovedScheduleRecord>(), new List<Claim>(),
            ordinaryAvailability: availability);
        var ranked = new List<RequestSuitabilityEntry> {
            new RequestSuitabilityEntry("dave", 0), new RequestSuitabilityEntry("steve", 1) };
        var unsuitable = new List<string> { "bob", "karen" };
        if (selfHelp) ranked.Add(new RequestSuitabilityEntry("alice", 0));
        else unsuitable.Add("alice");
        var profile = new RequestProfileData("pc", EmployeeCapability.ComputerSupport, ranked, unsuitable);
        var request = new RequestData("pc_help", "PC help", "Arrange help.", "alice",
            EmployeeCapability.ComputerSupport, selfHelp);
        var plans = new List<DayRequestPlan>();
        for (int i = 1; i <= requestCount; i++)
            plans.Add(new DayRequestPlan("R" + i, request, profile, new GameTime(3, 595),
                fixedWindow ? (TimeRange?)new TimeRange(3, 630, 645) : null));
        return new DayData(schedule, new List<EvidenceDefinition>(), new List<DayIncidentPlan>(), plans);
    }

    public static void ReplayDecision(ConsequenceCoordinator coordinator, DelegationDecision decision)
    {
        var method = typeof(ConsequenceCoordinator).GetMethod("OnDecisionResolved",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var callback = (Action<DelegationDecision>)Delegate.CreateDelegate(
            typeof(Action<DelegationDecision>), coordinator, method);
        callback(decision);
    }

    public static void ReplayExpiry(ConsequenceCoordinator coordinator, RequestExpiryEntry entry)
    {
        var method = typeof(ConsequenceCoordinator).GetMethod("OnRequestExpired",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var callback = (Action<RequestExpiryEntry>)Delegate.CreateDelegate(
            typeof(Action<RequestExpiryEntry>), coordinator, method);
        callback(entry);
    }
}
