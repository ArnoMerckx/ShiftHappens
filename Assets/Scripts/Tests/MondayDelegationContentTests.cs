using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class MondayDelegationContentTests
{
    private DayData day;

    [SetUp]
    public void SetUp()
    {
        var definition = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
        Assert.That(definition, Is.Not.Null);
        day = definition.GetData();
    }

    private DayRequestPlan Request(string instanceId)
    {
        foreach (var plan in day.Requests)
            if (plan.InstanceId == instanceId) return plan;
        Assert.Fail("Missing Monday request: " + instanceId);
        return null;
    }

    [Test]
    public void Monday_ComposesBothRequestModesWithAgreedTimingAndSelfHelpPolicy()
    {
        Assert.That(day.Requests.Count, Is.EqualTo(2));
        var pc = Request("D0R1");
        Assert.That(pc.Request.RequestId, Is.EqualTo("alice_pc_help"));
        Assert.That(pc.Request.RequesterId, Is.EqualTo("alice"));
        Assert.That(pc.Request.Category, Is.EqualTo(EmployeeCapability.ComputerSupport));
        Assert.That(pc.Request.AllowSelfHelp, Is.False);
        Assert.That(pc.DeliveryAt.TotalMilliseconds, Is.EqualTo(new GameTime(0, 595).TotalMilliseconds));
        Assert.That(pc.IsFixedWindow, Is.True);
        Assert.That(pc.HelpWindow.Value.DayIndex, Is.Zero);
        Assert.That(pc.HelpWindow.Value.StartMinute, Is.EqualTo(630));
        Assert.That(pc.HelpWindow.Value.EndMinute, Is.EqualTo(645));
        Assert.That(pc.CutoffAt.Value.TotalMilliseconds, Is.EqualTo(new GameTime(0, 630).TotalMilliseconds));

        var boxes = Request("D0R2");
        Assert.That(boxes.Request.RequestId, Is.EqualTo("karen_archive_boxes"));
        Assert.That(boxes.Request.RequesterId, Is.EqualTo("karen"));
        Assert.That(boxes.Request.Category, Is.EqualTo(EmployeeCapability.EquipmentHandling));
        Assert.That(boxes.Request.AllowSelfHelp, Is.False);
        Assert.That(boxes.DeliveryAt.TotalMilliseconds, Is.EqualTo(new GameTime(0, 860).TotalMilliseconds));
        Assert.That(boxes.IsFixedWindow, Is.False);
        Assert.That(boxes.HelpWindow.HasValue, Is.False);
        Assert.That(boxes.CutoffAt.HasValue, Is.False);
        Assert.That(day.ScheduleData.Settings.ClosesAt.TotalMilliseconds,
            Is.EqualTo(new GameTime(0, 1020).TotalMilliseconds));
    }

    [TestCase("D0R1", "desktop_troubleshooting", "dave", "steve",
        new[] { "alice", "bob", "karen" })]
    [TestCase("D0R2", "archive_box_handling", "bob", "steve",
        new[] { "alice", "dave", "karen" })]
    public void Profiles_PreserveHiddenTiersAndClassifyExactlyTheRoster(
        string instanceId, string profileId, string strongest, string fallback, string[] unsuitable)
    {
        var plan = Request(instanceId);
        var profile = plan.Profile;
        Assert.That(profile.ProfileId, Is.EqualTo(profileId));
        Assert.That(profile.RequiredCapability, Is.EqualTo(plan.Request.Category));
        Assert.That(profile.RankedEmployees.Count, Is.EqualTo(2));
        Assert.That(profile.RankedEmployees[0].EmployeeId, Is.EqualTo(strongest));
        Assert.That(profile.RankedEmployees[0].Tier, Is.Zero);
        Assert.That(profile.RankedEmployees[1].EmployeeId, Is.EqualTo(fallback));
        Assert.That(profile.RankedEmployees[1].Tier, Is.EqualTo(1));
        CollectionAssert.AreEquivalent(unsuitable, profile.UnsuitableEmployeeIds);
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in profile.RankedEmployees)
            Assert.That(covered.Add(entry.EmployeeId), Is.True);
        foreach (string employeeId in profile.UnsuitableEmployeeIds)
            Assert.That(covered.Add(employeeId), Is.True);
        CollectionAssert.AreEquivalent(day.ScheduleData.EmployeeIds, covered);
    }

    [Test]
    public void OrdinaryCoverage_ExplicitlyIncludesEachEmployeeForTheShift()
    {
        Assert.That(day.ScheduleData.OrdinaryAvailability.Count, Is.EqualTo(5));
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in day.ScheduleData.OrdinaryAvailability)
        {
            Assert.That(covered.Add(entry.EmployeeId), Is.True);
            Assert.That(entry.Period.DayIndex, Is.Zero);
            Assert.That(entry.Period.StartMinute, Is.EqualTo(540));
            Assert.That(entry.Period.EndMinute, Is.EqualTo(1020));
        }
        CollectionAssert.AreEquivalent(day.ScheduleData.EmployeeIds, covered);
    }

    [TestCase("dave", "inspection")]
    [TestCase("bob", "dentist")]
    public void PcWindow_OverlapsTheExistingApprovedBlockingCommitment(string employeeId, string recordId)
    {
        TimeRange window = Request("D0R1").HelpWindow.Value;
        var segments = ScheduleResolver.ResolveSegments(employeeId, window,
            new List<ApprovedScheduleRecord>(day.ScheduleData.WorldApprovedRecords));
        Assert.That(segments.Count, Is.EqualTo(1));
        Assert.That(segments[0].Period.StartMinute, Is.EqualTo(window.StartMinute));
        Assert.That(segments[0].Period.EndMinute, Is.EqualTo(window.EndMinute));
        Assert.That(segments[0].WinningRecords.Count, Is.EqualTo(1));
        Assert.That(segments[0].WinningRecords[0].RecordId, Is.EqualTo(recordId));
        Assert.That(segments[0].WinningRecords[0].BlocksDelegation, Is.True);
    }

    [Test]
    public void StevesPcWindow_HasAuthoredCoverageAndNoApprovedCommitment()
    {
        TimeRange window = Request("D0R1").HelpWindow.Value;
        EmployeeAvailability coverage = null;
        foreach (var entry in day.ScheduleData.OrdinaryAvailability)
            if (entry.EmployeeId == "steve") coverage = entry;
        Assert.That(coverage, Is.Not.Null);
        Assert.That(coverage.Period.StartMinute, Is.LessThanOrEqualTo(window.StartMinute));
        Assert.That(coverage.Period.EndMinute, Is.GreaterThanOrEqualTo(window.EndMinute));
        var segments = ScheduleResolver.ResolveSegments("steve", window,
            new List<ApprovedScheduleRecord>(day.ScheduleData.WorldApprovedRecords));
        Assert.That(segments.Count, Is.EqualTo(1));
        Assert.That(segments[0].WinningRecords, Is.Empty);
        // These are input facts; the U10 availability query/evaluator is not implemented here.
    }
}
