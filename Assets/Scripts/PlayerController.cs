using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 5f;

    [Header("Tamaño y salud")]
    public float size = 1f;
    public int health = 100;

    private Vector2 targetPosition;
    private Transform headIndicator;
    private CircleCollider2D hitbox;
    private bool hasTarget = false;

    void Awake()
    {
        headIndicator = transform.Find("Heart");
        if (headIndicator == null)
        {
            Debug.LogWarning("No se encontró el objeto 'HeadIndicator' como hijo del jugador.");
        }

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
        Vector2 currentPosition = headIndicator != null ? (Vector2)headIndicator.position : (Vector2)transform.position;

        if (hasTarget)
        {
            Vector2 direction = targetPosition - currentPosition;
            float distance = direction.magnitude;

            if (distance < 0.01f)
            {
                // Very close to target - stop completely
                hasTarget = false;
            }
            else
            {
                // Move at constant speed toward target
                Vector2 movement = direction.normalized * speed * Time.deltaTime;

                // Don't overshoot the target
                if (movement.magnitude > distance)
                {
                    movement = direction;
                    hasTarget = false;
                }

                Vector2 newPosition = currentPosition + movement;

                if (headIndicator != null)
                    headIndicator.position = newPosition;
                else
                    transform.position = newPosition;
            }
        }
    }

    void OnValidate()
    {
        UpdateSize();
    }

    private void UpdateSize()
    {
        if (headIndicator != null)
        {
            headIndicator.localScale = Vector3.one * size;
        }

        if (hitbox != null)
        {
            hitbox.radius = size * 0.5f;
        }
    }

    // Optional: Method to stop movement immediately
    public void StopMovement()
    {
        hasTarget = false;
    }

    // Optional: Check if currently moving
    public bool IsMoving()
    {
        return hasTarget;
    }
}