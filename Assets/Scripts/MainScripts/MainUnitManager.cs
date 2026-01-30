using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainUnitManager : NetworkBehaviour
{
    private Dictionary<string, int> countrySpawnIndices = new Dictionary<string, int>();

    public static MainUnitManager Instance;

    [Tooltip("Prefab must be in the NetworkManager spawnable prefabs list")]
    public GameObject unitPrefab;
    private List<MainUnit> allUnits = new List<MainUnit>();

    void Awake() => Instance = this;

    void Start()
    {
        if (unitPrefab == null)
            Debug.LogError("[MainUnitManager] unitPrefab is null!");

        TestRpcMove();
    }

    public List<MainUnit> GetAllUnits() => allUnits;

    #region Unit Spawning (Server Only)
    [Server]
    public GameObject SpawnUnitsForCountryServer(string countryName, int playerID, Color playerColor, int count)
    {
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
            return null;
        }

        GameObject lastSpawnedUnit = null;

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

            lastSpawnedUnit = unitObj;
        }

        RpcUpdateCountryOwnership(countryName, playerColor);
        return lastSpawnedUnit;
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

            GameObject countryObj = GameObject.FindWithTag(countryName);
            if (countryObj == null)
            {
                Debug.LogWarning($"[Server] Country with tag '{countryName}' not found!");
                continue;
            }

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
                foreach (MainUnit unit in units)
                {
                    StartCoroutine(SendRpcWithDelay(unit, unit.transform.position, 0.05f));
                }
                Debug.Log($"[Server] Bounce at {countryName}");
            }
            else
            {
                Country countryComp = countryObj.GetComponent<Country>();
                List<MainUnit> winningUnits = units.FindAll(u => u.ownerID == winnerID);

                foreach (MainUnit unit in winningUnits)
                {
                    if (unit.currentOrder == null) continue;
                    if (unit.currentOrder.orderType == UnitOrderType.Support) continue;

                    Vector3 targetPos = unit.currentOrder.targetPosition;
                    if (targetPos == Vector3.zero && countryComp != null)
                        targetPos = countryComp.centerWorldPos;

                    unit.currentCountry = countryName;

                    unit.currentOrder = null;

                    StartCoroutine(SendRpcWithDelay(unit, targetPos, 0.05f));
                }

                Debug.Log($"[Server] Player {winnerID} wins {countryName}");
                if (!isBounce && countryComp != null)
                    countryComp.SetOwner(winnerID);

                Color winnerColor = winningUnits[0].playerColor;
                RpcUpdateCountryOwnership(countryName, winnerColor);
            }
        }

        foreach (var player in MainPlayerController.allPlayers)
            player.RpcResetReady();
    }



    private IEnumerator SendRpcWithDelay(MainUnit unit, Vector3 targetPos, float delay)
    {
        yield return new WaitForSeconds(delay);
        unit.RpcMoveTo(targetPos);
    }
    #endregion

    #region Client RPCs
    [ClientRpc]
    public void RpcUpdateCountryOwnership(string countryTag, Color playerColor)
    {
        GameObject countryObj = GameObject.FindWithTag(countryTag);
        if (countryObj == null)
        {
            Debug.LogWarning($"[ClientRpc] Country with tag '{countryTag}' not found for coloring.");
            return;
        }

        Renderer[] renderers = countryObj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[ClientRpc] No renderers found in '{countryTag}' or its children.");
            return;
        }

        foreach (var rend in renderers)
        {
            rend.material.color = playerColor;
        }

        Debug.Log($"[ClientRpc] Updated color of {countryTag} to {playerColor}");
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



    [Server]
    public void TestRpcMove()
    {
        if (allUnits.Count > 0)
        {
            allUnits[0].RpcMoveTo(allUnits[0].transform.position + Vector3.forward * 3f);
            Debug.Log("[Server] Called TestRpcMove on unit");
        }
    }


    #endregion
}
