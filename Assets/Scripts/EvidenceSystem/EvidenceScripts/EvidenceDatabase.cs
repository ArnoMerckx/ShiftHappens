using System;
using System.Collections.Generic;

public sealed class EvidenceDatabase
{
    private readonly Dictionary<string, EvidenceDefinition> byId = new Dictionary<string, EvidenceDefinition>(StringComparer.Ordinal);

    public IReadOnlyList<EvidenceDefinition> All { get; }

    public EvidenceDatabase(List<EvidenceDefinition> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        var documents = new List<EvidenceDefinition>();

        foreach (var definition in definitions)
        {
            if (definition == null)
                throw new ArgumentException("Evidence definitions cannot contain null.", nameof(definitions));

            if (string.IsNullOrWhiteSpace(definition.EvidenceId))
                throw new ArgumentException("Every document requires an evidence ID.", nameof(definitions));

            if (byId.ContainsKey(definition.EvidenceId))
                throw new ArgumentException($"Duplicate evidence ID: {definition.EvidenceId}", nameof(definitions));

            ValidateDefinition(definition);

            byId.Add(definition.EvidenceId, definition);
            documents.Add(definition);
        }

        All = documents.AsReadOnly();
    }

    public EvidenceDefinition GetById(string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
            throw new ArgumentException("Evidence ID is required.", nameof(evidenceId));

        if (!byId.TryGetValue(evidenceId, out var definition))
            throw new KeyNotFoundException( $"Unknown evidence ID: {evidenceId}");

        return definition;
    }

    private static void ValidateDefinition(EvidenceDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.DisplayName) || string.IsNullOrWhiteSpace(definition.BodyText))
        {
            throw new ArgumentException($"Document {definition.EvidenceId} requires a title and body.");
        }

        if (definition.Source == EvidenceSource.None || !Enum.IsDefined(typeof(EvidenceSource), definition.Source))
        {
            throw new ArgumentException($"Document {definition.EvidenceId} requires a valid source.");
        }

        if (definition.AvailableDayIndex < 0 || definition.AvailableMinute < 0 || definition.AvailableMinute >= 1440)
        {
            throw new ArgumentException($"Document {definition.EvidenceId} has invalid availability.");
        }

        var claimIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var claim in definition.GetClaims())
        {
            if (claim == null)
                throw new ArgumentException($"Document {definition.EvidenceId} contains a null claim.");

            if (!claimIds.Add(claim.ClaimId))
                throw new ArgumentException($"Document {definition.EvidenceId} repeats claim {claim.ClaimId}.");
        }
    }
}
