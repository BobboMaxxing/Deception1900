using System.Collections.Generic;
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

    [Header("Supply System")]
    public bool isSupplyCenter = false;

    [Header("Adjacency List (drag neighboring countries here)")]
    public List<Country> adjacentCountries = new List<Country>();

    [Header("Starter Sub-Countries (only matters if isStarterCountry = true)")]
    public List<Country> starterSubCountries = new List<Country>();

    public bool CanBeSelected() => isStarterCountry && ownerID == -1;

    public bool IsAdjacentTo(Country target)
    {
        return adjacentCountries.Contains(target);
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

        if (MainGameManager.Instance != null)
        {
            MainGameManager.Instance.CheckWinConditionServer();
        }
    }
}
