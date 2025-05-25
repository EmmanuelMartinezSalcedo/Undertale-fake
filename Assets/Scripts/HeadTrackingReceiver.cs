using System;
using System.Collections;
using System.Collections.Generic;
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
    public int frame_id;
    public float timestamp;
    public HeadPosition head_position;
    public string frame_data;
    public int frame_width;
    public int frame_height;
}

[System.Serializable]
public class HeadPosition
{
    public float x;
    public float y;
    public float z;  // Profundidad
    public float normalized_x;
    public float normalized_y;
}

public class HeadTrackingReceiver : MonoBehaviour
{
    [Header("Configuración de Red")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 12345;

    [Header("UI Elements")]
    public RawImage backgroundImage;  // Para mostrar la webcam
    public Transform headIndicator;   // GameObject que representa la posición de la cabeza
    public TextMeshProUGUI statusText;           // Texto de estado
    public TextMeshProUGUI positionText;         // Texto que muestra la posición

    [Header("Configuración")]
    public bool showDebugInfo = true;
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
        // Configurar UI inicial
        if (statusText != null)
            statusText.text = "Conectando...";

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

            if (statusText != null)
                statusText.text = "Conectado";

            // Iniciar hilo para recibir datos
            receiveThread = new Thread(ReceiveData);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error conectando al servidor: {e.Message}");
            if (statusText != null)
                statusText.text = $"Error: {e.Message}";
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

        // Suavizar movimiento del indicador de cabeza
        if (headIndicator != null)
        {
            smoothedPosition = Vector2.Lerp(smoothedPosition, targetPosition, smoothingFactor * Time.deltaTime);

            // Convertir coordenadas normalizadas a coordenadas de pantalla
            Vector3 screenPos = new Vector3(
                smoothedPosition.x * Screen.width,
                (1 - smoothedPosition.y) * Screen.height, // Invertir Y
                0
            );

            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10));
            headIndicator.position = worldPos;
        }
    }

    void ProcessHeadData(HeadData data)
    {
        // Actualizar imagen de fondo (webcam)
        if (!string.IsNullOrEmpty(data.frame_data) && backgroundImage != null)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(data.frame_data);
                webcamTexture.LoadImage(imageBytes);
                backgroundImage.texture = webcamTexture;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error cargando imagen: {e.Message}");
            }
        }

        // Actualizar posición de la cabeza
        if (data.head_position != null)
        {
            targetPosition = new Vector2(
                data.head_position.normalized_x,
                data.head_position.normalized_y
            );

            // Actualizar texto de posición
            if (positionText != null && showDebugInfo)
            {
                positionText.text = $"Nariz: ({data.head_position.x:F1}, {data.head_position.y:F1}, {data.head_position.z:F3})\n" +
                                  $"Normalizada: ({data.head_position.normalized_x:F3}, {data.head_position.normalized_y:F3})";
            }
        }

        // Actualizar texto de estado
        if (statusText != null && showDebugInfo)
        {
            statusText.text = $"Frame: {data.frame_id} | {data.frame_width}x{data.frame_height}";
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