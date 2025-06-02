using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BulletHellManager : MonoBehaviour
{
    [Header("Attack Patterns")]
    public List<AttackPattern> patterns;

    [Header("References")]
    public Transform playerTransform;
    public List<Transform> attackPoints;

    [Header("Timing")]
    public float patternDelay = 2f;

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

                Transform chosenPoint = attackPoints.Count > 0
                    ? attackPoints[Random.Range(0, attackPoints.Count)]
                    : playerTransform; // Fallback

                AttackContext context = new AttackContext(playerTransform, chosenPoint);
                yield return StartCoroutine(pattern.Execute(context));

                yield return new WaitForSeconds(patternDelay);
            }
        }
    }
}
