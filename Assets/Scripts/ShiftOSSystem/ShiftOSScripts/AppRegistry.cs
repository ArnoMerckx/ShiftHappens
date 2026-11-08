using System;
using System.Collections.Generic;

public sealed class AppRegistry
{
    private readonly Dictionary<string, AppRegistration> registrations = new Dictionary<string, AppRegistration>(StringComparer.Ordinal);

    public int Count => registrations.Count;

    public AppRegistry(List<AppRegistration> entries)
    {
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));

        for (int i = 0; i < entries.Count; i++)
        {
            AppRegistration entry = entries[i];

            if (entry == null)
                throw new ArgumentException($"Registration {i} is null.", nameof(entries));

            if (entry.Definition == null)
                throw new ArgumentException($"Registration {i} needs an application definition.", nameof(entries));

            entry.Definition.Validate();

            if (entry.WindowPrefab == null)
                throw new ArgumentException($"Registration {i} needs a window prefab.", nameof(entries));

            string appId = entry.Definition.AppId;

            if (registrations.ContainsKey(appId))
                throw new ArgumentException($"Duplicate application ID: {appId}.", nameof(entries));

            registrations.Add(appId, entry);
        }
    }

    public AppRegistration Get(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException("An application ID is required.", nameof(appId));

        if (!registrations.TryGetValue(appId, out AppRegistration entry))
            throw new KeyNotFoundException($"Application is not registered: {appId}.");

        return entry;
    }
}