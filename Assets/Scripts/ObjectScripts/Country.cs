using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class Country : MonoBehaviour
{
    public string countryName;
    public int ownerID = -1;
    public bool isStarterCountry = true;

    [Header("Gameplay Anchor")]
    public Transform centerPoint;
    [Header("Cached World Data")]
    public Vector3 centerWorldPos;

    [Header("Unit Spawn Points")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Supply System")]
    public bool isSupplyCenter = false;
    
    [Header("Tile Type")]
    public bool isOcean = false;

    [Header("Airfields")]
    public bool isAirfield = false;

    [Header("Adjacency List (drag neighboring countries here)")]
    public List<Country> adjacentCountries = new List<Country>();

    [Header("Starter Sub-Countries (only matters if isStarterCountry = true)")]
    public List<Country> starterSubCountries = new List<Country>();

    public bool CanBeSelected() => isStarterCountry && ownerID == -1;

    public bool IsAdjacentTo(Country target)
    {
        return adjacentCountries.Contains(target);
    }
    public bool IsCoastal()
    {
        for (int i = 0; i < adjacentCountries.Count; i++)
        {
            Country n = adjacentCountries[i];
            if (n != null && n.isOcean) return true;
        }
        return false;
    }


    public List<Country> GetAllSelectableCountries()
    {
        List<Country> list = new List<Country> { this };

        if (isStarterCountry)
        {
            foreach (var sub in starterSubCountries)
            {
                if (sub != null)
                    list.Add(sub);
            }
        }

        return list;
    }

    void Awake()
    {
        if (centerPoint != null)
        {
            centerWorldPos = centerPoint.position;
        }
        else
        {
            Debug.LogError($"Country {countryName} has NO centerPoint assigned!");
        }
    }
    public void SetOwner(int newOwnerID)
    {
        ownerID = newOwnerID;
        Debug.Log($"{countryName} now owned by Player {ownerID}");

        if (isStarterCountry)
        {
            foreach (var sub in starterSubCountries)
            {
                if (sub != null)
                    sub.ownerID = newOwnerID;
            }
        }

        if (NetworkServer.active && MainGameManager.Instance != null)
        {
            MainGameManager.Instance.CheckWinConditionServer();
        }
    }

    public Transform GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
            return null;

        return spawnPoints[Random.Range(0, spawnPoints.Count)];
    }
}
