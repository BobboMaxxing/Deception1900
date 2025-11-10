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

            Dictionary<int, int> totalStrength = new Dictionary<int, int>();
            foreach (MainUnit mover in units)
            {
                if (!totalStrength.ContainsKey(mover.ownerID))
                    totalStrength[mover.ownerID] = 1;
                else
                    totalStrength[mover.ownerID] += 1;
            }

            foreach (MainUnit supporter in allUnits)
            {
                if (supporter.currentOrder == null) continue;
                if (supporter.currentOrder.orderType != UnitOrderType.Support) continue;
                if (supporter.currentOrder.targetCountry != countryName) continue;

                MainUnit supported = supporter.currentOrder.supportedUnit;
                if (supported == null || supported.currentOrder == null) continue;
                if (supported.currentOrder.targetCountry != countryName) continue;

                int supportedOwner = supported.ownerID;
                if (!totalStrength.ContainsKey(supportedOwner))
                    totalStrength[supportedOwner] = 0;
                totalStrength[supportedOwner] += 1;

                Debug.Log($"[Support] {supporter.name} adds +1 to Player {supportedOwner} for {countryName}");
            }

            int maxStrength = 0;
            int winnerID = -1;
            bool isBounce = false;
            foreach (var kv in totalStrength)
            {
                if (kv.Value > maxStrength)
                {
                    maxStrength = kv.Value;
                    winnerID = kv.Key;
                    isBounce = false;
                }
                else if (kv.Value == maxStrength)
                {
                    isBounce = true;
                }
            }

            if (isBounce)
            {
                Debug.Log($"[Server] Bounce at {countryName}");
                foreach (MainUnit unit in units)
                    unit.RpcMoveTo(unit.transform.position);
            }
            else
            {
                Vector3 basePos = countryObj.transform.position;
                List<MainUnit> winningUnits = units.FindAll(u => u.ownerID == winnerID);
                for (int i = 0; i < winningUnits.Count; i++)
                {
                    MainUnit unit = winningUnits[i];
                    if (unit.currentOrder != null && unit.currentOrder.orderType == UnitOrderType.Support)
                        continue;

                    Vector3 offset = GetSpawnOffset(i, winningUnits.Count);
                    Vector3 targetPos = new Vector3(basePos.x + offset.x, unit.transform.position.y, basePos.z + offset.z);
                    unit.currentCountry = countryName;
                    unit.RpcMoveTo(targetPos);
                }

                Debug.Log($"[Server] Player {winnerID} wins {countryName}");

                Country countryComp = countryObj.GetComponent<Country>();
                if (countryComp != null)
                    countryComp.SetOwner(winnerID);

                Color winnerColor = winningUnits[0].playerColor;
                RpcUpdateCountryOwnership(countryName, winnerColor);
            }
        }

        foreach (var player in MainPlayerController.allPlayers)
            player.RpcResetReady();
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
