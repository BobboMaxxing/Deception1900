using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class Country : MonoBehaviour
{
    public string countryName;
    public Color countryColor = Color.white;
    public int ownerID = -1;
    public bool isStarterCountry = true;

    [Header("Gameplay Anchor")]
    public Transform centerPoint;
    [Header("Cached World Data")]
    public Vector3 centerWorldPos;
    [SerializeField] private MeshCollider regionMeshCollider;

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
    
    [Header("Plane Adjacency List")]
    public List<Country> planeAdjacentCountries = new List<Country>();

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

        if (regionMeshCollider == null)
            regionMeshCollider = GetComponent<MeshCollider>();

        if (regionMeshCollider == null)
            regionMeshCollider = GetComponentInChildren<MeshCollider>();
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
    public MeshCollider GetRegionMeshCollider()
    {
        return regionMeshCollider;
    }

    private bool TryProjectPointOntoRegion(Vector3 worldPoint, out Vector3 result, float yLevel)
    {
        result = new Vector3(worldPoint.x, yLevel, worldPoint.z);

        if (regionMeshCollider == null)
            return false;

        Bounds bounds = regionMeshCollider.bounds;
        float rayHeight = bounds.max.y + 50f;
        float rayDepth = bounds.min.y - 50f;

        Ray downRay = new Ray(new Vector3(worldPoint.x, rayHeight, worldPoint.z), Vector3.down);
        if (regionMeshCollider.Raycast(downRay, out RaycastHit downHit, rayHeight - rayDepth + 5f))
        {
            result = downHit.point;
            result.y = yLevel;
            return true;
        }

        Ray upRay = new Ray(new Vector3(worldPoint.x, rayDepth, worldPoint.z), Vector3.up);
        if (regionMeshCollider.Raycast(upRay, out RaycastHit upHit, rayHeight - rayDepth + 5f))
        {
            result = upHit.point;
            result.y = yLevel;
            return true;
        }

        return false;
    }

    public Vector3 GetSurfaceConstrainedPoint(Vector3 worldPoint, float yLevel)
    {
        if (TryProjectPointOntoRegion(worldPoint, out Vector3 projected, yLevel))
            return projected;

        return new Vector3(worldPoint.x, yLevel, worldPoint.z);
    }

    public Vector3 GetBridgePointTowards(Country otherCountry, float yLevel)
    {
        if (otherCountry == null)
            return new Vector3(centerWorldPos.x, yLevel, centerWorldPos.z);

        Vector3 dir = (otherCountry.centerWorldPos - centerWorldPos);
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return new Vector3(centerWorldPos.x, yLevel, centerWorldPos.z);

        dir.Normalize();

        Vector3 candidate = centerWorldPos + dir * 12f;

        if (TryProjectPointOntoRegion(candidate, out Vector3 projected, yLevel))
            return projected;

        if (TryProjectPointOntoRegion(centerWorldPos, out Vector3 centerProjected, yLevel))
            return centerProjected;

        return new Vector3(centerWorldPos.x, yLevel, centerWorldPos.z);
    }

    public Vector3 GetRandomConstrainedPointNearCenter(float radius, float yLevel)
    {
        if (regionMeshCollider == null)
            return new Vector3(centerWorldPos.x, yLevel, centerWorldPos.z);

        for (int i = 0; i < 20; i++)
        {
            Vector2 offset2D = Random.insideUnitCircle * radius;
            Vector3 candidate = centerWorldPos + new Vector3(offset2D.x, 0f, offset2D.y);

            if (TryProjectPointOntoRegion(candidate, out Vector3 projected, yLevel))
                return projected;
        }

        if (TryProjectPointOntoRegion(centerWorldPos, out Vector3 centerProjected, yLevel))
            return centerProjected;

        return new Vector3(centerWorldPos.x, yLevel, centerWorldPos.z);
    }

}
