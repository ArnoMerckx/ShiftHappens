using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DelegationAvailabilityInputTests
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

    private static DayScheduleData Schedule(
        List<EmployeeAvailability> availability = null,
        List<ApprovedScheduleRecord> records = null)
    {
        return new DayScheduleData(
            new WorkdaySettings("availability_day", "Availability day", 0, 540, 1020, 40),
            new List<string> { "bob", "alice" },
            records ?? new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>(),
            ordinaryAvailability: availability);
    }

    private static ApprovedScheduleRecord Record(
        string id, bool blocks, int priority = 100,
        int start = 600, int end = 660, string employeeId = "bob")
    {
        return new ApprovedScheduleRecord(id, employeeId, "office",
            new TimeRange(0, start, end), priority, false, blocks);
    }

    private EmployeeDefinition Employee(string id)
    {
        var employee = Own<EmployeeDefinition>();
        SetField(employee, "employeeId", id);
        SetField(employee, "displayName", id);
        return employee;
    }

    private DayDefinition Day(EmployeeDefinition employee, List<DayAvailabilityEntry> entries)
    {
        var day = Own<DayDefinition>();
        SetField(day, "dayId", "authored_availability");
        SetField(day, "displayName", "Authored availability");
        SetField(day, "dayIndex", 3);
        SetField(day, "employees", new List<EmployeeDefinition> { employee });
        SetField(day, "ordinaryAvailability", entries);
        return day;
    }

    private static DayAvailabilityEntry Entry(EmployeeDefinition employee, int start = 540, int end = 1020)
    {
        var entry = new DayAvailabilityEntry();
        SetField(entry, "employee", employee);
        SetField(entry, "startMinute", start);
        SetField(entry, "endMinute", end);
        return entry;
    }

    [Test]
    public void OmittedNullAndEmptyCoverage_RemainEmptyWithoutAddingHrKnowledge()
    {
        var legacy = new DayScheduleData(
            new WorkdaySettings("legacy", "Legacy", 0, 540, 1020, 40),
            new List<string> { "bob" }, new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>());
        foreach (var schedule in new[] { legacy, Schedule(null), Schedule(new List<EmployeeAvailability>()) })
        {
            Assert.That(schedule.OrdinaryAvailability, Is.Empty);
            Assert.That(schedule.WorldApprovedRecords, Is.Empty);
            Assert.That(schedule.HrKnowledge.ApprovedRecords, Is.Empty);
            Assert.That(schedule.HrKnowledge.KnownClaims, Is.Empty);
        }
    }

    [TestCase("nullEntry")]
    [TestCase("unknownEmployee")]
    [TestCase("wrongCase")]
    [TestCase("wrongDay")]
    [TestCase("beforeOpening")]
    [TestCase("afterClosing")]
    public void DailyCoverage_RejectsInvalidContext(string invalid)
    {
        EmployeeAvailability entry = null;
        if (invalid != "nullEntry")
        {
            string id = invalid == "unknownEmployee" ? "outsider" : invalid == "wrongCase" ? "Bob" : "bob";
            int day = invalid == "wrongDay" ? 1 : 0;
            int start = invalid == "beforeOpening" ? 539 : 540;
            int end = invalid == "afterClosing" ? 1021 : 1020;
            entry = new EmployeeAvailability(id, new TimeRange(day, start, end));
        }
        Assert.Throws<ArgumentException>(() => Schedule(new List<EmployeeAvailability> { entry }));
    }

    [Test]
    public void DefaultPeriod_CannotBecomePositiveCoverage()
    {
        Assert.Throws<ArgumentException>(() => new EmployeeAvailability("bob", default(TimeRange)));
    }

    [Test]
    public void PositiveIntervals_AreCopiedReadOnlyWithoutFillingGapsOrChangingHrPresence()
    {
        var entries = new List<EmployeeAvailability>
        {
            new EmployeeAvailability("bob", new TimeRange(0, 540, 600)),
            new EmployeeAvailability("bob", new TimeRange(0, 580, 620)),
            new EmployeeAvailability("bob", new TimeRange(0, 620, 660)),
            new EmployeeAvailability("bob", new TimeRange(0, 900, 1020))
        };
        var schedule = Schedule(entries);
        entries.Clear();
        Assert.That(schedule.OrdinaryAvailability.Count, Is.EqualTo(4));
        Assert.That(schedule.OrdinaryAvailability[2].Period.EndMinute, Is.EqualTo(660));
        Assert.That(schedule.OrdinaryAvailability[3].Period.StartMinute, Is.EqualTo(900));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EmployeeAvailability>)schedule.OrdinaryAvailability).Clear());
        Assert.That(ScheduleResolver.GetPresence("bob", "office", new TimeRange(0, 540, 600),
            new List<ApprovedScheduleRecord>(schedule.WorldApprovedRecords)), Is.EqualTo(PresenceStatus.Unknown));
        Assert.That(schedule.HrKnowledge.ApprovedRecords, Is.Empty);
    }

    [Test]
    public void AuthoredCoverage_UsesOwningDayAndFreezesConvertedValues()
    {
        var employee = Employee("bob");
        var entry = Entry(employee);
        var entries = new List<DayAvailabilityEntry> { entry };
        var day = Day(employee, entries);
        var original = day.GetData().ScheduleData;
        SetField(entry, "endMinute", 900);
        Assert.That(day.GetData().ScheduleData.OrdinaryAvailability[0].Period.EndMinute, Is.EqualTo(900));
        entries.Clear();
        UnityEngine.Object.DestroyImmediate(day);
        UnityEngine.Object.DestroyImmediate(employee);
        var saved = original.OrdinaryAvailability[0];
        Assert.That(saved.EmployeeId, Is.EqualTo("bob"));
        Assert.That(saved.Period.DayIndex, Is.EqualTo(3));
        Assert.That(saved.Period.StartMinute, Is.EqualTo(540));
        Assert.That(saved.Period.EndMinute, Is.EqualTo(1020));
        Assert.That(original.HrKnowledge.KnownClaims, Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AuthoredNullCollectionOrEntry_IsRejected(bool nullCollection)
    {
        var day = Day(Employee("bob"), nullCollection ? null : new List<DayAvailabilityEntry> { null });
        Assert.Throws<InvalidOperationException>(() => day.GetData());
    }

    [Test]
    public void AuthoredMissingEmployee_IsRejectedBeforeConversion()
    {
        Assert.Throws<InvalidOperationException>(() => Entry(null).ToAvailability(3));
    }

    [TestCase(600, 600)]
    [TestCase(660, 600)]
    public void AuthoredEmptyOrReversedPeriod_IsRejected(int start, int end)
    {
        var day = Day(Employee("bob"), new List<DayAvailabilityEntry>());
        var entry = Entry(Employee("bob"), start, end);
        SetField(day, "ordinaryAvailability", new List<DayAvailabilityEntry> { entry });
        Assert.Throws<ArgumentException>(() => day.GetData());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LegacyApprovedRecord_DoesNotInferBlockingFromPresence(bool requiresPresence)
    {
        var record = new ApprovedScheduleRecord("legacy", "bob", "office",
            new TimeRange(0, 600, 660), 100, requiresPresence);
        Assert.That(record.BlocksDelegation, Is.False);
    }

    [Test]
    public void AuthoredEmployeeOutsideRoster_IsRejectedByDayBoundary()
    {
        var day = Day(Employee("bob"), new List<DayAvailabilityEntry> { Entry(Employee("outsider")) });
        Assert.Throws<ArgumentException>(() => day.GetData());
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void ApprovedFlags_ConvertIndependentlyAndHrPresenceIgnoresDelegation(bool requiresPresence, bool blocks)
    {
        var location = Own<LocationDefinition>();
        SetField(location, "locationId", "office");
        var definition = Own<ApprovedScheduleRecordDefinition>();
        SetField(definition, "recordId", "commitment");
        SetField(definition, "employee", Employee("bob"));
        SetField(definition, "location", location);
        SetField(definition, "startMinute", 600);
        SetField(definition, "endMinute", 660);
        SetField(definition, "priority", 100);
        SetField(definition, "requiresPresence", requiresPresence);
        SetField(definition, "blocksDelegation", blocks);
        var record = definition.ToRecord();
        Assert.That(record.BlocksDelegation, Is.EqualTo(blocks));
        Assert.That(record.RequiresPresence, Is.EqualTo(requiresPresence));
        var schedule = Schedule(records: new List<ApprovedScheduleRecord> { record });
        Assert.That(schedule.WorldApprovedRecords[0], Is.SameAs(record));
        Assert.That(schedule.HrKnowledge.ApprovedRecords, Is.Empty);
        Assert.That(ScheduleResolver.GetPresence("bob", "elsewhere", record.Period,
            new List<ApprovedScheduleRecord> { record }),
            Is.EqualTo(requiresPresence ? PresenceStatus.Impossible : PresenceStatus.Unknown));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TiedWinningDisagreement_IsRejectedRegardlessOfInsertionOrder(bool reverse)
    {
        var records = new List<ApprovedScheduleRecord> { Record("blocks", true), Record("allows", false) };
        if (reverse) records.Reverse();
        var error = Assert.Throws<InvalidOperationException>(() => Schedule(records: records));
        Assert.That(error.Message, Does.Contain("Conflicting delegation commitments"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TiedWinningAgreement_IsValid(bool blocks)
    {
        Assert.DoesNotThrow(() => Schedule(records: new List<ApprovedScheduleRecord>
        {
            Record("first", blocks), Record("second", blocks)
        }));
    }

    [Test]
    public void AdjacentAndDifferentEmployeeCommitments_DoNotConflict()
    {
        Assert.DoesNotThrow(() => Schedule(records: new List<ApprovedScheduleRecord>
        {
            Record("first", true), Record("adjacent", false, start: 660, end: 720),
            Record("alice", false, employeeId: "alice")
        }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HighestPriorityDeterminesWhichFlagParticipates(bool winningFlag)
    {
        var winner = Record("winner", winningFlag, priority: 200);
        var records = new List<ApprovedScheduleRecord> { Record("lower", !winningFlag), winner };
        var schedule = Schedule(records: records);
        var segments = ScheduleResolver.ResolveSegments("bob", new TimeRange(0, 600, 660),
            new List<ApprovedScheduleRecord>(schedule.WorldApprovedRecords));
        Assert.That(segments.Count, Is.EqualTo(1));
        Assert.That(segments[0].WinningRecords.Count, Is.EqualTo(1));
        Assert.That(segments[0].WinningRecords[0], Is.SameAs(winner));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void HigherOverride_HidesTieOnlyForItsCoveredSegments(bool fullCoverage)
    {
        var records = new List<ApprovedScheduleRecord>
        {
            Record("blocks", true), Record("allows", false),
            Record("override", false, priority: 200, end: fullCoverage ? 660 : 630)
        };
        if (fullCoverage)
            Assert.DoesNotThrow(() => Schedule(records: records));
        else
            Assert.Throws<InvalidOperationException>(() => Schedule(records: records));
    }
}
