using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UnitManager : MonoBehaviour
{
    public static UnitManager Instance;
    public PlayerInputController playerInputController;

    [Header("Units")]
    public GameObject starterUnitPrefab;
    public List<Unit> controlledUnits = new List<Unit>();
    [SerializeField] bool hasSpawn = false;

    [Header("Offset Settings")]
    [SerializeField] private float spawnOffsetRadius = 2f;
    [SerializeField] private float moveOffsetRadius = 2.5f;
    [SerializeField] private float spacing = 2f;

    private void Awake()
    {
        Instance = this;
        Color PlayerColor = playerInputController.PlayerColor;
    }

    public void SpawnUnitsForCountry(string countryName, int playerID, int totalUnits)
    {
        if (hasSpawn) {return; }
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        List<Transform> validSpawns = new List<Transform>();

        foreach (GameObject sp in spawnPoints)
        {
            SpawnPoint spScript = sp.GetComponent<SpawnPoint>();
            if (spScript != null && spScript.countryName == countryName)
                validSpawns.Add(sp.transform);
        }

        if (validSpawns.Count == 0)
        {
            Debug.LogWarning("No spawn points found for country: " + countryName);
            return;
        }

        for (int i = 0; i < totalUnits; i++)
        {
            Transform spawn = validSpawns[i % validSpawns.Count];

            float offsetX = (i - (totalUnits - 1) / 2f) * spacing;
            Vector3 spawnPos = spawn.position + new Vector3(offsetX, 0, 0);

            GameObject unitObj = Instantiate(starterUnitPrefab, spawnPos, spawn.rotation);
            Unit unit = unitObj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.Initialize(playerID);
                controlledUnits.Add(unit);
            }
        }
        hasSpawn = true;
    }

    public void IssueOrder(Unit unit, UnitOrder order)
    {
        if (unit == null || order == null) return;

        Vector3 targetPos = unit.transform.position;

        if (order.orderType == OrderType.Move)
        {
            GameObject targetObj = GameObject.Find(order.targetCountry);
            if (targetObj != null)
            {
                targetPos = targetObj.transform.position;
                targetPos.y = unit.transform.position.y;
            }
        }
        else if (order.orderType == OrderType.Support && order.supportedUnit != null)
        {
            targetPos = order.supportedUnit.transform.position;
        }

        unit.SetOrder(order, targetPos);

        LineRenderer lr = unit.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.startColor = (order.orderType == OrderType.Support) ? Color.yellow : Color.green;
            lr.endColor = lr.startColor;
        }

        Debug.Log($"Issued {order.orderType} order for {unit.name} to {order.targetCountry}");
    }

    public void ExecuteTurn()
    {
        StartCoroutine(ExecuteTurnCoroutine());
    }

    private IEnumerator ExecuteTurnCoroutine()
    {
        Dictionary<string, List<Unit>> unitsByTarget = new Dictionary<string, List<Unit>>();

        // Group units by target country
        foreach (Unit unit in controlledUnits)
        {
            UnitOrder order = unit.GetOrder();
            if (order == null) continue;

            if (!unitsByTarget.ContainsKey(order.targetCountry))
                unitsByTarget[order.targetCountry] = new List<Unit>();

            unitsByTarget[order.targetCountry].Add(unit);
        }

        // Resolve orders
        foreach (var kvp in unitsByTarget)
        {
            string countryName = kvp.Key;
            List<Unit> units = kvp.Value;
            GameObject targetObj = GameObject.Find(countryName);
            if (targetObj == null) continue;

            Dictionary<int, int> playerStrength = new Dictionary<int, int>();

            foreach (Unit u in units)
            {
                if (!playerStrength.ContainsKey(u.ownerID)) playerStrength[u.ownerID] = 0;

                if (u.GetOrder().orderType == OrderType.Move)
                    playerStrength[u.ownerID] += 1;
                else if (u.GetOrder().orderType == OrderType.Support && u.GetOrder().supportedUnit != null)
                    playerStrength[u.ownerID] += 1;
            }

            // Determine winner
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

            // Apply capture
            if (!tie)
            {
                CaptureCountry(targetObj, winningPlayer);
            }

            // Move units with actual Move orders
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].GetOrder().orderType == OrderType.Move)
                {
                    Vector3 basePos = targetObj.transform.position;
                    Vector3 offset = GetMoveOffset(basePos, i);
                    Vector3 targetPos = basePos + offset;
                    targetPos.y = units[i].transform.position.y;
                    units[i].ExecuteMove(targetPos);
                }

                // Clear the order after execution
                //units[i].ClearOrder();
            }
        }

        yield return null;
    }

    private Vector3 GetMoveOffset(Vector3 center, int index)
    {
        float angle = index * Mathf.PI * 2f / Mathf.Max(1, 4);
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * moveOffsetRadius;
    }

    public void ClearAllOrders()
    {
        foreach (Unit unit in controlledUnits)
        {
            if (unit != null)
                unit.ClearOrder();
        }
    }

    private void CaptureCountry(GameObject countryObj, int playerID)
    {
        Renderer rend = countryObj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = (playerID == 1) ? Color.red : Color.blue;
        }
    }

    public void ResetUnitsForNextTurn()
    {
        foreach (Unit unit in controlledUnits)
        {
            if (unit != null)
            {
                unit.ClearOrder(); 
            }
        }
    }
}
