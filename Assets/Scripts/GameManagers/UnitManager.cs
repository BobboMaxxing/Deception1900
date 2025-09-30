using System.Collections.Generic;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    [Header("Starter Units")]
    [SerializeField] private GameObject[] starterUnits;

    private readonly List<Unit> controlledUnits = new List<Unit>();
    private readonly Dictionary<Unit, UnitOrder> pendingOrders = new Dictionary<Unit, UnitOrder>();

    // Spawn units on chosen country
    public void SpawnUnitsForCountry(string countryName, int playerID)
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        List<Transform> validSpawns = new List<Transform>();
        foreach (GameObject sp in spawnPoints)
        {
            SpawnPoint spScript = sp.GetComponent<SpawnPoint>();
            if (spScript != null && spScript.countryName == countryName)
            {
                validSpawns.Add(sp.transform);
            }
        }

        for (int i = 0; i < starterUnits.Length && i < validSpawns.Count; i++)
        {
            GameObject unitObj = Instantiate(starterUnits[i], validSpawns[i].position, validSpawns[i].rotation);
            Unit unit = unitObj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.Initialize(playerID);
                controlledUnits.Add(unit);
            }
        }
    }

    // Assign an order to a unit
    public void IssueOrder(Unit unit, UnitOrder order)
    {
        if (controlledUnits.Contains(unit))
        {
            pendingOrders[unit] = order;

            // Draw line for visual feedback
            if (order.type == OrderType.Move && !string.IsNullOrEmpty(order.targetCountry))
            {
                Vector3 targetPos = GetCountryCenter(order.targetCountry);
                unit.SetOrder(order, targetPos);
            }
            else if (order.type == OrderType.Support && order.targetUnit != null)
            {
                unit.SetOrder(order, order.targetUnit.transform.position);
            }
        }
    }

    // Execute all orders (resolve moves with support)
    public void ExecuteTurn()
    {
        // 1. Count support for each move
        Dictionary<Unit, int> supportCounts = new Dictionary<Unit, int>();
        Dictionary<string, List<Unit>> movesToCountry = new Dictionary<string, List<Unit>>();

        foreach (var entry in pendingOrders)
        {
            Unit unit = entry.Key;
            UnitOrder order = entry.Value;
            if (order.type == OrderType.Move)
            {
                if (!movesToCountry.ContainsKey(order.targetCountry))
                    movesToCountry[order.targetCountry] = new List<Unit>();
                movesToCountry[order.targetCountry].Add(unit);
            }
            else if (order.type == OrderType.Support && order.targetUnit != null)
            {
                if (!supportCounts.ContainsKey(order.targetUnit))
                    supportCounts[order.targetUnit] = 0;
                supportCounts[order.targetUnit]++;
            }
        }

        // 2. Resolve moves
        foreach (var kv in movesToCountry)
        {
            string country = kv.Key;
            List<Unit> contenders = kv.Value;

            Unit winner = null;
            int highestStrength = 0;

            foreach (Unit unit in contenders)
            {
                int strength = 1;
                if (supportCounts.ContainsKey(unit))
                    strength += supportCounts[unit];

                if (strength > highestStrength)
                {
                    highestStrength = strength;
                    winner = unit;
                }
                else if (strength == highestStrength)
                {
                    winner = null; // tie = bounce
                }
            }

            if (winner != null)
            {
                Vector3 targetPos = GetCountryCenter(country);
                winner.ExecuteMove(targetPos);
            }
        }

        // 3. Clear all lines and pending orders
        foreach (Unit unit in controlledUnits)
        {
            unit.ClearLine();
        }
        pendingOrders.Clear();
    }

    private Vector3 GetCountryCenter(string countryName)
    {
        GameObject obj = GameObject.Find(countryName);
        return obj != null ? obj.transform.position : Vector3.zero;
    }

    public List<Unit> GetControlledUnits()
    {
        return new List<Unit>(controlledUnits);
    }
}
