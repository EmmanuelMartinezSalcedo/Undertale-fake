using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
public class Position
{
    public float normalized_x;
    public float normalized_y;
}

[System.Serializable]
public class BodyHeadData
{
    public HeadPosition head_position;
    public string frame_data;
}

[System.Serializable]
public class HandsPositions
{
    public Position left;
    public Position right;
}

[System.Serializable]
public class BodyHandData
{
    public HandsPositions hand_positions;
    public string frame_data;
}

public class BodyTrackingReceiver : MonoBehaviour
{
    [Header("Configuración de Red")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 12345;

    [Header("Elementos de escena")]
    public SpriteRenderer backgroundSpriteRenderer;
    public PlayerController player;
    public Transform leftHandObject;
    public Transform rightHandObject;
    public Camera mainCamera;

    [Header("Configuración")]
    public float smoothingFactor = 0.8f;
    public bool useHeadTracking = true;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isConnected = false;
    private bool shouldStop = false;

    private Texture2D webcamTexture;
    private StringBuilder messageBuffer = new StringBuilder();

    private BodyHeadData latestHeadData;
    private BodyHandData latestHandData;
    private bool hasNewData = false;

    private Vector2 smoothedLeft;
    private Vector2 smoothedRight;
    private Vector2 targetLeft;
    private Vector2 targetRight;
    private Vector2 targetPosition;

    void Start()
    {
        ConfigureCharacterObjects();
        webcamTexture = new Texture2D(2, 2);
        ConnectToServer();
    }

    void ConfigureCharacterObjects()
    {
        Transform character = transform.Find("Character");
        if (character == null)
        {
            return;
        }

        if (useHeadTracking)
        {
            Transform leftBarrier = character.Find("LeftBarrier");
            Transform rightBarrier = character.Find("RightBarrier");
            if (leftBarrier != null) leftBarrier.gameObject.SetActive(false);
            if (rightBarrier != null) rightBarrier.gameObject.SetActive(false);

            Transform heart = character.Find("Heart");
            if (heart != null) heart.gameObject.SetActive(true);
        }
        else
        {
            Transform heart = character.Find("Heart");
            if (heart != null) heart.gameObject.SetActive(false);

            Transform leftBarrier = character.Find("LeftBarrier");
            Transform rightBarrier = character.Find("RightBarrier");
            if (leftBarrier != null) leftBarrier.gameObject.SetActive(true);
            if (rightBarrier != null) rightBarrier.gameObject.SetActive(true);
        }
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
                if (useHeadTracking)
                    latestHeadData = JsonConvert.DeserializeObject<BodyHeadData>(jsonMessage);
                else
                    latestHandData = JsonConvert.DeserializeObject<BodyHandData>(jsonMessage);
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
        if (!hasNewData) return;

        if (useHeadTracking && latestHeadData != null)
        {
            ProcessHeadData(latestHeadData);
        }
        else if (!useHeadTracking && latestHandData != null)
        {
            ProcessHandData(latestHandData);
        }
        hasNewData = false;
    }

    void ProcessHeadData(BodyHeadData data)
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
            }
            catch (Exception e)
            {
                Debug.LogError($"Error cargando imagen: {e.Message}");
            }
        }

        if (data.head_position != null && player != null)
        {
            targetPosition = new Vector2(
                data.head_position.normalized_x,
                data.head_position.normalized_y
            );

            Vector3 spriteScale = backgroundSpriteRenderer.transform.localScale;
            float spriteWidth = backgroundSpriteRenderer.sprite.bounds.size.x * spriteScale.x;
            float spriteHeight = backgroundSpriteRenderer.sprite.bounds.size.y * spriteScale.y;

            float clampedX = Mathf.Clamp(targetPosition.x, 0f, 1f);
            float clampedY = Mathf.Clamp(1f - targetPosition.y, 0f, 1f);

            float worldX = backgroundSpriteRenderer.transform.position.x - spriteWidth / 2f + clampedX * spriteWidth;
            float worldY = backgroundSpriteRenderer.transform.position.y - spriteHeight / 2f + clampedY * spriteHeight;

            Vector3 worldPos = new Vector3(worldX, worldY, 0f);
            player.SetTargetPosition(worldPos);
        }
    }

    void ProcessHandData(BodyHandData data)
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
            }
            catch (Exception e)
            {
                Debug.LogError($"Error cargando imagen: {e.Message}");
            }
        }

        if (data.hand_positions != null)
        {
            if (data.hand_positions.left != null)
            {
                targetLeft = new Vector2(
                    data.hand_positions.left.normalized_x,
                    data.hand_positions.left.normalized_y
                );
            }

            if (data.hand_positions.right != null)
            {
                targetRight = new Vector2(
                    data.hand_positions.right.normalized_x,
                    data.hand_positions.right.normalized_y
                );
            }

            smoothedLeft = Vector2.Lerp(smoothedLeft, targetLeft, 1f - smoothingFactor);
            smoothedRight = Vector2.Lerp(smoothedRight, targetRight, 1f - smoothingFactor);

            if (leftHandObject != null)
                leftHandObject.position = NormalizedToWorld(smoothedLeft);
            if (rightHandObject != null)
                rightHandObject.position = NormalizedToWorld(smoothedRight);
        }
    }

    Vector3 NormalizedToWorld(Vector2 normalized)
    {
        if (backgroundSpriteRenderer == null) return Vector3.zero;

        Vector3 scale = backgroundSpriteRenderer.transform.localScale;
        float width = backgroundSpriteRenderer.sprite.bounds.size.x * scale.x;
        float height = backgroundSpriteRenderer.sprite.bounds.size.y * scale.y;

        float x = backgroundSpriteRenderer.transform.position.x - width / 2f + Mathf.Clamp01(normalized.x) * width;
        float y = backgroundSpriteRenderer.transform.position.y - height / 2f + Mathf.Clamp01(1f - normalized.y) * height;

        return new Vector3(x, y, 0f);
    }

    void OnApplicationQuit() => Disconnect();
    void OnDestroy() => Disconnect();

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

        stream?.Close();
        tcpClient?.Close();

        if (webcamTexture != null)
        {
            DestroyImmediate(webcamTexture);
        }
    }
}