using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MainUnitManager : MonoBehaviour
{
    public static MainUnitManager Instance;
    public List<MainUnit> controlledUnits = new List<MainUnit>();
    public GameObject starterUnitPrefab;

    [Header("Move Settings")]
    [SerializeField] private float moveOffsetRadius = 2.5f;
    [SerializeField] private float spacing = 2f;

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnUnitsForCountry(string countryName, int playerID, Color playerColor, int totalUnits)
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

        for (int i = 0; i < totalUnits; i++)
        {
            Transform spawn = validSpawns[i % validSpawns.Count];
            float offsetX = (i - (totalUnits - 1) / 2f) * spacing;
            Vector3 spawnPos = spawn.position + new Vector3(offsetX, 0, 0);

            GameObject unitObj = Instantiate(starterUnitPrefab, spawnPos, spawn.rotation);
            MainUnit unit = unitObj.GetComponent<MainUnit>();
            if (unit != null)
            {
                unit.Initialize(playerID);
                unit.SetColor(playerColor);
                controlledUnits.Add(unit);
            }
        }
    }

    public void IssueOrder(MainUnit unit, UnitOrder order)
    {
        if (unit == null || order == null) return;

        Vector3 targetPos = unit.transform.position;

        if (order.orderType == OrderType.Move)
        {
            GameObject targetObj = GameObject.Find(order.targetCountry);
            if (targetObj != null)
                targetPos = targetObj.transform.position;
        }
        else if (order.orderType == OrderType.Support && order.supportedUnit != null)
        {
            targetPos = order.supportedUnit.transform.position;
        }

        unit.SetOrder(order, targetPos);
    }

    public void ClearAllOrders()
    {
        foreach (MainUnit unit in controlledUnits)
            unit?.ClearOrder();
    }

    public void ExecuteTurn()
    {
        StartCoroutine(ExecuteTurnCoroutine());
    }

    private IEnumerator ExecuteTurnCoroutine()
    {
        Dictionary<string, List<MainUnit>> unitsByTarget = new Dictionary<string, List<MainUnit>>();

        foreach (MainUnit unit in controlledUnits)
        {
            UnitOrder order = unit.GetOrder();
            if (order == null) continue;

            if (!unitsByTarget.ContainsKey(order.targetCountry))
                unitsByTarget[order.targetCountry] = new List<MainUnit>();

            unitsByTarget[order.targetCountry].Add(unit);
        }

        foreach (var kvp in unitsByTarget)
        {
            string countryName = kvp.Key;
            List<MainUnit> units = kvp.Value;
            GameObject countryObj = GameObject.Find(countryName);
            if (countryObj == null) continue;

            Dictionary<int, int> playerStrength = new Dictionary<int, int>();
            foreach (MainUnit u in units)
            {
                if (!playerStrength.ContainsKey(u.ownerID))
                    playerStrength[u.ownerID] = 0;

                UnitOrder order = u.GetOrder();
                if (order.orderType == OrderType.Move) playerStrength[u.ownerID] += 1;
                if (order.orderType == OrderType.Support && order.supportedUnit != null)
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
                CaptureCountry(countryObj, winningPlayer);

            for (int i = 0; i < units.Count; i++)
            {
                MainUnit unit = units[i];
                UnitOrder order = unit.GetOrder();

                Vector3 basePos = order.orderType == OrderType.Move ?
                                  countryObj.transform.position :
                                  order.supportedUnit != null ? order.supportedUnit.transform.position :
                                  unit.transform.position;

                Vector3 offset = GetMoveOffsetForTurn(i, units.Count);
                Vector3 targetPos = basePos + offset;
                targetPos.y = unit.transform.position.y;

                unit.currentCountry = countryName;
                unit.ExecuteMove(targetPos);
            }
        }

        yield return new WaitForSeconds(0.1f);
    }

    private Vector3 GetMoveOffsetForTurn(int index, int totalUnits)
    {
        if (totalUnits <= 1) return Vector3.zero;

        float angleStep = 360f / totalUnits;
        float angle = index * angleStep * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * moveOffsetRadius;
    }

    private void CaptureCountry(GameObject countryObj, int playerID)
    {
        Renderer rend = countryObj.GetComponent<Renderer>();
        if (rend != null)
        {
            MainPlayerController player = MainPlayerController.GetPlayerByID(playerID);
            Color color = player != null ? player.playerColor : Color.white;
            rend.material.color = color;
        }
    }

    private List<MainUnit> GetUnitsInCountry(string countryName)
    {
        List<MainUnit> unitsInCountry = new List<MainUnit>();
        foreach (MainUnit u in controlledUnits)
            if (u.currentCountry == countryName) unitsInCountry.Add(u);
        return unitsInCountry;
    }

    public void ResetUnitsForNextTurn()
    {
        foreach (MainUnit unit in controlledUnits)
            unit?.ClearOrder();
    }
}
