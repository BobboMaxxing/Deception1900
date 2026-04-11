using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Basic AI bot that runs server-side. Picks a country, builds tanks,
/// moves toward supply centers, and auto-confirms each turn.
/// </summary>
public class AIBotController : MonoBehaviour
{
    public static List<AIBotController> allBots = new List<AIBotController>();

    [Header("Bot Settings")]
    public float thinkDelay = 1.5f;
    public float buildDelay = 0.8f;

    private MainPlayerController player;
    private bool hasPicked;
    private bool isBuildingDone = true;

    public void Initialize(MainPlayerController botPlayer)
    {
        player = botPlayer;
        allBots.Add(this);
        StartCoroutine(BotLoop());
    }

    void OnDestroy()
    {
        allBots.Remove(this);
    }

    private IEnumerator BotLoop()
    {
        // Wait a moment for everything to initialize
        yield return new WaitForSeconds(1f);

        // === PICK COUNTRY ===
        yield return PickCountry();

        // === MAIN LOOP: each turn, give orders and confirm ===
        while (player != null)
        {
            yield return new WaitForSeconds(thinkDelay);

            // If in build phase, build tanks
            if (IsBuildPhaseActive())
            {
                yield return DoBuildPhase();
                continue;
            }

            // Give move orders to all our units
            GiveOrders();

            // Confirm moves
            yield return new WaitForSeconds(0.5f);
            ConfirmMoves();

            // Wait for next turn
            yield return new WaitForSeconds(2f);
        }
    }

    private IEnumerator PickCountry()
    {
        yield return new WaitForSeconds(thinkDelay);

        // Find an unclaimed starter country
        Country[] allCountries = Object.FindObjectsByType<Country>(FindObjectsSortMode.None);
        List<Country> available = new List<Country>();

        for (int i = 0; i < allCountries.Length; i++)
        {
            if (allCountries[i].CanBeSelected())
                available.Add(allCountries[i]);
        }

        if (available.Count == 0)
        {
            Debug.LogWarning($"[AIBot {player.playerID}] No countries available to pick!");
            yield break;
        }

        // Pick a random available country
        Country chosen = available[Random.Range(0, available.Count)];

        // Assign ownership server-side
        List<Country> allOwned = chosen.GetAllSelectableCountries();
        for (int i = 0; i < allOwned.Count; i++)
            allOwned[i].SetOwner(player.playerID);

        player.chosenCountry = chosen.tag;
        player.hasChosenCountry = true;
        player.playerColor = chosen.countryColor;

        Debug.Log($"[AIBot {player.playerID}] Picked country: {chosen.countryName}");

        // Start initial build phase
        MainGameManager.Instance.StartInitialBuildPhaseForPlayer(player.playerID);

        yield return new WaitForSeconds(0.5f);
        yield return DoBuildPhase();

        hasPicked = true;
    }

    private IEnumerator DoBuildPhase()
    {
        yield return new WaitForSeconds(buildDelay);

        int safety = 20;
        while (MainGameManager.Instance.HasBuildCredits(player.playerID) && safety > 0)
        {
            safety--;

            // Find our owned countries to build on
            Country[] allCountries = Object.FindObjectsByType<Country>(FindObjectsSortMode.None);
            Country buildTarget = null;

            for (int i = 0; i < allCountries.Length; i++)
            {
                Country c = allCountries[i];
                if (c.ownerID != player.playerID) continue;
                if (c.isOcean) continue;

                // Check if it has a spawn point
                if (MainUnitManager.Instance.HasSpawnPoint(c.tag, UnitType.Land, null))
                {
                    buildTarget = c;
                    break;
                }
            }

            if (buildTarget == null)
            {
                Debug.Log($"[AIBot {player.playerID}] No valid tile to build on, passing.");
                MainGameManager.Instance.ServerPassBuildPhase(player.playerID);
                yield break;
            }

            // Build a tank
            MainGameManager.Instance.ServerTrySpawnUnit(
                player.playerID,
                buildTarget.tag,
                player.playerColor,
                UnitType.Land,
                player.connectionToClient
            );

            Debug.Log($"[AIBot {player.playerID}] Built tank on {buildTarget.countryName}");
            yield return new WaitForSeconds(buildDelay);
        }

        // Pass if still in build phase
        if (MainGameManager.Instance.IsPlayerBuilding(player.playerID))
            MainGameManager.Instance.ServerPassBuildPhase(player.playerID);
    }

    private void GiveOrders()
    {
        MainUnit[] allUnits = Object.FindObjectsByType<MainUnit>(FindObjectsSortMode.None);
        List<MainUnit> myUnits = new List<MainUnit>();

        for (int i = 0; i < allUnits.Length; i++)
        {
            if (allUnits[i].ownerID == player.playerID)
                myUnits.Add(allUnits[i]);
        }

        if (myUnits.Count == 0) return;

        // Find all supply centers we don't own
        Country[] allCountries = Object.FindObjectsByType<Country>(FindObjectsSortMode.None);
        List<Country> targetSupplyCenters = new List<Country>();

        for (int i = 0; i < allCountries.Length; i++)
        {
            Country c = allCountries[i];
            if (c.isSupplyCenter && c.ownerID != player.playerID && !c.isOcean)
                targetSupplyCenters.Add(c);
        }

        // For each unit, try to move toward the closest unowned supply center
        for (int u = 0; u < myUnits.Count; u++)
        {
            MainUnit unit = myUnits[u];
            if (unit.unitType == UnitType.Boat) continue; // skip boats for simplicity

            Country fromCountry = FindCountryByTag(unit.currentCountry);
            if (fromCountry == null) continue;

            // Find best adjacent tile to move to
            Country bestTarget = null;
            float bestDist = float.MaxValue;

            for (int a = 0; a < fromCountry.adjacentCountries.Count; a++)
            {
                Country adj = fromCountry.adjacentCountries[a];
                if (adj == null || adj.isOcean) continue;

                // Prefer supply centers we don't own
                if (adj.isSupplyCenter && adj.ownerID != player.playerID)
                {
                    bestTarget = adj;
                    break; // take it immediately
                }

                // Otherwise move toward closest unowned supply center
                for (int s = 0; s < targetSupplyCenters.Count; s++)
                {
                    float dist = Vector3.Distance(adj.centerWorldPos, targetSupplyCenters[s].centerWorldPos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = adj;
                    }
                }
            }

            if (bestTarget == null) continue;

            // Set the move order directly on server
            unit.currentOrder = new PlayerUnitOrder
            {
                orderType = UnitOrderType.Move,
                targetCountry = bestTarget.tag,
                targetPosition = bestTarget.centerWorldPos
            };

            Debug.Log($"[AIBot {player.playerID}] Unit on {unit.currentCountry} → {bestTarget.countryName}");
        }
    }

    private void ConfirmMoves()
    {
        if (player == null) return;

        if (!MainPlayerController.playersReady.ContainsKey(player.playerID))
            MainPlayerController.playersReady[player.playerID] = false;

        MainPlayerController.playersReady[player.playerID] = true;

        Debug.Log($"[AIBot {player.playerID}] Confirmed moves.");

        // Check if all players are ready
        bool allReady = true;
        foreach (var kv in MainPlayerController.playersReady)
        {
            if (!kv.Value) { allReady = false; break; }
        }

        if (allReady)
        {
            MainUnitManager.Instance.ExecuteTurnServer();
            MainGameManager.Instance?.NextTurnServer();

            foreach (var p in MainPlayerController.allPlayers)
                p.RpcResetReady();
        }
    }

    private bool IsBuildPhaseActive()
    {
        return MainGameManager.Instance != null &&
               MainGameManager.Instance.IsPlayerBuilding(player.playerID);
    }

    private Country FindCountryByTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        GameObject obj = GameObject.FindWithTag(tag);
        if (obj == null) return null;
        return obj.GetComponent<Country>();
    }
}
