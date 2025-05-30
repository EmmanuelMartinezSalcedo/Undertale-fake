using Mirror;
using UnityEngine;

public class NetworkPlayerManager : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] protected GameObject webcamBackground;
    [SerializeField] protected GameObject characterContainer;

    [SyncVar]
    protected GameNetworkManager.PlayerRole playerRole;

    public virtual void Start()
    {
        if (webcamBackground != null)
        {
            webcamBackground.SetActive(isLocalPlayer);
        }

        SetupCharacterVisibility();
    }

    public void SetPlayerRole(GameNetworkManager.PlayerRole role)
    {
        playerRole = role;
    }

    protected virtual void SetupCharacterVisibility()
    {
        if (characterContainer != null)
        {
            characterContainer.SetActive(playerRole != GameNetworkManager.PlayerRole.Server);
        }
    }

    protected virtual void Update()
    {
        if (!isLocalPlayer) return;

        HandleInput();
    }

    protected virtual void HandleInput()
    {
    }
}