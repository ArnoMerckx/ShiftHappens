using System;

public abstract class Claim
{
    public string ClaimId { get; }
    public string EmployeeId { get; }

    protected Claim(string claimId, string employeeId)
    {
        if (string.IsNullOrWhiteSpace(claimId))
            throw new ArgumentException("Claim ID is required.", nameof(claimId));

        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Employee ID is required.", nameof(employeeId));

        ClaimId = claimId;
        EmployeeId = employeeId;
    }
}