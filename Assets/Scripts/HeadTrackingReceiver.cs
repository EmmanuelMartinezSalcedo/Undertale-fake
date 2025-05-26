using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

[System.Serializable]
public class HeadData
{
    public HeadPosition head_position;
    public string frame_data;
    public int frame_width;
    public int frame_height;
}

[System.Serializable]
public class HeadPosition
{
    public float normalized_x;
    public float normalized_y;
}

public class HeadTrackingReceiver : MonoBehaviour
{
    [Header("Configuración de Red")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 12345;

    [Header("Elementos de escena")]
    public SpriteRenderer backgroundSpriteRenderer;  // Reemplazo del RawImage
    public Transform headIndicator;   // GameObject que representa la posición de la cabeza
    public Camera mainCamera;

    [Header("Configuración")]
    public float smoothingFactor = 0.8f;  // Para suavizar el movimiento

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isConnected = false;
    private bool shouldStop = false;

    // Variables para el procesamiento de datos
    private Texture2D webcamTexture;
    private HeadData latestHeadData;
    private bool hasNewData = false;
    private Vector2 smoothedPosition;
    private Vector2 targetPosition;

    // Buffer para recibir datos
    private StringBuilder messageBuffer = new StringBuilder();

    void Start()
    {
        // Inicializar textura para la webcam
        webcamTexture = new Texture2D(2, 2);

        // Conectar al servidor Python
        ConnectToServer();
    }

    void ConnectToServer()
    {
        try
        {
            tcpClient = new TcpClient(serverIP, serverPort);
            stream = tcpClient.GetStream();
            isConnected = true;

            // Iniciar hilo para recibir datos
            receiveThread = new Thread(ReceiveData);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error conectando al servidor: {e.Message}");
        }
    }

    void ReceiveData()
    {
        byte[] buffer = new byte[1024 * 1024]; // Buffer de 1MB

        while (isConnected && !shouldStop)
        {
            try
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        ProcessReceivedData(receivedData);
                    }
                }
                Thread.Sleep(1); // Pequeña pausa para no saturar el CPU
            }
            catch (Exception e)
            {
                Debug.LogError($"Error recibiendo datos: {e.Message}");
                break;
            }
        }
    }

    void ProcessReceivedData(string data)
    {
        messageBuffer.Append(data);
        string bufferContent = messageBuffer.ToString();

        // Procesar mensajes completos
        while (true)
        {
            int colonIndex = bufferContent.IndexOf(':');
            if (colonIndex == -1) break;

            string lengthStr = bufferContent.Substring(0, colonIndex);
            if (!int.TryParse(lengthStr, out int messageLength)) break;

            int totalMessageLength = colonIndex + 1 + messageLength;
            if (bufferContent.Length < totalMessageLength) break;

            string jsonMessage = bufferContent.Substring(colonIndex + 1, messageLength);
            bufferContent = bufferContent.Substring(totalMessageLength);

            // Procesar el mensaje JSON
            try
            {
                HeadData headData = JsonConvert.DeserializeObject<HeadData>(jsonMessage);
                latestHeadData = headData;
                hasNewData = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parseando JSON: {e.Message}");
            }
        }

        messageBuffer.Clear();
        messageBuffer.Append(bufferContent);
    }

    void Update()
    {
        if (hasNewData && latestHeadData != null)
        {
            ProcessHeadData(latestHeadData);
            hasNewData = false;
        }

        if (headIndicator != null && backgroundSpriteRenderer != null)
        {
            smoothedPosition = Vector2.Lerp(smoothedPosition, targetPosition, smoothingFactor * Time.deltaTime);

            // Obtener tamaño del sprite en unidades mundo
            Vector3 spriteScale = backgroundSpriteRenderer.transform.localScale;
            float spriteWidth = backgroundSpriteRenderer.sprite.bounds.size.x * spriteScale.x;
            float spriteHeight = backgroundSpriteRenderer.sprite.bounds.size.y * spriteScale.y;

            // Clamp de la posición normalizada para que se quede dentro del sprite
            float clampedX = Mathf.Clamp(smoothedPosition.x, 0f, 1f);
            float clampedY = Mathf.Clamp(1f - smoothedPosition.y, 0f, 1f);

            // Convertir la posición normalizada clampada a posición en unidades mundo relativas al centro del sprite
            float worldX = backgroundSpriteRenderer.transform.position.x - spriteWidth / 2f + clampedX * spriteWidth;
            float worldY = backgroundSpriteRenderer.transform.position.y - spriteHeight / 2f + clampedY * spriteHeight;

            Vector3 worldPos = new Vector3(worldX, worldY, headIndicator.position.z);

            headIndicator.position = worldPos;
        }
    }


    void ProcessHeadData(HeadData data)
    {
        if (!string.IsNullOrEmpty(data.frame_data) && backgroundSpriteRenderer != null)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(data.frame_data);
                webcamTexture.LoadImage(imageBytes);

                Sprite newSprite = Sprite.Create(webcamTexture,
                    new Rect(0, 0, webcamTexture.width, webcamTexture.height),
                    new Vector2(0.5f, 0.5f));
                backgroundSpriteRenderer.sprite = newSprite;

                Camera cam = mainCamera != null ? mainCamera : Camera.main;
                if (cam == null)
                {
                    Debug.LogWarning("No se encontró la cámara asignada ni la Main Camera");
                    return;
                }

                float camHeight = cam.orthographicSize * 2f;
                float camWidth = camHeight * cam.aspect;

                float pixelsPerUnit = newSprite.pixelsPerUnit;
                float spriteWidth = webcamTexture.width / pixelsPerUnit;
                float spriteHeight = webcamTexture.height / pixelsPerUnit;

                float scaleX = camWidth / spriteWidth;
                float scaleY = camHeight / spriteHeight;

                float scale = Mathf.Min(scaleX, scaleY);

                backgroundSpriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);

                backgroundSpriteRenderer.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error cargando imagen: {e.Message}");
            }
        }

        if (data.head_position != null)
        {
            targetPosition = new Vector2(
                data.head_position.normalized_x,
                data.head_position.normalized_y
            );
        }
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void Disconnect()
    {
        shouldStop = true;
        isConnected = false;

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(1000); // Esperar 1 segundo máximo
            if (receiveThread.IsAlive)
                receiveThread.Abort();
        }

        if (stream != null)
        {
            stream.Close();
            stream = null;
        }

        if (tcpClient != null)
        {
            tcpClient.Close();
            tcpClient = null;
        }

        if (webcamTexture != null)
        {
            DestroyImmediate(webcamTexture);
        }
    }
}