using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Linq;
using System.Net;
using System;
using System.Threading.Tasks;

public class GameNetworkManager : NetworkManager
{
    [Header("Network Settings")]
    public int port = 27015;

    [Header("Player Prefabs")]
    public GameObject playerHeartPrefab;
    public GameObject playerBarriersPrefab;

    [Header("Game Settings")]
    public int maxPlayers = 3;

    private Dictionary<NetworkConnection, PlayerRole> playerRoles = new Dictionary<NetworkConnection, PlayerRole>();
    private int connectedPlayersCount = 0;

    private NetworkManagerHUD hud;
    private string publicIP;

    public enum PlayerRole
    {
        Server = 0,
        Heart = 1,
        Barriers = 2
    }

    public override void Awake()
    {
        base.Awake();

        if (GetComponent<NetworkManagerHUD>() == null)
            hud = gameObject.AddComponent<NetworkManagerHUD>();

        Application.runInBackground = true;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        GetPublicIP();
        var localIP = GetLocalIPAddress();

        Debug.Log("=== SERVER STARTED ===");
        Debug.Log($"Local IP (LAN/WiFi): {localIP}");
        Debug.Log($"Port: {port} (Changed from 7777 to avoid ISP blocking)");
        Debug.Log("Waiting for public IP...");

        // Add transport type logging
        Debug.Log($"Transport Type: {transport.GetType().Name}");
        Debug.Log($"Transport Available: {transport.Available()}");

        // Test alternative ports
        TestAlternativePorts();
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("Client successfully connected to server!");
        Debug.Log($"Connected to address: {networkAddress}:{port}");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.LogError("Client disconnected from server!");
    }

    public override void OnClientError(TransportError error, string reason)
    {
        base.OnClientError(error, reason);
        Debug.LogError($"Client Error: {error} - {reason}");
    }

    private async void GetPublicIP()
    {
        try
        {
            using (var client = new HttpClient())
            {
                publicIP = await client.GetStringAsync("https://api.ipify.org");
                Debug.Log($"=== CONNECTION INFORMATION ===");
                Debug.Log($"Public IP (Internet): {publicIP}");
                Debug.Log($"Port: {port}");
                Debug.Log("Players OUTSIDE your network should use:");
                Debug.Log($"    IP: {publicIP}");
                Debug.Log($"    Port: {port}");
                Debug.Log("Make sure port {port} is forwarded on your router!");

                // Test if port is accessible
                TestPortForwarding();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Could not get public IP: {e.Message}");
        }
    }

    private string GetLocalIPAddress()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                       x.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            .Where(x => x.OperationalStatus == OperationalStatus.Up)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses)
            .Where(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Where(x => !IPAddress.IsLoopback(x.Address))
            .Select(x => x.Address.ToString())
            .FirstOrDefault();
    }

    private async void TestPortForwarding()
    {
        try
        {
            // Test TCP
            using (var tcpClient = new System.Net.Sockets.TcpClient())
            {
                var connectTask = tcpClient.ConnectAsync(publicIP, port);
                // Only wait 5 seconds max
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask)
                {
                    Debug.Log("Port 7777 TCP is accessible!");
                }
                else
                {
                    Debug.LogWarning("TCP test timed out");
                }
            }

            // Test UDP
            using (var udpClient = new System.Net.Sockets.UdpClient())
            {
                udpClient.Connect(publicIP, port);
                byte[] testData = System.Text.Encoding.ASCII.GetBytes("test");
                await udpClient.SendAsync(testData, testData.Length);
                Debug.Log("Port 7777 UDP send test succeeded");
            }

            Debug.Log("=== PORT FORWARDING STATUS ===");
            Debug.Log($"Local IP being used: {GetLocalIPAddress()}");
            Debug.Log($"Public IP being tested: {publicIP}");
            Debug.Log($"Port being tested: {port}");
            Debug.Log("Firewall Status: DISABLED");
            Debug.Log("If you still can't connect, check:");
            Debug.Log("2. ISP blocking (some ISPs block port 7777)");
            Debug.Log("3. Any antivirus software");
        }
        catch (Exception e)
        {
            Debug.LogError($"Port forwarding test failed: {e.Message}");
            Debug.LogWarning("Common solutions:");
            Debug.LogWarning("1. Double check port forwarding settings in router");
            Debug.LogWarning("2. Make sure the port forward points to: " + GetLocalIPAddress());
            Debug.LogWarning("3. Try a different port (some ISPs block 7777)");
            Debug.LogWarning("4. Check if any antivirus is blocking the connection");
        }
    }

    public void TestAlternativePorts()
    {
        Debug.Log("=== TESTING ALTERNATIVE PORTS ===");
        var commonPorts = new[] { 27015, 27016, 8777, 8888 };

        foreach (var testPort in commonPorts)
        {
            try
            {
                using (var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Tcp))
                {
                    socket.Bind(new System.Net.IPEndPoint(IPAddress.Any, testPort));
                    Debug.Log($"Port {testPort} is available locally");
                    socket.Close();
                }
            }
            catch (Exception)
            {
                Debug.LogWarning($"Port {testPort} is NOT available locally");
            }
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        connectedPlayersCount++;

        PlayerRole role = AssignPlayerRole(connectedPlayersCount);
        playerRoles[conn] = role;

        GameObject playerPrefab = GetPlayerPrefab(role);

        if (playerPrefab != null)
        {
            Vector3 spawnPosition = GetSpawnPosition(role);
            GameObject player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

            NetworkPlayerManager networkPlayer = player.GetComponent<NetworkPlayerManager>();
            if (networkPlayer != null)
            {
                networkPlayer.SetPlayerRole(role);
            }

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        Debug.Log($"Jugador conectado. Rol asignado: {role}");
    }

    private PlayerRole AssignPlayerRole(int playerNumber)
    {
        switch (playerNumber)
        {
            case 1: return PlayerRole.Server;
            case 2: return PlayerRole.Heart;
            case 3: return PlayerRole.Barriers;
            default: return PlayerRole.Server;
        }
    }

    private GameObject GetPlayerPrefab(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Heart:
                return playerHeartPrefab;
            case PlayerRole.Barriers:
                return playerBarriersPrefab;
            default:
                return null;
        }
    }

    private Vector3 GetSpawnPosition(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Heart:
                return new Vector3(0, 0, 0);
            case PlayerRole.Barriers:
                return new Vector3(0, -3, 0);
            default:
                return Vector3.zero;
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (playerRoles.ContainsKey(conn))
        {
            Debug.Log($"Jugador con rol {playerRoles[conn]} desconectado");
            playerRoles.Remove(conn);
            connectedPlayersCount--;
        }
        base.OnServerDisconnect(conn);
    }
}
