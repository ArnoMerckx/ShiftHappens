using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class TerminalSessionActions : MonoBehaviour
{
    [SerializeField] private WorkdaySessionDriver sessionDriver;
    [SerializeField] private ViewModeController viewModes;

    private bool leavingScene;

    private bool CanExecute => isActiveAndEnabled && !leavingScene && sessionDriver.HasBegunShift && sessionDriver.Session != null && sessionDriver.Session.Workday.IsTerminal && viewModes.CanUseSessionMenuInput;

    private void Awake()
    {
        if (sessionDriver == null || viewModes == null)
        {
            throw new InvalidOperationException("Assign the session driver and view modes.");
        }
    }

    public void ReplayMonday()
    {
        if (!CanExecute)
            return;

        Scene scene = SceneManager.GetActiveScene();

        if (scene.buildIndex < 0)
        {
            Debug.LogError("Add the current scene to the build scene list before replaying.", this);
            return;
        }

        leavingScene = true;
        SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
    }

    public void Quit()
    {
        if (!CanExecute)
            return;

        if (Application.isEditor)
        {
            Debug.Log("Quit requested. The standalone will close.", this);
            return;
        }

        leavingScene = true;
        Application.Quit();
    }
}