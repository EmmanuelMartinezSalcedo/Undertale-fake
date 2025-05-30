using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Net.Http;

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
        Debug.Log($"Server started! Players should connect to:");
        Debug.Log($"IP: {publicIP}");
        Debug.Log($"Port: {port}");
        Debug.Log("IMPORTANT: Make sure port {port} is forwarded on your router!");
    }

    private async void GetPublicIP()
    {
        try
        {
            using (var client = new HttpClient())
            {
                publicIP = await client.GetStringAsync("https://api.ipify.org");
                Debug.Log($"Public IP: {publicIP}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Could not get public IP: {e.Message}");
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
