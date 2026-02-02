using Mirror;
using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class MainGameManager : NetworkBehaviour
{
    public static MainGameManager Instance;

    [SyncVar] public int currentYear = 1901;
    [SyncVar] public string currentSeason = "Spring";

    private string[] seasons = { "Spring", "Autumn" };
    private int currentSeasonIndex = 0;

    [Header("UI")]
    public TMP_Text seasonText;
    public TMP_Text yearText;
    public TMP_Text winText;

    private Dictionary<int, int> lastSupplyCounts = new Dictionary<int, int>();
    private Dictionary<int, int> buildCredits = new Dictionary<int, int>();
    private HashSet<int> playersBuilding = new HashSet<int>();

    public bool IsPlayerBuilding(int playerId)
    {
        return playersBuilding.Contains(playerId);
    }

    void Awake() => Instance = this;
    void Start() => UpdateSeasonUI();

    [Server]
    public void NextTurnServer()
    {
        currentSeasonIndex++;
        if (currentSeasonIndex >= seasons.Length)
        {
            currentSeasonIndex = 0;
            currentYear++;

            CheckSupplyChangesAndGrantBuilds();
        }

        currentSeason = seasons[currentSeasonIndex];
        RpcUpdateSeasonUI(currentSeason, currentYear);
    }

    [ClientRpc]
    private void RpcUpdateSeasonUI(string season, int year)
    {
        currentSeason = season;
        currentYear = year;
        UpdateSeasonUI();
    }

    private void UpdateSeasonUI()
    {
        if (seasonText != null) seasonText.text = $"Season: {currentSeason}";
        if (yearText != null) yearText.text = $"Year: {currentYear}";
    }

    [Server]
    public void CheckWinConditionServer()
    {
        Country[] allCountries = GameObject.FindObjectsOfType<Country>();
        List<Country> supplyCenters = new List<Country>();

        foreach (var country in allCountries)
            if (country.isSupplyCenter)
                supplyCenters.Add(country);

        if (supplyCenters.Count == 0) return;

        Dictionary<int, int> ownershipCounts = new Dictionary<int, int>();
        foreach (var c in supplyCenters)
        {
            if (c.ownerID == -1) continue;
            if (!ownershipCounts.ContainsKey(c.ownerID))
                ownershipCounts[c.ownerID] = 0;
            ownershipCounts[c.ownerID]++;
        }

        int totalCenters = supplyCenters.Count;
        float required = totalCenters * 0.75f;

        foreach (var kvp in ownershipCounts)
        {
            int playerId = kvp.Key;
            int owned = kvp.Value;
            if (owned >= required)
            {
                RpcShowWinText($"Player {playerId} Wins!\nOwned {owned}/{totalCenters} supply centers.");

                foreach (var p in MainPlayerController.allPlayers)
                    if (p != null) p.canIssueOrders = false;
                return;
            }
        }
    }

    [ClientRpc]
    private void RpcShowWinText(string message)
    {
        if (winText != null)
        {
            winText.text = message;
            winText.gameObject.SetActive(true);
        }
    }

    [Server]
    private void CheckSupplyChangesAndGrantBuilds()
    {
        Country[] allCountries = GameObject.FindObjectsOfType<Country>();
        Dictionary<int, int> currentSupplyCounts = new Dictionary<int, int>();

        foreach (var c in allCountries)
            if (c.isSupplyCenter && c.ownerID != -1)
            {
                if (!currentSupplyCounts.ContainsKey(c.ownerID))
                    currentSupplyCounts[c.ownerID] = 0;
                currentSupplyCounts[c.ownerID]++;
            }

        HashSet<int> playerIds = new HashSet<int>();
        foreach (var p in MainPlayerController.allPlayers)
            if (p != null)
                playerIds.Add(p.playerID);

        foreach (var playerId in playerIds)
        {
            int currentCount = currentSupplyCounts.ContainsKey(playerId) ? currentSupplyCounts[playerId] : 0;
            int previousCount = lastSupplyCounts.ContainsKey(playerId) ? lastSupplyCounts[playerId] : 0;
            int gained = currentCount - previousCount;

            if (gained > 0)
            {
                if (!buildCredits.ContainsKey(playerId))
                    buildCredits[playerId] = 0;
                buildCredits[playerId] += gained;
            }
        }

        lastSupplyCounts = currentSupplyCounts;

        foreach (var playerId in playerIds)
            StartBuildPhaseForPlayer(playerId);
    }

    [Server]
    public void StartInitialBuildPhaseForPlayer(int playerId)
    {
        if (!buildCredits.ContainsKey(playerId))
            buildCredits[playerId] = 0;

        buildCredits[playerId] += 3;

        StartBuildPhaseForPlayer(playerId);
    }

    [Server]
    public void StartBuildPhaseForPlayer(int playerId)
    {
        if (playersBuilding.Contains(playerId)) return;

        playersBuilding.Add(playerId);
        TargetPromptForBuild(playerId, buildCredits.ContainsKey(playerId) ? buildCredits[playerId] : 0);
    }

    [Server]
    private void TargetPromptForBuild(int playerId, int buildCount)
    {
        MainPlayerController player = FindPlayerById(playerId);
        if (player != null && player.connectionToClient != null)
        {
            player.TargetStartBuildPhase(player.connectionToClient, buildCount);
        }
    }

    [TargetRpc]
    public void TargetSetupLocalUnit(NetworkConnection target, GameObject unitObj, string countryName)
    {
        unitObj?.GetComponent<MainUnit>()?.SetupLocalVisuals();
    }

    [Server]
    public void ServerTrySpawnUnit(int playerId, string clickedTag, Color playerColor, UnitType unitType, NetworkConnection requester)
    {
        MainPlayerController player = FindPlayerById(playerId);
        int creditsNow = buildCredits.ContainsKey(playerId) ? buildCredits[playerId] : 0;

        if (player == null || requester == null)
            return;

        if (creditsNow <= 0)
        {
            player.TargetBuildResult(requester, false, creditsNow, "No build points.");
            return;
        }

        GameObject clickedObj = GameObject.FindGameObjectWithTag(clickedTag);
        if (clickedObj == null)
        {
            player.TargetBuildResult(requester, false, creditsNow, "Tile not found (tag mismatch).");
            return;
        }

        Country clickedCountry = clickedObj.GetComponent<Country>();
        if (clickedCountry == null)
        {
            player.TargetBuildResult(requester, false, creditsNow, "Tile has no Country component.");
            return;
        }

        string ownerCountryTag = clickedTag;
        string requiredSpawnTileTag = null;

        if (unitType == UnitType.Boat && clickedCountry.isOcean)
        {
            bool found = false;

            foreach (var adj in clickedCountry.adjacentCountries)
            {
                if (adj == null) continue;
                if (adj.ownerID != playerId) continue;

                string candidateOwnerTag = adj.gameObject.tag;
                if (!MainUnitManager.Instance.HasSpawnPoint(candidateOwnerTag, UnitType.Boat, clickedTag))
                    continue;

                ownerCountryTag = candidateOwnerTag;
                requiredSpawnTileTag = clickedTag;
                found = true;
                break;
            }

            if (!found)
            {
                player.TargetBuildResult(requester, false, creditsNow, "No valid boat spawnpoint for that ocean from your coasts.");
                return;
            }
        }
        else
        {
            if (clickedCountry.ownerID != playerId)
            {
                player.TargetBuildResult(requester, false, creditsNow, "You do not own that tile.");
                return;
            }
        }

        if (unitType == UnitType.Plane)
        {
            if (!clickedCountry.isAirfield)
            {
                player.TargetBuildResult(requester, false, creditsNow, "Plane requires an airfield tile.");
                return;
            }

            Country[] allCountries = GameObject.FindObjectsOfType<Country>();
            int totalCenters = 0;
            int ownedCenters = 0;

            foreach (var c in allCountries)
            {
                if (!c.isSupplyCenter) continue;
                totalCenters++;
                if (c.ownerID == playerId) ownedCenters++;
            }

            int required = Mathf.CeilToInt(totalCenters / 3f);
            if (ownedCenters < required)
            {
                player.TargetBuildResult(requester, false, creditsNow, "Need at least 1/3 of supply centers to build planes.");
                return;
            }
        }

        GameObject unitObj = MainUnitManager.Instance.SpawnUnitsForCountryServer(ownerCountryTag, playerId, playerColor, 1, unitType, requiredSpawnTileTag);
        if (unitObj == null)
        {
            player.TargetBuildResult(requester, false, creditsNow, "No valid spawn point for that unit type on that tile.");
            return;
        }

        buildCredits[playerId] = creditsNow - 1;
        int creditsAfter = buildCredits[playerId];

        TargetSetupLocalUnit(requester, unitObj, ownerCountryTag);

        player.TargetBuildResult(requester, true, creditsAfter, "Built.");

        if (creditsAfter <= 0)
            FinishBuildPhaseForPlayer(playerId);
    }

    [Server]
    public void FinishBuildPhaseForPlayer(int playerId)
    {
        playersBuilding.Remove(playerId);

        if (playersBuilding.Count == 0)
        {
            foreach (var player in MainPlayerController.allPlayers)
                player.RpcResetReady();
        }
    }

    [Server]
    public void ServerPassBuildPhase(int playerId)
    {
        FinishBuildPhaseForPlayer(playerId);
    }

    public bool HasBuildCredits(int playerId)
    {
        return buildCredits.ContainsKey(playerId) && buildCredits[playerId] > 0;
    }

    private MainPlayerController FindPlayerById(int playerId)
    {
        foreach (var player in MainPlayerController.allPlayers)
            if (player != null && player.playerID == playerId)
                return player;
        return null;
    }
}
