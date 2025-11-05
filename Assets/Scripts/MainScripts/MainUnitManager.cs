using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class MainUnitManager : NetworkBehaviour
{
    public static MainUnitManager Instance;

    [Tooltip("Prefab must be in the NetworkManager spawnable prefabs list")]
    public GameObject unitPrefab;
    private List<MainUnit> allUnits = new List<MainUnit>();

    void Awake() => Instance = this;

    void Start()
    {
        if (unitPrefab == null)
            Debug.LogError("[MainUnitManager] unitPrefab is null!");
    }

    public List<MainUnit> GetAllUnits() => allUnits;

    #region Unit Spawning (Server Only)
    [Server]
    public void SpawnUnitsForCountryServer(string countryName, int playerID, Color playerColor, int count)
    {
        Debug.Log("Spawning units for player " + playerID);
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        List<Transform> validSpawns = new List<Transform>();
        foreach (var sp in spawnPoints)
        {
            SpawnPoint spScript = sp.GetComponent<SpawnPoint>();
            if (spScript != null && spScript.countryName == countryName)
                validSpawns.Add(sp.transform);
        }

        if (validSpawns.Count == 0)
        {
            Debug.LogWarning($"[MainUnitManager] No spawn points found for country '{countryName}'");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Transform spawn = validSpawns[i % validSpawns.Count];
            Vector3 offset = GetSpawnOffset(i, count);
            Vector3 spawnPos = spawn.position + offset;

            GameObject unitObj = Instantiate(unitPrefab, spawnPos, spawn.rotation);
            MainUnit unit = unitObj.GetComponent<MainUnit>();
            if (unit == null)
            {
                Debug.LogError("[MainUnitManager] unitPrefab missing MainUnit component.");
                Destroy(unitObj);
                continue;
            }

            unit.ownerID = playerID;
            unit.playerColor = playerColor;
            unit.currentCountry = countryName;

            NetworkServer.Spawn(unitObj);

            allUnits.Add(unit);

            unit.RpcInitialize(playerID, playerColor);
        }

        // Original color logic: just update country renderer directly
        RpcUpdateCountryOwnership(countryName, playerColor);
    }
    #endregion

    #region Turn Execution (Server Only)
    [Server]
    public void ExecuteTurnServer()
    {
        Dictionary<string, List<MainUnit>> unitsByTarget = new Dictionary<string, List<MainUnit>>();

        foreach (var unit in allUnits)
        {
            if (unit.currentOrder == null) continue;
            string targetCountry = unit.currentOrder.targetCountry;
            if (!unitsByTarget.ContainsKey(targetCountry))
                unitsByTarget[targetCountry] = new List<MainUnit>();
            unitsByTarget[targetCountry].Add(unit);
        }

        foreach (var kvp in unitsByTarget)
        {
            string countryName = kvp.Key;
            List<MainUnit> units = kvp.Value;
            GameObject countryObj = GameObject.Find(countryName);
            if (countryObj == null) continue;

            for (int i = 0; i < units.Count; i++)
            {
                MainUnit unit = units[i];
                Vector3 basePos = countryObj.transform.position;
                Vector3 offset = GetSpawnOffset(i, units.Count);
                Vector3 targetPos = new Vector3(basePos.x + offset.x, unit.transform.position.y, basePos.z + offset.z);

                unit.currentCountry = countryName;
                unit.RpcMoveTo(targetPos);
            }

            // Determine the winner once
            int winnerID = GetDominantOwner(units);

            // Placeholder for future actions:
            // TODO: Handle Lose action
            // TODO: Handle Bounce action
            // TODO: Handle Defens action

            // Update server-side ownership
            Country countryComp = countryObj.GetComponent<Country>();
            if (countryComp != null)
                countryComp.SetOwner(winnerID);

            // Update visuals on clients
            Color winnerColor = units.Find(u => u.ownerID == winnerID).playerColor;
            RpcUpdateCountryOwnership(countryName, winnerColor);
        }

        foreach (var player in MainPlayerController.allPlayers)
            player.RpcResetReady();
    }

    private int GetDominantOwner(List<MainUnit> units)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        foreach (var u in units)
        {
            if (!counts.ContainsKey(u.ownerID))
                counts[u.ownerID] = 0;
            counts[u.ownerID]++;
        }

        int bestOwner = -1;
        int maxCount = 0;
        foreach (var kv in counts)
        {
            if (kv.Value > maxCount)
            {
                maxCount = kv.Value;
                bestOwner = kv.Key;
            }
        }

        return bestOwner;
    }
    #endregion

    #region Client RPCs
    [ClientRpc]
    public void RpcUpdateCountryOwnership(string countryName, Color playerColor)
    {
        GameObject countryObj = GameObject.Find(countryName);
        if (countryObj != null)
        {
            Renderer rend = countryObj.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = playerColor;
        }
    }
    #endregion

    #region Utility
    private Vector3 GetSpawnOffset(int index, int total)
    {
        if (total <= 1) return Vector3.zero;
        float angleStep = 360f / total;
        float angle = index * angleStep * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2f;
    }
    #endregion
}
