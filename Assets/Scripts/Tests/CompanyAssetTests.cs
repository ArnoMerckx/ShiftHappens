using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class CompanyAssetTests
{
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