using Mirror;
using UnityEngine;

/// <summary>
/// Attach this to any GameObject in your MainMenu scene.
/// On Awake it destroys any leftover NetworkManager so a fresh one
/// spawns when the next game starts.
/// </summary>
public class NetworkCleanup : MonoBehaviour
{
    void Awake()
    {
        if (NetworkManager.singleton != null)
        {
            if (NetworkServer.active && NetworkClient.isConnected)
                NetworkManager.singleton.StopHost();
            else if (NetworkClient.isConnected)
                NetworkManager.singleton.StopClient();
            else if (NetworkServer.active)
                NetworkManager.singleton.StopServer();

            Destroy(NetworkManager.singleton.gameObject);
        }

        // Clear any stale player data
        MainPlayerController.allPlayers.Clear();
        MainPlayerController.playersReady.Clear();
    }
}
