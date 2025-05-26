using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;

    [Header("Tamaño y salud")]
    public float size = 1f;
    public int health = 100;

    private Vector2 targetPosition;
    private CircleCollider2D hitbox;
    private bool hasTarget = false;

    void Awake()
    {
        hitbox = GetComponent<CircleCollider2D>();
        if (hitbox == null)
        {
            hitbox = gameObject.AddComponent<CircleCollider2D>();
        }

        UpdateSize();
    }

    public void SetTargetPosition(Vector2 worldPosition)
    {
        targetPosition = worldPosition;
        hasTarget = true;
    }

    void Update()
    {
        if (!hasTarget) return;

        Vector2 currentPosition = transform.position;
        Vector2 direction = targetPosition - currentPosition;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
        {
            transform.position = targetPosition; // Corrige posición exacta
            hasTarget = false;
            return;
        }

        Vector2 movement = direction.normalized * speed * Time.deltaTime;

        // Si va a pasarse del objetivo, clava directamente en target
        if (movement.magnitude >= distance)
        {
            transform.position = targetPosition;
            hasTarget = false;
        }
        else
        {
            transform.position = currentPosition + movement;
        }
    }

    void OnValidate()
    {
        UpdateSize();
    }

    private void UpdateSize()
    {
        transform.localScale = Vector3.one * size;
    }

    public void StopMovement()
    {
        hasTarget = false;
    }

    public bool IsMoving()
    {
        return hasTarget;
    }
}
