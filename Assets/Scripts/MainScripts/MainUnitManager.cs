using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class MainUnitManager : NetworkBehaviour
{
    public static MainUnitManager Instance;

    public GameObject unitPrefab;
    private List<MainUnit> allUnits = new List<MainUnit>();

    void Awake() => Instance = this;

    public List<MainUnit> GetAllUnits() => allUnits;

    [Server]
    public void SpawnUnitsForCountryServer(string countryName, int playerID, Color playerColor, int count)
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        List<Transform> validSpawns = new List<Transform>();
        foreach (var sp in spawnPoints)
        {
            SpawnPoint spScript = sp.GetComponent<SpawnPoint>();
            if (spScript != null && spScript.countryName == countryName)
                validSpawns.Add(sp.transform);
        }

        for (int i = 0; i < count; i++)
        {
            Transform spawn = validSpawns[i % validSpawns.Count];
            Vector3 offset = GetSpawnOffset(i, count);
            Vector3 spawnPos = spawn.position + offset;

            GameObject unitObj = Instantiate(unitPrefab, spawnPos, spawn.rotation);
            NetworkServer.Spawn(unitObj);

            MainUnit unit = unitObj.GetComponent<MainUnit>();
            if (unit != null)
            {
                unit.currentCountry = countryName;
                allUnits.Add(unit);
                unit.RpcInitialize(playerID, playerColor);
                if (isServer && connectionToClient == null)
                    unit.SetupLocalVisuals();
            }
        }

        RpcUpdateCountryOwnership(countryName, playerColor);
    }

    public void SpawnUnitsForCountryLocal(string countryName, int playerID, Color playerColor, int count)
    {
        if (isServer) return;

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        List<Transform> validSpawns = new List<Transform>();
        foreach (var sp in spawnPoints)
        {
            SpawnPoint spScript = sp.GetComponent<SpawnPoint>();
            if (spScript != null && spScript.countryName == countryName)
                validSpawns.Add(sp.transform);
        }

        for (int i = 0; i < count; i++)
        {
            Transform spawn = validSpawns[i % validSpawns.Count];
            Vector3 offset = GetSpawnOffset(i, count);
            Vector3 spawnPos = spawn.position + offset;

            GameObject unitObj = Instantiate(unitPrefab, spawnPos, spawn.rotation);
            MainUnit unit = unitObj.GetComponent<MainUnit>();
            unit.ownerID = playerID;
            unit.currentCountry = countryName;
            unit.playerColor = playerColor;
            allUnits.Add(unit);
            unit.SetupLocalVisuals();
        }
    }

    private Vector3 GetSpawnOffset(int index, int total)
    {
        if (total <= 1) return Vector3.zero;
        float angleStep = 360f / total;
        float angle = index * angleStep * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2f;
    }

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

            int winnerID = GetDominantOwner(units);
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
}
