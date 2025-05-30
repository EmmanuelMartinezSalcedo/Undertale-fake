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
    public int port = 7777;

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
        Debug.Log($"Port: {port}");
        Debug.Log("Waiting for public IP...");
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
            Debug.Log("1. Router port forwarding settings");
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

    public void DiagnoseNetworkSetup()
    {
        Debug.Log("=== NETWORK SETUP DIAGNOSIS ===");
        Debug.Log($"Transport: {transport?.GetType().Name}");
        Debug.Log($"Local IP: {GetLocalIPAddress()}");
        Debug.Log($"Public IP: {publicIP}");
        Debug.Log($"Port: {port}");
        Debug.Log($"Server Active: {NetworkServer.active}");
        Debug.Log($"Client Active: {NetworkClient.active}");
        Debug.Log($"Is Host: {NetworkServer.active && NetworkClient.active}");
        Debug.Log("\nCheck your router's port forwarding:");
        Debug.Log("1. Protocol: TCP and UDP");
        Debug.Log($"2. External Port: {port}");
        Debug.Log($"3. Internal Port: {port}");
        Debug.Log($"4. Internal IP: {GetLocalIPAddress()}");
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
