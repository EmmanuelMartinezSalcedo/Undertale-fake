using UnityEngine;
using System.Collections;

public abstract class AttackPattern : ScriptableObject
{
    public float duration = 3f;

    public abstract IEnumerator Execute(AttackContext context);
}
