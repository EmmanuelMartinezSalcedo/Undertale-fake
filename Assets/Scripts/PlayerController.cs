using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 1f;  // Controla qué tan rápido se mueve hacia targetPosition

    [Header("Tamaño y salud")]
    public float size = 1f;
    public int health = 100;

    private Vector2 targetPosition;
    private bool hasTarget = false;

    public void SetTargetPosition(Vector2 worldPosition)
    {
        targetPosition = worldPosition;
        hasTarget = true;
    }

    void Update()
    {
        if (hasTarget)
        {
            Vector2 currentPos = transform.position;
            // Mover suavemente hacia targetPosition con Lerp
            Vector2 newPos = Vector2.Lerp(currentPos, targetPosition, speed * Time.deltaTime);
            transform.position = newPos;

            // Si está muy cerca, deja de moverse
            if (Vector2.Distance(newPos, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                hasTarget = false;
            }
        }
    }
}
