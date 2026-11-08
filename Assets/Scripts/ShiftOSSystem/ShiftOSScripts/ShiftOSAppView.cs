using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public abstract class ShiftOSAppView : MonoBehaviour
{
    public abstract void Initialize(WorkdaySession session, ShiftOSAppState appState);
}