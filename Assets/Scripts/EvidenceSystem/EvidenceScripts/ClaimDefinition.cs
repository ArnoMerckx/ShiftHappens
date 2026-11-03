using UnityEngine;

public abstract class ClaimDefinition : ScriptableObject
{
    public abstract Claim ToClaim();
}