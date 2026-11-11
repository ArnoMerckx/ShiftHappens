using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RequestProfileTests
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

    private EmployeeDefinition Employee(string id, params EmployeeCapability[] capabilities)
    {
        var employee = Own<EmployeeDefinition>();
        SetField(employee, "employeeId", id);
        SetField(employee, "displayName", id);
        SetField(employee, "capabilities", new List<EmployeeCapability>(capabilities));
        return employee;
    }

    private static RequestProfileTierEntry Entry(EmployeeDefinition employee, int tier)
    {
        var entry = new RequestProfileTierEntry();
        SetField(entry, "employee", employee);
        SetField(entry, "tier", tier);
        return entry;
    }

    private RequestProfileDefinition Profile(
        List<RequestProfileTierEntry> ranked, List<EmployeeDefinition> unsuitable)
    {
        var profile = Own<RequestProfileDefinition>();
        SetField(profile, "profileId", "desktop_support");
        SetField(profile, "requiredCapability", EmployeeCapability.ComputerSupport);
        SetField(profile, "rankedEmployees", ranked);
        SetField(profile, "unsuitableEmployees", unsuitable);
        return profile;
    }

    [TestCase("blankId")]
    [TestCase("noCapability")]
    [TestCase("undefinedCapability")]
    [TestCase("nullRanked")]
    [TestCase("nullUnsuitable")]
    [TestCase("nullRankedEntry")]
    [TestCase("duplicateRanked")]
    [TestCase("duplicateUnsuitable")]
    [TestCase("crossList")]
    [TestCase("blankUnsuitable")]
    public void RuntimeProfile_RejectsInconsistentInputs(string invalid)
    {
        string id = invalid == "blankId" ? " " : "desktop_support";
        var capability = invalid == "noCapability" ? EmployeeCapability.None :
            invalid == "undefinedCapability" ? (EmployeeCapability)999 : EmployeeCapability.ComputerSupport;
        var ranked = new List<RequestSuitabilityEntry> { new RequestSuitabilityEntry("dave", 0) };
        var unsuitable = new List<string> { "bob" };

        switch (invalid)
        {
            case "nullRanked": ranked = null; break;
            case "nullUnsuitable": unsuitable = null; break;
            case "nullRankedEntry": ranked.Add(null); break;
            case "duplicateRanked": ranked.Add(new RequestSuitabilityEntry("dave", 1)); break;
            case "duplicateUnsuitable": unsuitable.Add("bob"); break;
            case "crossList": unsuitable.Add("dave"); break;
            case "blankUnsuitable": unsuitable.Add(" "); break;
        }
        Assert.Catch<ArgumentException>(() => new RequestProfileData(id, capability, ranked, unsuitable));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void BlankRankedIdentity_IsRejected(string employeeId)
    {
        Assert.Throws<ArgumentException>(() => new RequestSuitabilityEntry(employeeId, 0));
    }

    [Test]
    public void NegativeTier_IsRejectedByBothRuntimeAndAuthoredConversion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestSuitabilityEntry("dave", -1));
        var profile = Profile(new List<RequestProfileTierEntry>
        {
            Entry(Employee("dave", EmployeeCapability.ComputerSupport), -1)
        }, new List<EmployeeDefinition>());
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.ToData());
    }

    [TestCase("nullRanked")]
    [TestCase("nullUnsuitable")]
    [TestCase("nullEntry")]
    [TestCase("missingRankedEmployee")]
    [TestCase("missingUnsuitableEmployee")]
    [TestCase("missingCapability")]
    public void AuthoredProfile_RejectsMissingReferencesAndCapabilities(string invalid)
    {
        var employee = Employee("dave", EmployeeCapability.ComputerSupport);
        var ranked = new List<RequestProfileTierEntry> { Entry(employee, 0) };
        var unsuitable = new List<EmployeeDefinition>();
        switch (invalid)
        {
            case "nullRanked": ranked = null; break;
            case "nullUnsuitable": unsuitable = null; break;
            case "nullEntry": ranked.Add(null); break;
            case "missingRankedEmployee": ranked[0] = Entry(null, 0); break;
            case "missingUnsuitableEmployee": unsuitable.Add(null); break;
            case "missingCapability": SetField(employee, "capabilities",
                new List<EmployeeCapability> { EmployeeCapability.EquipmentHandling }); break;
        }
        var profile = Profile(ranked, unsuitable);
        Assert.Throws<InvalidOperationException>(() => profile.ToData());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DuplicateIdentityAcrossDistinctAssets_IsRejected(bool crossList)
    {
        var first = Employee("dave", EmployeeCapability.ComputerSupport);
        var second = Employee("dave", EmployeeCapability.ComputerSupport);
        var ranked = new List<RequestProfileTierEntry> { Entry(first, 0) };
        var unsuitable = new List<EmployeeDefinition>();
        if (crossList) unsuitable.Add(second);
        else ranked.Add(Entry(second, 1));
        Assert.Throws<ArgumentException>(() => Profile(ranked, unsuitable).ToData());
    }

    [Test]
    public void AuthoredTies_PreserveExplicitTierAndInputOrder()
    {
        var profile = Profile(new List<RequestProfileTierEntry>
        {
            Entry(Employee("steve", EmployeeCapability.ComputerSupport), 2),
            Entry(Employee("dave", EmployeeCapability.ComputerSupport), 0),
            Entry(Employee("other", EmployeeCapability.ComputerSupport), 2)
        }, new List<EmployeeDefinition> { Employee("bob") });
        var data = profile.ToData();
        var ids = new List<string>();
        var tiers = new List<int>();
        foreach (var entry in data.RankedEmployees)
        {
            ids.Add(entry.EmployeeId);
            tiers.Add(entry.Tier);
        }
        CollectionAssert.AreEqual(new[] { "steve", "dave", "other" }, ids);
        CollectionAssert.AreEqual(new[] { 2, 0, 2 }, tiers);
        CollectionAssert.AreEqual(new[] { "bob" }, data.UnsuitableEmployeeIds);
    }

    [Test]
    public void CapabilityDoesNotForceRanking_ExplicitAllUnsuitableProfileIsValid()
    {
        var profile = Profile(new List<RequestProfileTierEntry>(),
            new List<EmployeeDefinition> { Employee("dave", EmployeeCapability.ComputerSupport) });
        var data = profile.ToData();
        Assert.That(data.RankedEmployees, Is.Empty);
        CollectionAssert.AreEqual(new[] { "dave" }, data.UnsuitableEmployeeIds);
    }

    [Test]
    public void RuntimeMembership_IsIndependentReadOnlyAndOrdinal()
    {
        var ranked = new List<RequestSuitabilityEntry> { new RequestSuitabilityEntry("dave", 0) };
        var unsuitable = new List<string> { "Dave" };
        var data = new RequestProfileData("desktop_support", EmployeeCapability.ComputerSupport, ranked, unsuitable);
        ranked.Clear();
        unsuitable.Clear();
        Assert.That(data.RankedEmployees[0].EmployeeId, Is.EqualTo("dave"));
        CollectionAssert.AreEqual(new[] { "Dave" }, data.UnsuitableEmployeeIds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RequestSuitabilityEntry>)data.RankedEmployees).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)data.UnsuitableEmployeeIds).Clear());
    }

    [Test]
    public void ConvertedProfile_SurvivesSourceEditsAndDestruction()
    {
        var employee = Employee("dave", EmployeeCapability.ComputerSupport);
        var unsuitableEmployee = Employee("bob");
        var entry = Entry(employee, 0);
        var ranked = new List<RequestProfileTierEntry> { entry };
        var unsuitable = new List<EmployeeDefinition> { unsuitableEmployee };
        var profile = Profile(ranked, unsuitable);
        var original = profile.ToData();

        SetField(profile, "profileId", "equipment_help");
        SetField(profile, "requiredCapability", EmployeeCapability.EquipmentHandling);
        SetField(employee, "employeeId", "changed_helper");
        SetField(employee, "capabilities", new List<EmployeeCapability> { EmployeeCapability.EquipmentHandling });
        SetField(entry, "tier", 4);
        SetField(unsuitableEmployee, "employeeId", "changed_unsuitable");
        var updated = profile.ToData();
        Assert.That(updated.ProfileId, Is.EqualTo("equipment_help"));
        Assert.That(updated.RequiredCapability, Is.EqualTo(EmployeeCapability.EquipmentHandling));
        Assert.That(updated.RankedEmployees[0].EmployeeId, Is.EqualTo("changed_helper"));
        Assert.That(updated.RankedEmployees[0].Tier, Is.EqualTo(4));
        CollectionAssert.AreEqual(new[] { "changed_unsuitable" }, updated.UnsuitableEmployeeIds);

        ranked.Clear();
        unsuitable.Clear();
        UnityEngine.Object.DestroyImmediate(profile);
        UnityEngine.Object.DestroyImmediate(employee);
        UnityEngine.Object.DestroyImmediate(unsuitableEmployee);
        Assert.That(original.ProfileId, Is.EqualTo("desktop_support"));
        Assert.That(original.RequiredCapability, Is.EqualTo(EmployeeCapability.ComputerSupport));
        Assert.That(original.RankedEmployees[0].EmployeeId, Is.EqualTo("dave"));
        Assert.That(original.RankedEmployees[0].Tier, Is.EqualTo(0));
        CollectionAssert.AreEqual(new[] { "bob" }, original.UnsuitableEmployeeIds);
    }
}
