using Mirror;
using UnityEngine;

public class MainNetworkManager : NetworkManager
{
    private int nextPlayerId = 0;

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Transform startPos = GetStartPosition();
        GameObject playerObj = startPos != null
            ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
            : Instantiate(playerPrefab);

        MainPlayerController player = playerObj.GetComponent<MainPlayerController>();
        if (player != null)
        {
            // Assign a unique server-side player ID
            player.playerID = nextPlayerId++;

            if (!MainPlayerController.allPlayers.Contains(player))
                MainPlayerController.allPlayers.Add(player);

            if (!MainPlayerController.playersReady.ContainsKey(player.playerID))
                MainPlayerController.playersReady[player.playerID] = false;

            Debug.Log($"[Server] Added Player {player.playerID}");
        }

        NetworkServer.AddPlayerForConnection(conn, playerObj);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn.identity != null)
        {
            MainPlayerController player = conn.identity.GetComponent<MainPlayerController>();
            if (player != null)
            {
                MainPlayerController.allPlayers.Remove(player);
                if (MainPlayerController.playersReady.ContainsKey(player.playerID))
                    MainPlayerController.playersReady.Remove(player.playerID);

                Debug.Log($"[Server] Player {player.playerID} disconnected");
            }
        }

        base.OnServerDisconnect(conn);
    }

    public override void OnStopServer()
    {
        MainPlayerController.allPlayers.Clear();
        MainPlayerController.playersReady.Clear();
        nextPlayerId = 0;
        base.OnStopServer();
        Debug.Log("[Server] Server stopped and reset player data");
    }
}
