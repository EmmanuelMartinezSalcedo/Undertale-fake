using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "BulletHell/Patterns/CircularPattern")]
public class CircularPattern : AttackPattern
{
    public GameObject bulletPrefab;
    public int bulletCount = 12;
    public float bulletSpeed = 3f;

    public override IEnumerator Execute(AttackContext context)
    {
        float angleStep = 360f / bulletCount;
        Vector3 spawnPos = context.attackPoint != null ? context.attackPoint.position : Vector3.zero;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            GameObject bullet = GameObject.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            bullet.GetComponent<Rigidbody2D>().linearVelocity = dir * bulletSpeed;
        }

        yield return new WaitForSeconds(duration);
    }
}
