using System;
using System.Collections.Generic;

public sealed class HrKnowledge
{
    public IReadOnlyList<ApprovedScheduleRecord> ApprovedRecords { get; }
    public IReadOnlyList<Claim> KnownClaims { get; }

    public HrKnowledge(List<ApprovedScheduleRecord> approvedRecords, List<Claim> knownClaims)
    {
        if (approvedRecords == null)
            throw new ArgumentNullException(nameof(approvedRecords));

        if (knownClaims == null)
            throw new ArgumentNullException(nameof(knownClaims));

        var records = new List<ApprovedScheduleRecord>();
        var recordIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var record in approvedRecords)
        {
            if (record == null)
                throw new ArgumentException("Approved records cannot contain null.", nameof(approvedRecords));

            if (!recordIds.Add(record.RecordId))
                throw new ArgumentException($"Duplicate approved record ID: {record.RecordId}", nameof(approvedRecords));

            records.Add(record);
        }

        var claims = new List<Claim>();
        var claimIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var claim in knownClaims)
        {
            if (claim == null)
                throw new ArgumentException("Known claims cannot contain null.", nameof(knownClaims));

            if (!claimIds.Add(claim.ClaimId))
                throw new ArgumentException($"Duplicate known claim ID: {claim.ClaimId}", nameof(knownClaims));

            claims.Add(claim);
        }

        ApprovedRecords = records.AsReadOnly();
        KnownClaims = claims.AsReadOnly();
    }
}