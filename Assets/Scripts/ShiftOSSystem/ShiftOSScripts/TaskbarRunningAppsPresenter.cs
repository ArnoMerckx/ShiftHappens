using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TaskbarRunningAppsPresenter : MonoBehaviour
{
    [SerializeField] private WindowManagerController windowManager;
    [SerializeField] private RectTransform runningApps;
    [SerializeField] private TaskbarButtonView buttonPrefab;

    private List<AppDefinition> definitions;

    private readonly Dictionary<string, TaskbarButtonView> entries = new Dictionary<string, TaskbarButtonView>(StringComparer.Ordinal);

    private void Start()
    {
        if (windowManager == null || runningApps == null ||
            buttonPrefab == null)
        {
            throw new InvalidOperationException("Assign the window manager, running row and button prefab.");
        }

        if (runningApps.childCount != 0)
            throw new InvalidOperationException("The running-app row must start empty.");

        definitions = windowManager.GetApplicationDefinitions();
        windowManager.WindowsChanged += RefreshEntries;
        RefreshEntries();
    }

    private void RefreshEntries()
    {
        List<WindowState> windows = windowManager.GetOpenWindows();

        var open = new Dictionary<string, WindowState>(
            StringComparer.Ordinal);

        for (int i = 0; i < windows.Count; i++)
            open.Add(windows[i].AppId, windows[i]);

        var closedIds = new List<string>();

        foreach (var pair in entries)
        {
            if (!open.ContainsKey(pair.Key))
                closedIds.Add(pair.Key);
        }

        for (int i = 0; i < closedIds.Count; i++)
        {
            string id = closedIds[i];
            TaskbarButtonView view = entries[id];

            entries.Remove(id);
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            AppDefinition definition = definitions[i];

            if (!open.TryGetValue(definition.AppId, out WindowState state))
            {
                continue;
            }

            if (!entries.TryGetValue(definition.AppId, out TaskbarButtonView view))
            {
                view = Instantiate(buttonPrefab, runningApps, false);
                view.name = definition.AppId + " Running";

                view.Bind(
                    definition, windowManager.ActivateRunningWindow);

                entries.Add(definition.AppId, view);
            }

            view.ApplyRunningState(state);
            view.transform.SetAsLastSibling();
        }
    }

    private void OnDestroy()
    {
        if (windowManager != null)
            windowManager.WindowsChanged -= RefreshEntries;
    }
}