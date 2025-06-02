using UnityEngine;

public class ArrowEnemy : MonoBehaviour
{
    public float speed = 5f;

    private Transform playerTransform;
    private bool isTracking = true;
    private Vector2 travelDirection;
    public float lifeTime = 5f;
    public void Initialize(Transform player)
    {
        playerTransform = player;
        isTracking = true;
        Destroy(gameObject, lifeTime);
    }

    public void Shoot()
    {
        isTracking = false;
        travelDirection = (playerTransform.position - transform.position).normalized;
    }

    void Update()
    {
        if (isTracking)
        {
            Vector2 direction = (playerTransform.position - transform.position).normalized;
            transform.up = direction;
        }

        transform.position += (Vector3)(travelDirection * speed * Time.deltaTime);
    }
}
