using System;
using System.Collections.Generic;

public sealed class EvaluationContext
{
    public IncidentInstance Incident { get; }
    public IncidentReport Report { get; }
    public HrKnowledge HrKnowledge { get; }
    public IReadOnlyList<Claim> SubmittedClaims { get; }

    public EvaluationContext(IncidentInstance incident, HrKnowledge hrKnowledge, EvidenceDatabase evidenceDatabase)
    {
        if (incident == null)
            throw new ArgumentNullException(nameof(incident));

        if (hrKnowledge == null)
            throw new ArgumentNullException(nameof(hrKnowledge));

        if (evidenceDatabase == null)
            throw new ArgumentNullException(nameof(evidenceDatabase));

        if (incident.Status != IncidentStatus.Pending || incident.SubmittedReport == null)
        {
            throw new InvalidOperationException("Evaluation requires a pending incident with a submitted report.");
        }

        var claims = new List<Claim>();

        foreach (var evidenceId in incident.SubmittedReport.EvidenceIds)
        {
            var document = evidenceDatabase.GetById(evidenceId);

            foreach (var claim in document.GetClaims())
                claims.Add(claim);
        }

        Incident = incident;
        Report = incident.SubmittedReport;
        HrKnowledge = hrKnowledge;
        SubmittedClaims = claims.AsReadOnly();
    }
}