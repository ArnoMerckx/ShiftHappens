using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class CompanyAssetTests
{
    [TestCase("Alice", "alice", EmployeeRole.Accounts,
        EmployeeCapability.Accounts, EmployeeCapability.DataAnalysis)]
    [TestCase("Bob", "bob", EmployeeRole.Operations,
        EmployeeCapability.EquipmentHandling, EmployeeCapability.EquipmentMaintenance)]
    [TestCase("Dave", "dave", EmployeeRole.IT,
        EmployeeCapability.ComputerSupport, EmployeeCapability.None)]
    [TestCase("Karen", "karen", EmployeeRole.Administration,
        EmployeeCapability.Administration, EmployeeCapability.EventCoordination)]
    [TestCase("Steve", "steve", EmployeeRole.Dispatch,
        EmployeeCapability.EquipmentHandling, EmployeeCapability.ComputerSupport)]
    public void AuthoredEmployee_HasReadableProfileAndAgreedCapabilities(
        string filename, string employeeId, EmployeeRole role,
        EmployeeCapability first, EmployeeCapability second)
    {
        string path = $"Assets/Scripts/CompanySystem/CompanyObjects/Employees/{filename}.asset";
        var employee = AssetDatabase.LoadAssetAtPath<EmployeeDefinition>(path);
        Assert.That(employee, Is.Not.Null, path);
        Assert.That(employee.EmployeeId, Is.EqualTo(employeeId), path);
        Assert.That(employee.DisplayName, Is.EqualTo(filename), path);
        Assert.That(employee.Role, Is.EqualTo(role), path);
        Assert.That(string.IsNullOrWhiteSpace(employee.Bio), Is.False, path);
        Assert.That(string.IsNullOrWhiteSpace(employee.InterestsAndExperience), Is.False, path);

        var expected = new List<EmployeeCapability> { first };
        if (second != EmployeeCapability.None)
            expected.Add(second);

        var actual = new List<EmployeeCapability>();
        var seen = new HashSet<EmployeeCapability>();
        foreach (var capability in employee.Capabilities)
        {
            Assert.That(Enum.IsDefined(typeof(EmployeeCapability), capability), Is.True, path);
            Assert.That(capability, Is.Not.EqualTo(EmployeeCapability.None), path);
            Assert.That(seen.Add(capability), Is.True, $"Duplicate capability at {path}");
            actual.Add(capability);
        }
        CollectionAssert.AreEquivalent(expected, actual, path);
    }

    [Test]
    public void EmployeeAssets_HaveValidUniqueIdsAndRoles()
    {
        const string folder = "Assets/Scripts/CompanySystem/CompanyObjects/Employees/";

        var guids = AssetDatabase.FindAssets("t:EmployeeDefinition", new[] { "Assets" });

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var employee = AssetDatabase.LoadAssetAtPath<EmployeeDefinition>(path);

            Assert.That(employee, Is.Not.Null, path);
            Assert.That(path, Does.StartWith(folder), path);

            Assert.That(
                string.IsNullOrWhiteSpace(employee.EmployeeId),
                Is.False, path);

            Assert.That(
                ids.Add(employee.EmployeeId),
                Is.True, $"Duplicate employee ID at {path}");

            Assert.That(
                string.IsNullOrWhiteSpace(employee.DisplayName),
                Is.False, path);

            Assert.That(
                Enum.IsDefined(typeof(EmployeeRole), employee.Role),
                Is.True, path);

            Assert.That(employee.Role, Is.Not.EqualTo(EmployeeRole.None),
                path);
        }

        Assert.That(ids, Is.SupersetOf(new[]
        {
            "bob", "alice", "dave", "steve", "karen"
        }));
    }

    [Test]
    public void LocationAssets_HaveValidUniqueIds()
    {
        const string folder = "Assets/Scripts/CompanySystem/CompanyObjects/Locations/";

        var guids = AssetDatabase.FindAssets("t:LocationDefinition", new[] { "Assets" });

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(path);

            Assert.That(location, Is.Not.Null, path);
            Assert.That(path, Does.StartWith(folder), path);

            Assert.That(
                string.IsNullOrWhiteSpace(location.LocationId),
                Is.False, path);

            Assert.That(
                ids.Add(location.LocationId),
                Is.True, $"Duplicate location ID at {path}");

            Assert.That(
                string.IsNullOrWhiteSpace(location.DisplayName),
                Is.False, path);
        }

        Assert.That(ids, Is.SupersetOf(new[]
        {
            "break_room",
            "printer_area",
            "supplies_area",
            "accounts_office",
            "it_office",
            "dispatch_area",
            "offsite"
        }));
    }
}
