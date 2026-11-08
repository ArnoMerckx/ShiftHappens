using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TaskbarLauncherPresenter : MonoBehaviour
{
    [SerializeField] private WindowManagerController windowManager;
    [SerializeField] private RectTransform launchers;
    [SerializeField] private TaskbarButtonView buttonPrefab;

    private void Start()
    {
        if (windowManager == null || launchers == null ||
            buttonPrefab == null)
        {
            throw new InvalidOperationException("Assign the window manager, launcher row and button prefab.");
        }

        if (launchers.childCount != 0)
            throw new InvalidOperationException("Remove the temporary launcher buttons before generating launchers.");

        List<AppDefinition> definitions =
            windowManager.GetApplicationDefinitions();

        for (int i = 0; i < definitions.Count; i++)
        {
            AppDefinition definition = definitions[i];

            TaskbarButtonView view = Instantiate(buttonPrefab, launchers, false);

            view.name = definition.AppId + " Launcher";
            view.Bind(definition, windowManager.Launch);
        }
    }
}