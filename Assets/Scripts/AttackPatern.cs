using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "AttackPatern", menuName = "Scriptable Objects/AttackPatern")]
public abstract class AttackPattern : ScriptableObject
{
    public float duration = 3f;

    public abstract IEnumerator Execute(Transform origin);
}
