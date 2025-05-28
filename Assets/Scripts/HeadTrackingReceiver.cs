using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;
using System.Collections.Generic;

[Serializable]
public class InitData
{
    public bool init;
    public int frame_width;
    public int frame_height;
}

[System.Serializable]
public class HeadData
{
    public HeadPosition head_position;
    public string frame_data;
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
    public SpriteRenderer backgroundSpriteRenderer;
    public PlayerController player;
    public Camera mainCamera;
    public SpawnArrowsController spawnArrowsController;

    [Header("Configuración")]
    public float smoothingFactor = 0.8f;

    private bool isInitialized = false;

    private bool initDataReceived = false;
    private int initFrameWidth = 0;
    private int initFrameHeight = 0;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isConnected = false;
    private bool shouldStop = false;

    private Texture2D webcamTexture;
    private HeadData latestHeadData;
    private bool hasNewData = false;
    private Vector2 smoothedPosition;
    private Vector2 targetPosition;

    private StringBuilder messageBuffer = new StringBuilder();

    void Start()
    {
        webcamTexture = new Texture2D(2, 2);
        ConnectToServer();
    }

    void ConnectToServer()
    {
        try
        {
            tcpClient = new TcpClient(serverIP, serverPort);
            stream = tcpClient.GetStream();
            isConnected = true;

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
        byte[] buffer = new byte[1024 * 1024];

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
                Thread.Sleep(1);
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

            try
            {
                var tempObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonMessage);
                if (tempObj != null && tempObj.ContainsKey("init") && (bool)tempObj["init"] == true)
                {
                    InitData initData = JsonConvert.DeserializeObject<InitData>(jsonMessage);
                    OnInitDataReceived(initData);
                }
                else
                {
                    HeadData headData = JsonConvert.DeserializeObject<HeadData>(jsonMessage);
                    latestHeadData = headData;
                    hasNewData = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parseando JSON: {e.Message}");
            }
        }

        messageBuffer.Clear();
        messageBuffer.Append(bufferContent);
    }

    void OnInitDataReceived(InitData initData)
    {
        initFrameWidth = initData.frame_width;
        initFrameHeight = initData.frame_height;
        initDataReceived = true;
    }

    void Update()
    {
        if (initDataReceived && !isInitialized && initFrameWidth > 0 && initFrameHeight > 0 && backgroundSpriteRenderer != null)
        {
            Camera cam = mainCamera != null ? mainCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("No se encontró la cámara asignada ni la Main Camera");
                return;
            }

            // Ya no hacemos cálculo ni escalado del sprite para mantener su tamaño original
            //backgroundSpriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            //backgroundSpriteRenderer.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0);

            isInitialized = true;
            initDataReceived = false;

            if (spawnArrowsController != null)
            {
                spawnArrowsController.InitializeWithBackground(backgroundSpriteRenderer);
            }
        }

        if (hasNewData && latestHeadData != null)
        {
            ProcessHeadData(latestHeadData);
            hasNewData = false;
        }

        if (player != null && backgroundSpriteRenderer != null)
        {
            //smoothedPosition = Vector2.Lerp(smoothedPosition, targetPosition, smoothingFactor * Time.deltaTime);
            smoothedPosition = targetPosition;
            Vector3 spriteScale = backgroundSpriteRenderer.transform.localScale;
            float spriteWidth = backgroundSpriteRenderer.sprite.bounds.size.x * spriteScale.x;
            float spriteHeight = backgroundSpriteRenderer.sprite.bounds.size.y * spriteScale.y;

            float clampedX = Mathf.Clamp(smoothedPosition.x, 0f, 1f);
            float clampedY = Mathf.Clamp(1f - smoothedPosition.y, 0f, 1f);

            float worldX = backgroundSpriteRenderer.transform.position.x - spriteWidth / 2f + clampedX * spriteWidth;
            float worldY = backgroundSpriteRenderer.transform.position.y - spriteHeight / 2f + clampedY * spriteHeight;

            Vector3 worldPos = new Vector3(worldX, worldY, 0f);

            player.SetTargetPosition(worldPos); // Aquí es donde se actualiza la posición
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

                // NO modificar la escala aquí
                // backgroundSpriteRenderer.transform.localScale = ...  <-- ELIMINAR o COMENTAR
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
            receiveThread.Join(1000);
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