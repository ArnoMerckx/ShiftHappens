using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EmployeeRosterTests
{
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

    private T Own<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
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
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing serialized field: " + name);
        field.SetValue(target, value);
    }

    private static WorkdaySettings Settings()
    {
        return new WorkdaySettings("roster_day", "Roster day", 0, 540, 1020, 20);
    }

    private static DayScheduleData Schedule(List<string> ids, List<EmployeeViewData> rows)
    {
        return new DayScheduleData(Settings(), ids, new List<ApprovedScheduleRecord>(),
            new List<ApprovedScheduleRecord>(), new List<Claim>(), rows);
    }

    private EmployeeDefinition Employee(string id, string name)
    {
        var employee = Own<EmployeeDefinition>();
        SetField(employee, "employeeId", id);
        SetField(employee, "displayName", name);
        return employee;
    }

    private DayDefinition AuthoredDay(params EmployeeDefinition[] employees)
    {
        var day = Own<DayDefinition>();
        SetField(day, "dayId", "roster_day");
        SetField(day, "displayName", "Roster day");
        SetField(day, "employees", new List<EmployeeDefinition>(employees));
        return day;
    }

    private static List<string> Ids(IReadOnlyList<EmployeeViewData> rows)
    {
        var result = new List<string>();
        foreach (var row in rows) result.Add(row.EmployeeId);
        return result;
    }

    [TestCase(null, "Steve", "employeeId")]
    [TestCase("", "Steve", "employeeId")]
    [TestCase(" \t", "Steve", "employeeId")]
    [TestCase("steve", null, "displayName")]
    [TestCase("steve", "", "displayName")]
    [TestCase("steve", " \t", "displayName")]
    public void BlankIdentityOrName_IsRejected(string id, string name, string parameter)
    {
        var error = Assert.Throws<ArgumentException>(() => new EmployeeViewData(id, name));
        Assert.That(error.ParamName, Is.EqualTo(parameter));
    }

    [Test]
    public void Metadata_PreservesAuthoredStringsWithoutNormalizingIdentity()
    {
        var row = new EmployeeViewData("Steve_ID", " Steve Name ");
        Assert.That(row.EmployeeId, Is.EqualTo("Steve_ID"));
        Assert.That(row.DisplayName, Is.EqualTo(" Steve Name "));
    }

    [Test]
    public void PublicMetadataApi_ContainsOnlyReadOnlyProfileFields()
    {
        var type = typeof(EmployeeViewData);
        var names = new List<string>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            names.Add(property.Name);
            Assert.That(property.PropertyType, Is.EqualTo(
                property.Name == "Role" ? typeof(EmployeeRole) : typeof(string)));
            Assert.That(property.GetSetMethod(true), Is.Null);
        }
        CollectionAssert.AreEquivalent(new[]
        {
            "EmployeeId", "DisplayName", "Role", "Bio", "InterestsAndExperience"
        }, names);
        Assert.That(type.GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
        Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(type), Is.False);
    }

    [Test]
    public void SuppliedMetadata_FollowsCanonicalEmployeeOrder()
    {
        var schedule = Schedule(new List<string> { "steve", "karen" },
            new List<EmployeeViewData> { new EmployeeViewData("karen", "Karen"),
                new EmployeeViewData("steve", "Steve") });
        CollectionAssert.AreEqual(new[] { "steve", "karen" }, Ids(schedule.EmployeeRoster));
        Assert.That(schedule.EmployeeRoster[0].DisplayName, Is.EqualTo("Steve"));
        Assert.That(schedule.EmployeeRoster[1].DisplayName, Is.EqualTo("Karen"));
        CollectionAssert.AreEqual(new[] { "steve", "karen" }, schedule.EmployeeIds);
    }

    [Test]
    public void ExistingFiveArgumentConstruction_UsesIdsAsLabels()
    {
        var schedule = new DayScheduleData(Settings(), new List<string> { "steve", "karen" },
            new List<ApprovedScheduleRecord>(), new List<ApprovedScheduleRecord>(), new List<Claim>());
        CollectionAssert.AreEqual(new[] { "steve", "karen" }, Ids(schedule.EmployeeRoster));
        Assert.That(schedule.EmployeeRoster[0].DisplayName, Is.EqualTo("steve"));
        Assert.That(schedule.EmployeeRoster[1].DisplayName, Is.EqualTo("karen"));
    }

    [Test]
    public void ExplicitNullMetadata_UsesSameLegacyIdLabels()
    {
        var schedule = Schedule(new List<string> { "steve" }, null);
        Assert.That(schedule.EmployeeRoster.Count, Is.EqualTo(1));
        Assert.That(schedule.EmployeeRoster[0].DisplayName, Is.EqualTo("steve"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyEmployeeDay_AcceptsOmittedOrExplicitEmptyRoster(bool supplied)
    {
        var schedule = Schedule(new List<string>(), supplied ? new List<EmployeeViewData>() : null);
        Assert.That(schedule.EmployeeIds, Is.Empty);
        Assert.That(schedule.EmployeeRoster, Is.Empty);
    }

    [TestCase("missing")]
    [TestCase("extra")]
    [TestCase("outside")]
    [TestCase("wrongCase")]
    [TestCase("duplicate")]
    [TestCase("nullRow")]
    public void MetadataMembership_MustMatchDayIdsExactly(string invalid)
    {
        var rows = new List<EmployeeViewData>
        {
            new EmployeeViewData("steve", "Steve"), new EmployeeViewData("karen", "Karen")
        };
        switch (invalid)
        {
            case "missing": rows.RemoveAt(1); break;
            case "extra": rows.Add(new EmployeeViewData("outside", "Outside")); break;
            case "outside": rows[1] = new EmployeeViewData("outside", "Outside"); break;
            case "wrongCase": rows[1] = new EmployeeViewData("Karen", "Karen"); break;
            case "duplicate": rows[1] = new EmployeeViewData("steve", "Another Steve"); break;
            case "nullRow": rows[1] = null; break;
        }
        var error = Assert.Throws<ArgumentException>(() =>
            Schedule(new List<string> { "steve", "karen" }, rows));
        Assert.That(error.ParamName, Is.EqualTo("employeePresentation"));
    }

    [Test]
    public void DuplicateDisplayNames_AreAllowedForDistinctAuthorizedIds()
    {
        var schedule = Schedule(new List<string> { "one", "two" }, new List<EmployeeViewData>
        {
            new EmployeeViewData("one", "Alex"), new EmployeeViewData("two", "Alex")
        });
        Assert.That(schedule.EmployeeRoster.Count, Is.EqualTo(2));
        CollectionAssert.AreEqual(new[] { "one", "two" }, Ids(schedule.EmployeeRoster));
    }

    [Test]
    public void OrdinalCaseDistinctIds_RemainSeparateWhenBothBelongToDay()
    {
        var schedule = Schedule(new List<string> { "steve", "Steve" }, new List<EmployeeViewData>
        {
            new EmployeeViewData("Steve", "Second"), new EmployeeViewData("steve", "First")
        });
        CollectionAssert.AreEqual(new[] { "steve", "Steve" }, Ids(schedule.EmployeeRoster));
        Assert.That(schedule.EmployeeRoster[0].DisplayName, Is.EqualTo("First"));
        Assert.That(schedule.EmployeeRoster[1].DisplayName, Is.EqualTo("Second"));
    }

    [Test]
    public void MutatingInputLists_DoesNotRewriteRosterOrAuthorizedIds()
    {
        var ids = new List<string> { "steve" };
        var rows = new List<EmployeeViewData> { new EmployeeViewData("steve", "Steve") };
        var schedule = Schedule(ids, rows);
        ids.Clear();
        rows[0] = new EmployeeViewData("outside", "Outside");
        rows.Clear();
        CollectionAssert.AreEqual(new[] { "steve" }, schedule.EmployeeIds);
        CollectionAssert.AreEqual(new[] { "steve" }, Ids(schedule.EmployeeRoster));
        Assert.That(schedule.EmployeeRoster[0].DisplayName, Is.EqualTo("Steve"));
    }

    [Test]
    public void ScheduleRoster_CannotBeModifiedThroughCollectionInterface()
    {
        var schedule = Schedule(new List<string> { "steve" },
            new List<EmployeeViewData> { new EmployeeViewData("steve", "Steve") });
        var list = schedule.EmployeeRoster as IList<EmployeeViewData>;
        Assert.That(list, Is.Not.Null);
        Assert.That(list.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.That(schedule.EmployeeRoster.Count, Is.EqualTo(1));
    }

    [Test]
    public void AuthoredDay_CopiesEmployeeNamesIntoSessionInDayOrder()
    {
        var day = AuthoredDay(Employee("karen", "Karen"), Employee("steve", "Steve"));
        DayData data = day.GetData();
        var session = new WorkdaySession(data);
        var rows = session.GetEmployeeRoster();
        CollectionAssert.AreEqual(new[] { "karen", "steve" }, Ids(rows));
        Assert.That(rows[0].DisplayName, Is.EqualTo("Karen"));
        Assert.That(rows[1].DisplayName, Is.EqualTo("Steve"));
        CollectionAssert.AreEqual(data.ScheduleData.EmployeeIds, Ids(rows));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t")]
    public void MissingAuthoredName_IsRejectedByDayComposition(string name)
    {
        var day = AuthoredDay(Employee("steve", name));
        Assert.Throws<ArgumentException>(() => day.GetData());
    }

    [Test]
    public void ComposedRoster_DoesNotFollowLaterAuthoredNameChanges()
    {
        var employee = Employee("steve", "Original name");
        var day = AuthoredDay(employee);
        DayData original = day.GetData();
        var session = new WorkdaySession(original);
        SetField(employee, "displayName", "Changed name");
        Assert.That(original.ScheduleData.EmployeeRoster[0].DisplayName, Is.EqualTo("Original name"));
        Assert.That(session.GetEmployeeRoster()[0].DisplayName, Is.EqualTo("Original name"));
        Assert.That(day.GetData().ScheduleData.EmployeeRoster[0].DisplayName, Is.EqualTo("Changed name"));
    }

    [Test]
    public void SessionRoster_SurvivesDestructionOfAuthoredDayAndEmployeeAssets()
    {
        var employee = Employee("steve", "Steve");
        var day = AuthoredDay(employee);
        var session = new WorkdaySession(day.GetData());
        UnityEngine.Object.DestroyImmediate(day);
        UnityEngine.Object.DestroyImmediate(employee);
        var row = session.GetEmployeeRoster()[0];
        Assert.That(row.EmployeeId, Is.EqualTo("steve"));
        Assert.That(row.DisplayName, Is.EqualTo("Steve"));
    }

    [Test]
    public void SessionQuery_ReturnsIndependentListsWithoutChangingMembershipOrClock()
    {
        var day = AuthoredDay(Employee("steve", "Steve"));
        DayData data = day.GetData();
        var session = new WorkdaySession(data);
        long before = session.Now.TotalMilliseconds;
        var first = session.GetEmployeeRoster();
        var second = session.GetEmployeeRoster();
        Assert.That(second, Is.Not.SameAs(first));
        first.Clear();
        second.Add(new EmployeeViewData("outside", "Outside"));
        CollectionAssert.AreEqual(new[] { "steve" }, Ids(session.GetEmployeeRoster()));
        CollectionAssert.AreEqual(new[] { "steve" }, data.ScheduleData.EmployeeIds);
        Assert.That(session.Now.TotalMilliseconds, Is.EqualTo(before));
    }

    [Test]
    public void RosterQuery_DoesNotAlterWorldHrKnowledgeOrEvidenceDiscovery()
    {
        var world = new ApprovedScheduleRecord("world", "steve", "office",
            new TimeRange(0, 540, 600), 100, true);
        var claim = new WhereaboutsClaim("known", "steve", "office", new TimeRange(0, 540, 600), true);
        var schedule = new DayScheduleData(Settings(), new List<string> { "steve" },
            new List<ApprovedScheduleRecord> { world }, new List<ApprovedScheduleRecord> { world },
            new List<Claim> { claim }, new List<EmployeeViewData> { new EmployeeViewData("steve", "Steve") });
        var document = Own<EvidenceDefinition>();
        SetField(document, "evidenceId", "EMAIL");
        SetField(document, "displayName", "Authored title");
        SetField(document, "bodyText", "Authored text");
        SetField(document, "source", EvidenceSource.InWatch);
        var session = new WorkdaySession(new DayData(schedule,
            new List<EvidenceDefinition> { document }, new List<DayIncidentPlan>()));
        session.GetEmployeeRoster();
        session.GetEmployeeRoster().Clear();
        CollectionAssert.AreEqual(new[] { world }, schedule.WorldApprovedRecords);
        CollectionAssert.AreEqual(new[] { world }, schedule.HrKnowledge.ApprovedRecords);
        CollectionAssert.AreEqual(new Claim[] { claim }, schedule.HrKnowledge.KnownClaims);
        Assert.That(session.IsEvidenceDiscovered("EMAIL") || session.IsEvidenceRead("EMAIL"), Is.False);
    }

    [Test]
    public void ActualMonday_ExposesOnlyItsFiveExistingEmployeeIdsAndNames()
    {
        var day = AssetDatabase.LoadAssetAtPath<DayDefinition>(
            "Assets/Scripts/CompanySystem/CompanyObjects/Days/Monday.asset");
        Assert.That(day, Is.Not.Null);
        DayData data = day.GetData();
        var rows = new WorkdaySession(data).GetEmployeeRoster();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "bob", "Bob" }, { "alice", "Alice" }, { "dave", "Dave" },
            { "steve", "Steve" }, { "karen", "Karen" }
        };
        Assert.That(rows.Count, Is.EqualTo(5));
        CollectionAssert.AreEqual(data.ScheduleData.EmployeeIds, Ids(rows));
        foreach (var row in rows)
        {
            Assert.That(expected.ContainsKey(row.EmployeeId), Is.True);
            Assert.That(row.DisplayName, Is.EqualTo(expected[row.EmployeeId]));
        }
    }
}
