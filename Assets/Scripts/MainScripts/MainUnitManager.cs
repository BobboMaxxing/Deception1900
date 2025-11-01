using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class MainUnitManager : NetworkBehaviour
{
    public static MainUnitManager Instance;

    public GameObject unitPrefab; // Prefab must have NetworkIdentity + NetworkTransform
    private List<MainUnit> allUnits = new List<MainUnit>();

    void Awake() => Instance = this;

    [Server]
    public void SpawnUnitsForCountryServer(string countryName, int playerID, Color playerColor, int count)
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        List<Transform> validSpawns = new List<Transform>();

        foreach (GameObject sp in spawnPoints)
        {
            SpawnPoint spScript = sp.GetComponent<SpawnPoint>();
            if (spScript != null && spScript.countryName == countryName)
                validSpawns.Add(sp.transform);
        }

        for (int i = 0; i < count; i++)
        {
            Transform spawn = validSpawns[i % validSpawns.Count];
            GameObject unitObj = Instantiate(unitPrefab, spawn.position, spawn.rotation);
            NetworkServer.Spawn(unitObj);

            MainUnit unit = unitObj.GetComponent<MainUnit>();
            if (unit != null)
            {
                unit.RpcInitialize(playerID, playerColor);
                allUnits.Add(unit);
            }
        }
    }

    // Execute moves & resolve battles
    [Server]
    public void ExecuteTurnServer()
    {
        Dictionary<string, List<MainUnit>> unitsByCountry = new Dictionary<string, List<MainUnit>>();

        foreach (var unit in allUnits)
        {
            if (unit.currentOrder == null) continue;
            string targetCountry = unit.currentOrder.targetCountry;

            if (!unitsByCountry.ContainsKey(targetCountry))
                unitsByCountry[targetCountry] = new List<MainUnit>();

            unitsByCountry[targetCountry].Add(unit);
        }

        foreach (var kvp in unitsByCountry)
        {
            string countryName = kvp.Key;
            List<MainUnit> units = kvp.Value;
            GameObject countryObj = GameObject.Find(countryName);
            if (countryObj == null) continue;

            Dictionary<int, int> playerStrength = new Dictionary<int, int>();
            foreach (var u in units)
            {
                if (!playerStrength.ContainsKey(u.ownerID))
                    playerStrength[u.ownerID] = 0;

                if (u.currentOrder.orderType == UnitOrderType.Move)
                    playerStrength[u.ownerID] += 1;
                else if (u.currentOrder.orderType == UnitOrderType.Support && u.currentOrder.supportedUnit != null)
                    playerStrength[u.ownerID] += 1;
            }

            int maxStrength = 0;
            int winningPlayer = -1;
            bool tie = false;

            foreach (var kv in playerStrength)
            {
                if (kv.Value > maxStrength)
                {
                    maxStrength = kv.Value;
                    winningPlayer = kv.Key;
                    tie = false;
                }
                else if (kv.Value == maxStrength)
                {
                    tie = true;
                }
            }

            if (!tie)
                RpcCaptureCountry(countryObj, winningPlayer);

            // Move units
            for (int i = 0; i < units.Count; i++)
            {
                MainUnit unit = units[i];
                Vector3 basePos = countryObj.transform.position;
                Vector3 offset = GetMoveOffset(i, units.Count);
                Vector3 targetPos = basePos + offset;
                targetPos.y = unit.transform.position.y;

                unit.currentCountry = countryName;
                unit.RpcMoveTo(targetPos);
            }
        }
    }

    private Vector3 GetMoveOffset(int index, int total)
    {
        if (total <= 1) return Vector3.zero;
        float angleStep = 360f / total;
        float angle = index * angleStep * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2f;
    }

    [ClientRpc]
    private void RpcCaptureCountry(GameObject countryObj, int playerID)
    {
        MainPlayerController player = MainPlayerController.GetPlayerByID(playerID);
        if (countryObj != null)
        {
            Renderer rend = countryObj.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = player != null ? player.playerColor : Color.white;
        }
    }
}
