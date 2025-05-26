using System.Collections.Generic;
using UnityEngine;

public class SpawnArrowsController : MonoBehaviour
{
    public int arrowCount = 3;
    public GameObject arrowPrefab;
    public GameObject alertPrefab;
    public Transform playerTransform;

    // Distancia fuera de cámara donde se generarán las flechas
    public float spawnDistance = 10f;

    private List<ArrowEnemy> arrows = new List<ArrowEnemy>();
    private List<AlertBlink> alerts = new List<AlertBlink>();

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        SpawnArrowsAndAlerts();
    }

    void SpawnArrowsAndAlerts()
    {
        for (int i = 0; i < arrowCount; i++)
        {
            Vector3 spawnPos = GetRandomSpawnPositionOutsideCamera();

            // Instanciar flecha
            GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
            ArrowEnemy arrowEnemy = arrowObj.GetComponent<ArrowEnemy>();
            arrowEnemy.Initialize(playerTransform);
            arrows.Add(arrowEnemy);

            // Instanciar alerta en borde cerca de la flecha
            Vector3 alertPos = GetAlertPositionNear(spawnPos);
            GameObject alertObj = Instantiate(alertPrefab, alertPos, Quaternion.identity);

            AlertBlink alertBlink = alertObj.GetComponent<AlertBlink>();
            alertBlink.OnBlinkComplete.AddListener(() => OnAlertComplete(arrowEnemy));
            alerts.Add(alertBlink);
        }
    }

    Vector3 GetRandomSpawnPositionOutsideCamera()
    {
        // Obtener los límites de la cámara en world coordinates
        Vector3 camPos = mainCamera.transform.position;
        float camHeight = 2f * mainCamera.orthographicSize;
        float camWidth = camHeight * mainCamera.aspect;

        // Elegir un borde aleatorio: 0 = arriba, 1 = derecha, 2 = abajo, 3 = izquierda
        int edge = Random.Range(0, 4);
        Vector3 pos = Vector3.zero;

        switch (edge)
        {
            case 0: // arriba
                pos = new Vector3(
                    Random.Range(camPos.x - camWidth / 2f, camPos.x + camWidth / 2f),
                    camPos.y + camHeight / 2f + spawnDistance,
                    0);
                break;
            case 1: // derecha
                pos = new Vector3(
                    camPos.x + camWidth / 2f + spawnDistance,
                    Random.Range(camPos.y - camHeight / 2f, camPos.y + camHeight / 2f),
                    0);
                break;
            case 2: // abajo
                pos = new Vector3(
                    Random.Range(camPos.x - camWidth / 2f, camPos.x + camWidth / 2f),
                    camPos.y - camHeight / 2f - spawnDistance,
                    0);
                break;
            case 3: // izquierda
                pos = new Vector3(
                    camPos.x - camWidth / 2f - spawnDistance,
                    Random.Range(camPos.y - camHeight / 2f, camPos.y + camHeight / 2f),
                    0);
                break;
        }
        return pos;
    }

    Vector3 GetAlertPositionNear(Vector3 spawnPos)
    {
        Vector3 camPos = mainCamera.transform.position;
        float camHeight = 2f * mainCamera.orthographicSize;
        float camWidth = camHeight * mainCamera.aspect;

        // Limites de cámara
        float left = camPos.x - camWidth / 2f;
        float right = camPos.x + camWidth / 2f;
        float top = camPos.y + camHeight / 2f;
        float bottom = camPos.y - camHeight / 2f;

        Vector3 alertPos = spawnPos;

        // Si la flecha está fuera arriba, alerta en top
        if (spawnPos.y > top)
        {
            alertPos.y = top - 0.5f; // un poco dentro de cámara
            alertPos.x = Mathf.Clamp(spawnPos.x, left + 0.5f, right - 0.5f);
        }
        // Si fuera abajo
        else if (spawnPos.y < bottom)
        {
            alertPos.y = bottom + 0.5f;
            alertPos.x = Mathf.Clamp(spawnPos.x, left + 0.5f, right - 0.5f);
        }
        // Si fuera derecha
        else if (spawnPos.x > right)
        {
            alertPos.x = right - 0.5f;
            alertPos.y = Mathf.Clamp(spawnPos.y, bottom + 0.5f, top - 0.5f);
        }
        // Si fuera izquierda
        else if (spawnPos.x < left)
        {
            alertPos.x = left + 0.5f;
            alertPos.y = Mathf.Clamp(spawnPos.y, bottom + 0.5f, top - 0.5f);
        }

        alertPos.z = 0; // asegurarse que esté en la capa correcta

        return alertPos;
    }

    void OnAlertComplete(ArrowEnemy arrow)
    {
        // Cuando el alert terminó, la flecha dispara
        arrow.Shoot();

        // Podrías también destruir o desactivar el alert aquí si quieres
    }
}
