using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DayRequestTests
{
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

    private T Own<T>() where T : ScriptableObject
    {
        var value = ScriptableObject.CreateInstance<T>();
        owned.Add(value);
        return value;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing serialized field: " + name);
        field.SetValue(target, value);
    }

    private static RequestData Content(string requester = "alice")
    {
        return new RequestData("pc_help", "PC help", "Arrange desktop troubleshooting.",
            requester, EmployeeCapability.ComputerSupport);
    }

    private static RequestProfileData Profile(List<string> unsuitable = null)
    {
        return new RequestProfileData("desktop_support", EmployeeCapability.ComputerSupport,
            new List<RequestSuitabilityEntry> { new RequestSuitabilityEntry("dave", 0) },
            unsuitable ?? new List<string> { "alice" });
    }

    private static DayData Day(List<DayRequestPlan> requests = null)
    {
        var schedule = new DayScheduleData(
            new WorkdaySettings("request_day", "Request day", 0, 540, 1020, 40),
            new List<string> { "alice", "dave" }, new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>());
        return new DayData(schedule, new List<EvidenceDefinition>(), new List<DayIncidentPlan>(), requests);
    }

    private static DayRequestPlan EndOfShift(GameTime delivery)
    {
        return new DayRequestPlan("R1", Content(), Profile(), delivery);
    }

    private DayDefinition AuthoredDay(out DayRequestEntry entry, out RequestDefinition request)
    {
        var alice = Own<EmployeeDefinition>();
        SetField(alice, "employeeId", "alice");
        SetField(alice, "displayName", "Alice");
        var dave = Own<EmployeeDefinition>();
        SetField(dave, "employeeId", "dave");
        SetField(dave, "displayName", "Dave");
        SetField(dave, "capabilities", new List<EmployeeCapability> { EmployeeCapability.ComputerSupport });

        var tier = new RequestProfileTierEntry();
        SetField(tier, "employee", dave);
        SetField(tier, "tier", 0);
        var profile = Own<RequestProfileDefinition>();
        SetField(profile, "profileId", "desktop_support");
        SetField(profile, "requiredCapability", EmployeeCapability.ComputerSupport);
        SetField(profile, "rankedEmployees", new List<RequestProfileTierEntry> { tier });
        SetField(profile, "unsuitableEmployees", new List<EmployeeDefinition> { alice });

        request = Own<RequestDefinition>();
        SetField(request, "requestId", "pc_help");
        SetField(request, "displayName", "PC help");
        SetField(request, "description", "Arrange desktop troubleshooting.");
        SetField(request, "requester", alice);
        SetField(request, "category", EmployeeCapability.ComputerSupport);

        entry = new DayRequestEntry();
        SetField(entry, "instanceId", "D3R1");
        SetField(entry, "request", request);
        SetField(entry, "profile", profile);
        SetField(entry, "deliveryMinute", 600);
        SetField(entry, "hasFixedWindow", true);
        SetField(entry, "helpStartMinute", 660);
        SetField(entry, "helpEndMinute", 720);

        var day = Own<DayDefinition>();
        SetField(day, "dayId", "authored_requests");
        SetField(day, "displayName", "Authored requests");
        SetField(day, "dayIndex", 3);
        SetField(day, "employees", new List<EmployeeDefinition> { alice, dave });
        SetField(day, "requests", new List<DayRequestEntry> { entry });
        return day;
    }

    [TestCase("requestId")]
    [TestCase("displayName")]
    [TestCase("description")]
    [TestCase("requesterId")]
    [TestCase("noneCategory")]
    [TestCase("undefinedCategory")]
    public void RequestContent_RejectsMissingIdentityTextOrCategory(string invalid)
    {
        var category = invalid == "noneCategory" ? EmployeeCapability.None :
            invalid == "undefinedCategory" ? (EmployeeCapability)999 : EmployeeCapability.ComputerSupport;
        Assert.Catch<ArgumentException>(() => new RequestData(
            invalid == "requestId" ? " " : "pc_help",
            invalid == "displayName" ? " " : "PC help",
            invalid == "description" ? " " : "Arrange help.",
            invalid == "requesterId" ? " " : "alice", category));
    }

    [TestCase("blankInstance")]
    [TestCase("nullRequest")]
    [TestCase("nullProfile")]
    [TestCase("categoryMismatch")]
    [TestCase("defaultWindow")]
    [TestCase("otherDayWindow")]
    [TestCase("cutoffBeforeDelivery")]
    [TestCase("otherDayCutoff")]
    [TestCase("endWithCutoff")]
    public void RequestPlan_RejectsInconsistentTimingOrContent(string invalid)
    {
        var request = invalid == "nullRequest" ? null : Content();
        var profile = invalid == "nullProfile" ? null : Profile();
        if (invalid == "categoryMismatch")
            profile = new RequestProfileData("equipment", EmployeeCapability.EquipmentHandling,
                new List<RequestSuitabilityEntry>(), new List<string> { "alice", "dave" });
        TimeRange? window = new TimeRange(0, 660, 720);
        GameTime? cutoff = null;
        if (invalid == "defaultWindow") window = default(TimeRange);
        if (invalid == "otherDayWindow") window = new TimeRange(1, 660, 720);
        if (invalid == "cutoffBeforeDelivery") cutoff = new GameTime(new GameTime(0, 600).TotalMilliseconds - 1);
        if (invalid == "otherDayCutoff") cutoff = new GameTime(1, 630);
        if (invalid == "endWithCutoff") { window = null; cutoff = new GameTime(0, 630); }
        Assert.Catch<ArgumentException>(() => new DayRequestPlan(
            invalid == "blankInstance" ? " " : "R1", request, profile,
            new GameTime(0, 600), window, cutoff));
    }

    [TestCase(-1, true)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    public void FixedCutoff_CannotExceedWindowStartEvenByOneMillisecond(int offset, bool valid)
    {
        var start = new GameTime(0, 660);
        var cutoff = new GameTime(start.TotalMilliseconds + offset);
        if (valid)
        {
            var plan = new DayRequestPlan("R1", Content(), Profile(), new GameTime(0, 600),
                new TimeRange(0, 660, 720), cutoff);
            Assert.That(plan.CutoffAt.Value.TotalMilliseconds, Is.EqualTo(cutoff.TotalMilliseconds));
        }
        else
            Assert.Throws<ArgumentException>(() => new DayRequestPlan("R1", Content(), Profile(),
                new GameTime(0, 600), new TimeRange(0, 660, 720), cutoff));
    }

    [Test]
    public void FixedCutoff_DefaultsToWindowStartAndMayEqualDelivery()
    {
        var start = new GameTime(0, 660);
        var plan = new DayRequestPlan("R1", Content(), Profile(), start, new TimeRange(0, 660, 720));
        Assert.That(plan.CutoffAt.Value.TotalMilliseconds, Is.EqualTo(start.TotalMilliseconds));
        Assert.DoesNotThrow(() => Day(new List<DayRequestPlan> { plan }));
    }

    [TestCase(539, 59, 999, false)]
    [TestCase(540, 0, 0, true)]
    [TestCase(1019, 59, 999, true)]
    [TestCase(1020, 0, 0, false)]
    public void Delivery_UsesPreciseHalfOpenWorkdayBounds(int minute, int second, int millisecond, bool valid)
    {
        var plan = EndOfShift(new GameTime(0, minute, second, millisecond));
        if (valid)
        {
            var day = Day(new List<DayRequestPlan> { plan });
            Assert.That(day.Requests[0].IsFixedWindow, Is.False);
            Assert.That(day.Requests[0].CutoffAt.HasValue, Is.False);
        }
        else
            Assert.Throws<ArgumentException>(() => Day(new List<DayRequestPlan> { plan }));
    }

    [TestCase("nullPlan")]
    [TestCase("duplicateInstance")]
    [TestCase("unknownRequester")]
    [TestCase("missingRosterMember")]
    [TestCase("extraRosterMember")]
    [TestCase("caseMismatch")]
    [TestCase("otherDay")]
    [TestCase("windowAfterClosing")]
    [TestCase("cutoffAtClosing")]
    public void DayBoundary_RejectsInvalidRequestMembershipOrShift(string invalid)
    {
        var profile = Profile();
        if (invalid == "missingRosterMember") profile = Profile(new List<string>());
        if (invalid == "extraRosterMember") profile = Profile(new List<string> { "alice", "outsider" });
        if (invalid == "caseMismatch") profile = Profile(new List<string> { "Alice" });
        var delivery = new GameTime(invalid == "otherDay" ? 1 : 0, 600);
        TimeRange? window = null;
        if (invalid == "windowAfterClosing") window = new TimeRange(0, 1000, 1021);
        if (invalid == "cutoffAtClosing") window = new TimeRange(0, 1020, 1030);
        var plan = new DayRequestPlan("R1", Content(invalid == "unknownRequester" ? "outsider" : "alice"),
            profile, delivery, window);
        var requests = new List<DayRequestPlan> { invalid == "nullPlan" ? null : plan };
        if (invalid == "duplicateInstance") requests.Add(plan);
        Assert.Throws<ArgumentException>(() => Day(requests));
    }

    [Test]
    public void WindowEndingAtClosing_IsValidWithEarlierCutoff()
    {
        var plan = new DayRequestPlan("R1", Content(), Profile(), new GameTime(0, 900),
            new TimeRange(0, 1000, 1020));
        Assert.DoesNotThrow(() => Day(new List<DayRequestPlan> { plan }));
    }

    [Test]
    public void DailyRequests_AreReadOnlyCopiesAndRepeatedTemplatesKeepDistinctInstances()
    {
        var requests = new List<DayRequestPlan>
        {
            new DayRequestPlan("R1", Content(), Profile(), new GameTime(0, 600)),
            new DayRequestPlan("R2", Content(), Profile(), new GameTime(0, 700))
        };
        var day = Day(requests);
        requests.Clear();
        Assert.That(day.Requests.Count, Is.EqualTo(2));
        Assert.That(day.Requests[0].InstanceId, Is.EqualTo("R1"));
        Assert.That(day.Requests[1].InstanceId, Is.EqualTo("R2"));
        Assert.That(day.Requests[0].Request.RequestId, Is.EqualTo(day.Requests[1].Request.RequestId));
        Assert.Throws<NotSupportedException>(() => ((IList<DayRequestPlan>)day.Requests).Clear());
        Assert.That(day.ScheduleData.HrKnowledge.KnownClaims, Is.Empty);
    }

    [Test]
    public void LegacyNullAndEmptyRequestLists_StillConstructSessions()
    {
        var schedule = Day().ScheduleData;
        var legacy = new DayData(schedule, new List<EvidenceDefinition>(), new List<DayIncidentPlan>());
        foreach (var day in new[] { legacy, Day(null), Day(new List<DayRequestPlan>()) })
        {
            Assert.That(day.Requests, Is.Empty);
            Assert.DoesNotThrow(() => new WorkdaySession(day));
        }
    }

    [TestCase("fixedDefault")]
    [TestCase("fixedEarlier")]
    [TestCase("endOfShift")]
    public void AuthoredConversion_UsesOwningDayAndSelectedTimingMode(string mode)
    {
        DayRequestEntry entry;
        RequestDefinition request;
        var day = AuthoredDay(out entry, out request);
        if (mode == "fixedEarlier")
        {
            SetField(entry, "useEarlierCutoff", true);
            SetField(entry, "cutoffMinute", 630);
        }
        if (mode == "endOfShift")
        {
            SetField(entry, "hasFixedWindow", false);
            SetField(entry, "helpStartMinute", -1);
            SetField(entry, "helpEndMinute", -1);
            SetField(entry, "useEarlierCutoff", true);
            SetField(entry, "cutoffMinute", -1);
        }
        var data = day.GetData();
        var plan = data.Requests[0];
        Assert.That(plan.InstanceId, Is.EqualTo("D3R1"));
        Assert.That(plan.DeliveryAt.DayIndex, Is.EqualTo(3));
        Assert.That(plan.Request.RequesterId, Is.EqualTo("alice"));
        Assert.That(plan.Request.AllowSelfHelp, Is.False);
        Assert.That(plan.IsFixedWindow, Is.EqualTo(mode != "endOfShift"));
        if (plan.IsFixedWindow)
        {
            Assert.That(plan.HelpWindow.Value.DayIndex, Is.EqualTo(3));
            Assert.That(plan.HelpWindow.Value.EndMinute, Is.EqualTo(720));
            Assert.That(plan.CutoffAt.Value.MinuteOfDay, Is.EqualTo(mode == "fixedEarlier" ? 630 : 660));
            Assert.That(plan.CutoffAt.Value.DayIndex, Is.EqualTo(3));
        }
        else
            Assert.That(plan.CutoffAt.HasValue, Is.False);
    }

    [TestCase("request")]
    [TestCase("profile")]
    [TestCase("requester")]
    [TestCase("nullCollection")]
    [TestCase("nullEntry")]
    public void AuthoredMissingInputs_AreRejected(string invalid)
    {
        DayRequestEntry entry;
        RequestDefinition request;
        var day = AuthoredDay(out entry, out request);
        if (invalid == "request" || invalid == "profile") SetField(entry, invalid, null);
        if (invalid == "requester") SetField(request, "requester", null);
        if (invalid == "nullCollection") SetField(day, "requests", null);
        if (invalid == "nullEntry") SetField(day, "requests", new List<DayRequestEntry> { null });
        Assert.Throws<InvalidOperationException>(() => day.GetData());
    }

    [Test]
    public void AuthoredRequestSnapshot_SurvivesEditsAndSourceDestruction()
    {
        DayRequestEntry entry;
        RequestDefinition request;
        var day = AuthoredDay(out entry, out request);
        var original = day.GetData();
        SetField(entry, "instanceId", "D3R2");
        SetField(entry, "deliveryMinute", 610);
        SetField(request, "description", "Changed request.");
        SetField(request, "allowSelfHelp", true);
        var updated = day.GetData().Requests[0];
        Assert.That(updated.InstanceId, Is.EqualTo("D3R2"));
        Assert.That(updated.Request.Description, Is.EqualTo("Changed request."));
        Assert.That(updated.Request.AllowSelfHelp, Is.True);
        SetField(day, "requests", new List<DayRequestEntry>());
        Assert.That(day.GetData().Requests, Is.Empty);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        var saved = original.Requests[0];
        Assert.That(saved.InstanceId, Is.EqualTo("D3R1"));
        Assert.That(saved.DeliveryAt.MinuteOfDay, Is.EqualTo(600));
        Assert.That(saved.Request.Description, Is.EqualTo("Arrange desktop troubleshooting."));
        Assert.That(saved.Request.AllowSelfHelp, Is.False);
        Assert.That(saved.Profile.RankedEmployees[0].EmployeeId, Is.EqualTo("dave"));
        Assert.DoesNotThrow(() => new WorkdaySession(original));
    }

    [Test]
    public void PublicRequestContent_ExposesOnlyReadOnlySafeFields()
    {
        var names = new List<string>();
        foreach (var property in typeof(RequestData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            names.Add(property.Name);
            Assert.That(property.GetSetMethod(true), Is.Null);
            Type expected = property.Name == "Category" ? typeof(EmployeeCapability) :
                property.Name == "AllowSelfHelp" ? typeof(bool) : typeof(string);
            Assert.That(property.PropertyType, Is.EqualTo(expected));
        }
        CollectionAssert.AreEquivalent(new[]
        {
            "RequestId", "DisplayName", "Description", "RequesterId", "Category", "AllowSelfHelp"
        }, names);
        Assert.That(typeof(RequestData).GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
    }
}
