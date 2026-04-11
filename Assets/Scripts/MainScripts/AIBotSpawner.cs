using System.Collections;
using UnityEngine;
using Mirror;

/// <summary>
/// Attach to the same GameObject as your NetworkManager in the game scene.
/// On server start, reads GameModeMenu.BotCount and spawns that many AI bots.
/// Each bot gets a fake MainPlayerController registered in allPlayers/playersReady.
/// </summary>
public class AIBotSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player prefab from NetworkManager (needs MainPlayerController)")]
    public GameObject playerPrefab;

    [Header("Timing")]
    public float spawnDelay = 1.5f;

    private bool hasSpawned;

    void Start()
    {
        // Only spawn bots on the server/host
        if (!NetworkServer.active) return;
        if (hasSpawned) return;
        if (!GameModeMenu.IsSinglePlayer) return;
        if (GameModeMenu.BotCount <= 0) return;

        hasSpawned = true;
        StartCoroutine(SpawnBots());
    }

    private IEnumerator SpawnBots()
    {
        yield return new WaitForSeconds(spawnDelay);

        // Get the network manager to find the next player ID
        MainNetworkManager netManager = NetworkManager.singleton as MainNetworkManager;

        for (int i = 0; i < GameModeMenu.BotCount; i++)
        {
            // Create a bot player object (server-only, no client connection)
            GameObject botObj;
            if (playerPrefab != null)
                botObj = Instantiate(playerPrefab);
            else
                botObj = Instantiate(NetworkManager.singleton.playerPrefab);

            MainPlayerController botPlayer = botObj.GetComponent<MainPlayerController>();
            if (botPlayer == null)
            {
                Debug.LogError("[AIBotSpawner] Player prefab has no MainPlayerController!");
                Destroy(botObj);
                continue;
            }

            // Assign a unique player ID (starting after real players)
            int botId = 100 + i;
            botPlayer.playerID = botId;

            // Register in the player tracking systems
            if (!MainPlayerController.allPlayers.Contains(botPlayer))
                MainPlayerController.allPlayers.Add(botPlayer);

            if (!MainPlayerController.playersReady.ContainsKey(botId))
                MainPlayerController.playersReady[botId] = false;

            // Spawn on network so units/orders work
            NetworkServer.Spawn(botObj);

            // Attach the AI controller
            AIBotController bot = botObj.AddComponent<AIBotController>();
            bot.Initialize(botPlayer);

            Debug.Log($"[AIBotSpawner] Spawned AI Bot {i + 1} with playerID {botId}");

            yield return new WaitForSeconds(0.5f);
        }
    }
}
