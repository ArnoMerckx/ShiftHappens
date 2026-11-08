using System;
using UnityEngine;

[Serializable]
public sealed class AppRegistration
{
    [SerializeField] private AppDefinition definition;
    [SerializeField] private BaseWindow windowPrefab;

    [SerializeField] private ShiftOSAppView contentPrefab;

    public AppDefinition Definition => definition;
    public BaseWindow WindowPrefab => windowPrefab;

    public ShiftOSAppView ContentPrefab => contentPrefab;
}