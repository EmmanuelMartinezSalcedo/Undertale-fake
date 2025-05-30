using UnityEngine;
using Mirror;

public class WebcamBackgroundControl : NetworkBehaviour
{
    [Header("References")]
    public GameObject webcamBackground;

    void Start()
    {
        if (webcamBackground != null)
        {
            webcamBackground.SetActive(isLocalPlayer);
        }
    }

    public override void OnStartLocalPlayer()
    {
        if (webcamBackground != null)
        {
            webcamBackground.SetActive(true);
        }
    }

    public override void OnStartClient()
    {
        if (!isLocalPlayer && webcamBackground != null)
        {
            webcamBackground.SetActive(false);
        }
    }
}