using UnityEngine;
using System.Collections.Generic;

public class UnitManager : MonoBehaviour
{
    public static UnitManager Instance;

    [Header("Units")]
    public GameObject[] starterUnits;
    public List<Unit> controlledUnits = new List<Unit>();

    [Header("Offset Settings")]
    [SerializeField] private float spawnOffsetRadius = 2f;
    [SerializeField] private float moveOffsetRadius = 2.5f;

    [SerializeField] private int totalUnits = 3;
    [SerializeField] private float spacing = 2f;
    [SerializeField] private GameObject starterUnitPrefab;

    private void Awake()
    {
        Instance = this;
    }


    public void SpawnUnitsForCountry(string countryName, int playerID)
    {
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

        int unitCounter = 0;
        for (int i = 0; i < totalUnits; i++)
        {
            Transform spawn = validSpawns[unitCounter % validSpawns.Count];
            float offsetX = (unitCounter - (totalUnits - 1) / 2f) * spacing;
            Vector3 spawnPos = spawn.position + new Vector3(offsetX, 0, 0);

            GameObject unitObj = Instantiate(starterUnitPrefab, spawnPos, spawn.rotation);

            Unit unit = unitObj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.Initialize(playerID);
                controlledUnits.Add(unit);
            }

            unitCounter++;
        }

        Debug.Log($"Spawned {controlledUnits.Count} units for {countryName}");
    }

    public void ExecuteTurn()
    {
        Debug.Log("Executing turn — moving all units.");
        Dictionary<string, List<Unit>> unitsByTarget = new Dictionary<string, List<Unit>>();

        foreach (Unit unit in controlledUnits)
        {
            UnitOrder order = unit.GetOrder();
            if (order == null || order.orderType != OrderType.Move || string.IsNullOrEmpty(order.targetCountry))
                continue;

            if (!unitsByTarget.ContainsKey(order.targetCountry))
                unitsByTarget[order.targetCountry] = new List<Unit>();

            unitsByTarget[order.targetCountry].Add(unit);
        }

        foreach (var kvp in unitsByTarget)
        {
            string countryName = kvp.Key;
            List<Unit> units = kvp.Value;
            GameObject targetObj = GameObject.Find(countryName);
            if (targetObj == null) continue;

            Vector3 basePos = targetObj.transform.position;

            for (int i = 0; i < units.Count; i++)
            {
                Vector3 offset = GetMoveOffset(basePos, i);
                Vector3 targetPos = basePos + offset;
                targetPos.y = units[i].transform.position.y;
                units[i].ExecuteMove(targetPos);
            }
        }
    }

    public void ClearAllOrders()
    {
        Debug.Log("Clearing all unit orders.");
        foreach (Unit unit in controlledUnits)
        {
            if (unit != null)
                unit.ClearOrder();
        }
    }

    public void IssueOrder(Unit unit, UnitOrder order)
    {
        if (unit == null || order == null) return;

        GameObject targetObj = GameObject.Find(order.targetCountry);
        if (targetObj == null)
        {
            Debug.LogWarning("No target object found for country: " + order.targetCountry);
            return;
        }

        Vector3 targetPos = targetObj.transform.position;
        targetPos.y = unit.transform.position.y;

        unit.SetOrder(order, targetPos);

        LineRenderer lr = unit.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.startColor = Color.green;
            lr.endColor = Color.green;
        }

        Debug.Log($"Issued {order.orderType} order for {unit.name} to {order.targetCountry}");
    }

    private Vector3 GetMoveOffset(Vector3 center, int index)
    {
        float angle = index * Mathf.PI * 2f / Mathf.Max(1, 4); // 4 units per ring approx
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * moveOffsetRadius;
    }
}
