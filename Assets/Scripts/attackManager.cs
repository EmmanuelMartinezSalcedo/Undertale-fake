using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BulletHellManager : MonoBehaviour
{
    public List<AttackPattern> patterns;
    public Transform attackOrigin;
    public float patternDelay = 2f; // Tiempo entre patrones

    private void Start()
    {
        StartCoroutine(ExecutePatternsLoop());
    }

    private IEnumerator ExecutePatternsLoop()
    {
        while (true)
        {
            foreach (AttackPattern pattern in patterns)
            {
                Debug.Log("Starting pattern: " + pattern.name);

                // Ejecuta el patrón directamente
                yield return StartCoroutine(pattern.Execute(attackOrigin));

                yield return new WaitForSeconds(patternDelay);
            }
        }
    }
}
