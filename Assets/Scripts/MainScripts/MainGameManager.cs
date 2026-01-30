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

    public bool IsPlayerBuilding(int playerId) => playersBuilding.Contains(playerId);

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

        foreach (var kvp in currentSupplyCounts)
        {
            int playerId = kvp.Key;
            int currentCount = kvp.Value;
            int previousCount = lastSupplyCounts.ContainsKey(playerId) ? lastSupplyCounts[playerId] : 0;
            int gained = currentCount - previousCount;

            if (gained > 0)
            {
                if (!buildCredits.ContainsKey(playerId))
                    buildCredits[playerId] = 0;
                buildCredits[playerId] += gained;

                StartBuildPhaseForPlayer(playerId);
            }
        }

        lastSupplyCounts = currentSupplyCounts;
    }

    [Server]
    public void StartBuildPhaseForPlayer(int playerId)
    {
        if (playersBuilding.Contains(playerId)) return;

        playersBuilding.Add(playerId);

        MainPlayerController player = FindPlayerById(playerId);
        if (player != null && player.connectionToClient != null)
        {
            int credits = buildCredits.ContainsKey(playerId) ? buildCredits[playerId] : 0;
            player.TargetStartBuildPhase(player.connectionToClient, credits);
        }
    }

    [Server]
    public void ServerTrySpawnUnit(int playerId, string countryRegionId, Color playerColor, NetworkConnection requester)
    {
        if (!buildCredits.ContainsKey(playerId) || buildCredits[playerId] <= 0) return;
        if (RegionDirectory.Instance == null) return;

        Country c = RegionDirectory.Instance.GetCountryOrNull(countryRegionId);
        if (c == null) return;

        if (c.ownerID != playerId) return;

        GameObject unitObj = MainUnitManager.Instance.SpawnUnitsForRegionServer(c.regionId, playerId, playerColor, 1);

        buildCredits[playerId]--;

        if (requester != null)
            TargetSetupLocalUnit(requester, unitObj);

        if (buildCredits[playerId] <= 0)
            FinishBuildPhaseForPlayer(playerId);
    }

    [TargetRpc]
    public void TargetSetupLocalUnit(NetworkConnection target, GameObject unitObj)
    {
        unitObj?.GetComponent<MainUnit>()?.SetupLocalVisuals();
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

    public bool HasBuildCredits(int playerId)
        => buildCredits.ContainsKey(playerId) && buildCredits[playerId] > 0;

    private MainPlayerController FindPlayerById(int playerId)
    {
        foreach (var player in MainPlayerController.allPlayers)
            if (player != null && player.playerID == playerId)
                return player;
        return null;
    }
}
