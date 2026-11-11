using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DayDefinitionTests
{
    private const string CompanyFolder = "Assets/Scripts/CompanySystem/CompanyObjects/";
    private const string MondayPath = CompanyFolder + "Days/Monday.asset";
    private DayDefinition day;
    private readonly List<UnityEngine.Object> temporaryObjects =
        new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        day = Copy(Load<DayDefinition>(MondayPath));
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = temporaryObjects.Count - 1; i >= 0; i--)
        {
            if (temporaryObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(temporaryObjects[i]);
        }

        temporaryObjects.Clear();
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, path);
        return asset;
    }

    private T Copy<T>(T original) where T : UnityEngine.Object
    {
        T clone = UnityEngine.Object.Instantiate(original);
        temporaryObjects.Add(clone);
        return clone;
    }

    private static void Edit(
        UnityEngine.Object target, Action<SerializedObject> change)
    {
        var data = new SerializedObject(target);
        change(data);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private T FirstReference<T>(string field) where T : UnityEngine.Object
    {
        var serialized = new SerializedObject(day);
        var reference = serialized.FindProperty(field)
            .GetArrayElementAtIndex(0).objectReferenceValue as T;
        Assert.That(reference, Is.Not.Null, field);
        return reference;
    }

    private void ReplaceFirstReference(string field, UnityEngine.Object value)
    {
        Edit(day, data => data.FindProperty(field)
            .GetArrayElementAtIndex(0).objectReferenceValue = value);
    }

    private void AddReference(string field, UnityEngine.Object value)
    {
        Edit(day, data =>
        {
            var array = data.FindProperty(field);
            int index = array.arraySize;
            array.arraySize = index + 1;
            array.GetArrayElementAtIndex(index).objectReferenceValue = value;
        });
    }

    private void RepeatFirstReference(string field)
    {
        Edit(day, data =>
        {
            var array = data.FindProperty(field);
            array.GetArrayElementAtIndex(1).objectReferenceValue =
                array.GetArrayElementAtIndex(0).objectReferenceValue;
        });
    }

    private void ReplaceFirstWorldAndHr(
        ApprovedScheduleRecordDefinition definition)
    {
        ReplaceFirstReference("worldApprovedRecords", definition);
        ReplaceFirstReference("hrApprovedRecords", definition);
    }

    [Test]
    public void Monday_ComposesExpectedIdentityAndContentMembership()
    {
        DayData data = day.GetData();
        Assert.That(data.ScheduleData.Settings.DayId, Is.EqualTo("monday"));
        Assert.That(data.ScheduleData.Settings.DayIndex, Is.Zero);
        Assert.That(data.ScheduleData.Settings.WorkHours.StartMinute, Is.EqualTo(540));
        Assert.That(data.ScheduleData.Settings.WorkHours.EndMinute, Is.EqualTo(1020));
        Assert.That(data.ScheduleData.Settings.GameSecondsPerRealSecond, Is.EqualTo(40));
        Assert.That(data.ScheduleData.EmployeeIds, Is.EquivalentTo(new[]
        {
            "bob", "alice", "dave", "steve", "karen"
        }));

        var evidenceIds = new List<string>();
        foreach (var document in data.EvidenceDatabase.All)
            evidenceIds.Add(document.EvidenceId);

        Assert.That(evidenceIds, Is.EquivalentTo(new[]
        {
            "E_SCHEDULE", "E_BOB_DENTIST", "E_ALICE_MEETING",
            "E_DAVE_PRINTER", "E_KAREN_COLLECTION", "E_OFFICE_SOCIAL",
            "E_SUPPLIES_RECEIPT"
        }));
        Assert.That(data.Incidents.Count, Is.EqualTo(3));
    }

    [TestCase("BobDentist", "dentist", "bob", "offsite", 600, 690, true)]
    [TestCase("AliceMeeting", "meeting", "alice", "accounts_office", 565, 590, true)]
    [TestCase("DavePrinterInspection", "inspection", "dave", "printer_area", 630, 645, false)]
    [TestCase("KarenCollection", "collection", "karen", "supplies_area", 810, 825, false)]
    public void ApprovedRecordAssets_ConvertAgreedValues(
        string filename, string recordId, string employeeId,
        string locationId, int start, int end, bool required)
    {
        var definition = Load<ApprovedScheduleRecordDefinition>(
            CompanyFolder + "ApprovedScheduleRecords/" + filename + ".asset");
        ApprovedScheduleRecord record = definition.ToRecord();

        Assert.That(record.RecordId, Is.EqualTo(recordId));
        Assert.That(record.EmployeeId, Is.EqualTo(employeeId));
        Assert.That(record.LocationId, Is.EqualTo(locationId));
        Assert.That(record.Period.DayIndex, Is.Zero);
        Assert.That(record.Period.StartMinute, Is.EqualTo(start));
        Assert.That(record.Period.EndMinute, Is.EqualTo(end));
        Assert.That(record.Priority, Is.EqualTo(100));
        Assert.That(record.RequiresPresence, Is.EqualTo(required));
        Assert.That(record.BlocksDelegation, Is.True);
    }

    [Test]
    public void Monday_KnowledgeUsesCanonicalWorldRecordsAndExplicitClaims()
    {
        DayScheduleData data = day.GetScheduleData();
        var world = new List<ApprovedScheduleRecord>(data.WorldApprovedRecords);
        var recordIds = new List<string>();
        var claimIds = new List<string>();

        Assert.That(world.Count, Is.EqualTo(4));
        foreach (var record in data.HrKnowledge.ApprovedRecords)
        {
            recordIds.Add(record.RecordId);
            Assert.That(record, Is.SameAs(
                world.Find(candidate => candidate.RecordId == record.RecordId)));
        }

        foreach (var claim in data.HrKnowledge.KnownClaims)
            claimIds.Add(claim.ClaimId);

        Assert.That(recordIds, Is.EquivalentTo(new[]
        {
            "dentist", "meeting", "inspection", "collection"
        }));
        Assert.That(claimIds, Is.EquivalentTo(new[]
        {
            "C_BOB_DENTIST", "C_ALICE_MEETING",
            "C_DAVE_PRINTER", "C_KAREN_COLLECTION"
        }));
    }

    [Test]
    public void ReceiptDiscovery_DoesNotGrantHrKnowledgeOrOtherSessionDiscovery()
    {
        DayData data = day.GetData();
        var first = new EvidenceService(data.EvidenceDatabase);
        var second = new EvidenceService(data.EvidenceDatabase);

        Assert.That(first.IsAvailable("E_SUPPLIES_RECEIPT", 0, 540), Is.True);
        Assert.That(first.CanAttach("E_SUPPLIES_RECEIPT", 0, 540), Is.False);
        Assert.That(first.MarkRead("E_SUPPLIES_RECEIPT", 0, 540), Is.True);
        Assert.That(first.CanAttach("E_SUPPLIES_RECEIPT", 0, 540), Is.True);
        Assert.That(second.IsDiscovered("E_SUPPLIES_RECEIPT"), Is.False);
        Assert.That(second.IsRead("E_SUPPLIES_RECEIPT"), Is.False);

        foreach (var claim in data.ScheduleData.HrKnowledge.KnownClaims)
            Assert.That(claim.ClaimId, Is.Not.EqualTo("C_SUPPLIES_HANDOVER"));

        Assert.That(data.ScheduleData.HrKnowledge.KnownClaims.Count, Is.EqualTo(4));
        Assert.That(data.ScheduleData.WorldApprovedRecords.Count, Is.EqualTo(4));
    }

    [TestCase(0, "D0I1", "I1", 575, 580, 590, 630, 10, "break_room", "")]
    [TestCase(1, "D0I2", "I2", 635, 640, 675, 720, 15, "printer_area", "")]
    [TestCase(2, "D0I3", "I3", 815, 820, 840, 900, 15, "supplies_area", "supplies_carton_01")]
    public void Monday_IncidentPlansPreserveAgreedContent(
        int index, string instanceId, string incidentId,
        int start, int end, int delivery, int deadline, int delay,
        string locationId, string itemId)
    {
        IncidentInstance instance = day.GetData().Incidents[index].CreateInstance();
        Assert.That(instance.InstanceId, Is.EqualTo(instanceId));
        Assert.That(instance.IncidentId, Is.EqualTo(incidentId));
        Assert.That(instance.DayIndex, Is.Zero);
        Assert.That(instance.Occurrence.StartMinute, Is.EqualTo(start));
        Assert.That(instance.Occurrence.EndMinute, Is.EqualTo(end));
        Assert.That(instance.DeliveryMinute, Is.EqualTo(delivery));
        Assert.That(instance.DeadlineMinute, Is.EqualTo(deadline));
        Assert.That(instance.ReviewDelayMinutes, Is.EqualTo(delay));
        Assert.That(instance.LocationId, Is.EqualTo(locationId));
        Assert.That(instance.RelatedItemId, Is.EqualTo(itemId));
        Assert.That(instance.Status, Is.EqualTo(IncidentStatus.Open));
        Assert.That(instance.SubmittedReport, Is.Null);
        Assert.That(instance.Decision, Is.Null);
    }

    [Test]
    public void RepeatedConversion_ProducesIndependentRuntimeSnapshots()
    {
        DayData first = day.GetData();
        DayData second = day.GetData();

        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(second.ScheduleData, Is.Not.SameAs(first.ScheduleData));
        Assert.That(second.ScheduleData.HrKnowledge,
            Is.Not.SameAs(first.ScheduleData.HrKnowledge));
        Assert.That(second.ScheduleData.WorldApprovedRecords[0],
            Is.Not.SameAs(first.ScheduleData.WorldApprovedRecords[0]));
        Assert.That(second.Incidents[0], Is.Not.SameAs(first.Incidents[0]));
        Assert.That(second.EvidenceDatabase, Is.Not.SameAs(first.EvidenceDatabase));
        Assert.That(second.EvidenceDatabase.GetById("E_SCHEDULE"),
            Is.SameAs(first.EvidenceDatabase.GetById("E_SCHEDULE")));
    }

    [Test]
    public void ClearingAuthoredCollections_DoesNotRewriteExistingSnapshot()
    {
        DayData before = day.GetData();
        Edit(day, data =>
        {
            foreach (string field in new[]
            {
                "employees", "evidence", "worldApprovedRecords",
                "hrApprovedRecords", "hrKnownClaims", "incidents",
                "ordinaryAvailability", "requests"
            })
                data.FindProperty(field).arraySize = 0;
        });

        DayData after = day.GetData();
        Assert.That(before.ScheduleData.EmployeeIds.Count, Is.EqualTo(5));
        Assert.That(before.ScheduleData.WorldApprovedRecords.Count, Is.EqualTo(4));
        Assert.That(before.ScheduleData.HrKnowledge.ApprovedRecords.Count, Is.EqualTo(4));
        Assert.That(before.ScheduleData.HrKnowledge.KnownClaims.Count, Is.EqualTo(4));
        Assert.That(before.EvidenceDatabase.All.Count, Is.EqualTo(7));
        Assert.That(before.Incidents.Count, Is.EqualTo(3));
        Assert.That(before.ScheduleData.OrdinaryAvailability.Count, Is.EqualTo(5));
        Assert.That(before.Requests.Count, Is.EqualTo(2));
        Assert.That(after.ScheduleData.EmployeeIds, Is.Empty);
        Assert.That(after.ScheduleData.WorldApprovedRecords, Is.Empty);
        Assert.That(after.ScheduleData.HrKnowledge.ApprovedRecords, Is.Empty);
        Assert.That(after.ScheduleData.HrKnowledge.KnownClaims, Is.Empty);
        Assert.That(after.EvidenceDatabase.All, Is.Empty);
        Assert.That(after.Incidents, Is.Empty);
        Assert.That(after.ScheduleData.OrdinaryAvailability, Is.Empty);
        Assert.That(after.Requests, Is.Empty);
        Assert.That(Load<DayDefinition>(MondayPath).GetData().Incidents.Count,
            Is.EqualTo(3));
    }

    [TestCase("employees", typeof(InvalidOperationException))]
    [TestCase("evidence", typeof(ArgumentException))]
    [TestCase("worldApprovedRecords", typeof(InvalidOperationException))]
    [TestCase("hrApprovedRecords", typeof(InvalidOperationException))]
    [TestCase("hrKnownClaims", typeof(InvalidOperationException))]
    public void MissingListReference_IsRejected(string field, Type exceptionType)
    {
        ReplaceFirstReference(field, null);
        Assert.Throws(exceptionType, () => day.GetData());
    }

    [Test]
    public void DuplicateEmployeeReference_IsRejected()
    {
        AddReference("employees", FirstReference<EmployeeDefinition>("employees"));
        Assert.Throws<ArgumentException>(() => day.GetScheduleData());
    }

    [Test]
    public void RepeatedWorldAsset_IsRejected()
    {
        AddReference("worldApprovedRecords",
            FirstReference<ApprovedScheduleRecordDefinition>("worldApprovedRecords"));
        Assert.Throws<InvalidOperationException>(() => day.GetScheduleData());
    }

    [Test]
    public void DistinctWorldAssetsWithSameId_AreRejected()
    {
        var duplicate = Copy(
            FirstReference<ApprovedScheduleRecordDefinition>("worldApprovedRecords"));
        AddReference("worldApprovedRecords", duplicate);
        Assert.Throws<ArgumentException>(() => day.GetScheduleData());
    }

    [Test]
    public void HrAssetOutsideWorldSelection_IsRejectedEvenWithMatchingId()
    {
        var duplicate = Copy(
            FirstReference<ApprovedScheduleRecordDefinition>("worldApprovedRecords"));
        ReplaceFirstReference("hrApprovedRecords", duplicate);
        Assert.Throws<InvalidOperationException>(() => day.GetScheduleData());
    }

    [Test]
    public void DuplicateHrRecordSelection_IsRejected()
    {
        RepeatFirstReference("hrApprovedRecords");
        Assert.Throws<ArgumentException>(() => day.GetScheduleData());
    }

    [Test]
    public void DuplicateKnownClaimSelection_IsRejected()
    {
        RepeatFirstReference("hrKnownClaims");
        Assert.Throws<ArgumentException>(() => day.GetScheduleData());
    }

    [Test]
    public void MissingIncidentTemplate_IsRejected()
    {
        Edit(day, data => data.FindProperty("incidents")
            .GetArrayElementAtIndex(0).FindPropertyRelative("incident")
            .objectReferenceValue = null);
        Assert.Throws<InvalidOperationException>(() => day.GetData());
    }

    [Test]
    public void DuplicateAuthoredIncidentInstanceId_IsRejected()
    {
        Edit(day, data =>
        {
            var entries = data.FindProperty("incidents");
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("instanceId")
                .stringValue = entries.GetArrayElementAtIndex(0)
                .FindPropertyRelative("instanceId").stringValue;
        });
        Assert.Throws<ArgumentException>(() => day.GetData());
    }

    [TestCase("occurrenceEndMinute", 575)]
    [TestCase("deadlineMinute", 590)]
    public void InvalidAuthoredIncidentTiming_IsRejected(string field, int value)
    {
        Edit(day, data => data.FindProperty("incidents")
            .GetArrayElementAtIndex(0).FindPropertyRelative(field).intValue = value);
        Assert.Throws<ArgumentException>(() => day.GetData());
    }

    [Test]
    public void WorldRecordEmployeeOutsideDay_IsRejected()
    {
        var employee = Copy(FirstReference<EmployeeDefinition>("employees"));
        Edit(employee, data =>
            data.FindProperty("employeeId").stringValue = "outside_day");
        var record = Copy(
            FirstReference<ApprovedScheduleRecordDefinition>("worldApprovedRecords"));
        Edit(record, data =>
            data.FindProperty("employee").objectReferenceValue = employee);
        ReplaceFirstWorldAndHr(record);
        Assert.Throws<ArgumentException>(() => day.GetScheduleData());
    }

    [Test]
    public void KnownClaimEmployeeOutsideDay_IsRejected()
    {
        var employee = Copy(FirstReference<EmployeeDefinition>("employees"));
        Edit(employee, data =>
            data.FindProperty("employeeId").stringValue = "outside_day");
        var claim = Copy(FirstReference<ClaimDefinition>("hrKnownClaims"));
        Edit(claim, data =>
            data.FindProperty("employee").objectReferenceValue = employee);
        ReplaceFirstReference("hrKnownClaims", claim);
        Assert.Throws<ArgumentException>(() => day.GetScheduleData());
    }

    [TestCase("employee")]
    [TestCase("location")]
    public void ApprovedRecordConversion_RejectsMissingReference(string field)
    {
        var record = Copy(
            FirstReference<ApprovedScheduleRecordDefinition>("worldApprovedRecords"));
        Edit(record, data => data.FindProperty(field).objectReferenceValue = null);
        Assert.Throws<InvalidOperationException>(() => record.ToRecord());
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void ApprovedRecordConversion_RejectsBlankId(string id)
    {
        var record = Copy(
            FirstReference<ApprovedScheduleRecordDefinition>("worldApprovedRecords"));
        Edit(record, data => data.FindProperty("recordId").stringValue = id);
        Assert.Throws<ArgumentException>(() => record.ToRecord());
    }

    [TestCase(600, 600)]
    [TestCase(700, 690)]
    public void ApprovedRecordConversion_RejectsEmptyOrReversedPeriod(int start, int end)
    {
        var record = Copy(
            FirstReference<ApprovedScheduleRecordDefinition>("worldApprovedRecords"));
        Edit(record, data =>
        {
            data.FindProperty("startMinute").intValue = start;
            data.FindProperty("endMinute").intValue = end;
        });
        Assert.Throws<ArgumentException>(() => record.ToRecord());
    }

    [Test]
    public void ApprovedRecordConversion_RejectsNegativePriority()
    {
        var record = Copy(
            FirstReference<ApprovedScheduleRecordDefinition>("worldApprovedRecords"));
        Edit(record, data => data.FindProperty("priority").intValue = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => record.ToRecord());
    }

    [Test]
    public void AuthoredRecordFromAnotherDay_IsRejected()
    {
        var record = Copy(
            FirstReference<ApprovedScheduleRecordDefinition>("worldApprovedRecords"));
        Edit(record, data => data.FindProperty("dayIndex").intValue = 1);
        ReplaceFirstWorldAndHr(record);
        Assert.Throws<ArgumentException>(() => day.GetScheduleData());
    }
}
