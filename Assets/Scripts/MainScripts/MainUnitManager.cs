using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainUnitManager : NetworkBehaviour
{
    public static MainUnitManager Instance;

    [Header("Unit Prefabs (must be in NetworkManager spawnable prefabs)")]
    public GameObject landUnitPrefab;
    public GameObject boatUnitPrefab;
    public GameObject planeUnitPrefab;

    private readonly List<MainUnit> allUnits = new List<MainUnit>();

    private readonly Dictionary<int, int> spawnUseIndexBySpawnPoint = new Dictionary<int, int>();

    [Header("Spawn Offsets")]
    [Tooltip("Controls how fast the spiral expands.")]
    [SerializeField] private float spawnSpacing = 2.0f;

    [Tooltip("Max radius per type. Increase if units still overlap.")]
    [SerializeField] private float landSpawnRadius = 15.0f;
    [SerializeField] private float boatSpawnRadius = 10.0f;
    [SerializeField] private float planeSpawnRadius = 8.0f;

    void Awake() => Instance = this;

    void Start()
    {
        if (landUnitPrefab == null) Debug.LogError("[MainUnitManager] landUnitPrefab is null!");
        if (boatUnitPrefab == null) Debug.LogError("[MainUnitManager] boatUnitPrefab is null!");
        if (planeUnitPrefab == null) Debug.LogError("[MainUnitManager] planeUnitPrefab is null!");
    }

    public List<MainUnit> GetAllUnits() => allUnits;

    [Server]
    public GameObject SpawnUnitsForCountryServer(
        string ownerCountryTag,
        int playerID,
        Color playerColor,
        int count,
        UnitType unitType,
        string requiredSpawnTileTag
    )
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        List<SpawnPoint> validSpawns = new List<SpawnPoint>();


        foreach (var spObj in spawnPoints)
        {
            SpawnPoint sp = spObj.GetComponent<SpawnPoint>();
            if (sp == null) continue;

            if (sp.allowedType != unitType) continue;

            bool ownerMatch =
                !string.IsNullOrEmpty(sp.ownerCountryTag)
                    ? sp.ownerCountryTag == ownerCountryTag
                    : sp.countryName == ownerCountryTag;

            bool tileMatch = true;
            if (!string.IsNullOrEmpty(requiredSpawnTileTag))
                tileMatch = sp.spawnTileTag == requiredSpawnTileTag;

            if (!(ownerMatch && tileMatch)) continue;

            validSpawns.Add(sp);
        }


        if (validSpawns.Count == 0)
        {
            Debug.LogWarning(
                $"[MainUnitManager] No spawn points found. owner='{ownerCountryTag}', type='{unitType}', requiredTile='{requiredSpawnTileTag}'"
            );
            return null;
        }

        GameObject prefab =
            unitType == UnitType.Boat ? boatUnitPrefab :
            unitType == UnitType.Plane ? planeUnitPrefab :
            landUnitPrefab;

        if (prefab == null)
        {
            Debug.LogError("[MainUnitManager] Prefab for unitType is null");
            return null;
        }

        GameObject lastSpawned = null;

        for (int i = 0; i < count; i++)
        {
            SpawnPoint sp = validSpawns[i % validSpawns.Count];
            Transform spawn = sp.transform;

            int spId = sp.gameObject.GetInstanceID();
            int useIndex = spawnUseIndexBySpawnPoint.TryGetValue(spId, out int v) ? v : 0;
            spawnUseIndexBySpawnPoint[spId] = useIndex + 1;

            Vector3 offset = GetSpawnOffset(useIndex, unitType);
            Vector3 spawnPos = spawn.position + offset;

            GameObject unitObj = Instantiate(prefab, spawnPos, spawn.rotation);

            MainUnit unit = unitObj.GetComponent<MainUnit>();
            if (unit == null)
            {
                Debug.LogError("[MainUnitManager] Spawned prefab missing MainUnit component.");
                Destroy(unitObj);
                continue;
            }

            unit.ownerID = playerID;
            unit.playerColor = playerColor;
            unit.unitType = unitType;

            string spawnTile = string.IsNullOrEmpty(sp.spawnTileTag) ? ownerCountryTag : sp.spawnTileTag;
            unit.currentCountry = spawnTile;

            NetworkServer.Spawn(unitObj);
            allUnits.Add(unit);

            unit.RpcInitialize(playerID, playerColor, unitType);

            lastSpawned = unitObj;
        }

        RpcUpdateCountryOwnership(ownerCountryTag, playerColor);

        return lastSpawned;
    }

    [Server]
    public GameObject SpawnUnitsForCountryServer(string ownerCountryTag, int playerID, Color playerColor, int count, UnitType unitType)
    {
        return SpawnUnitsForCountryServer(ownerCountryTag, playerID, playerColor, count, unitType, null);
    }

    [Server]
    public bool HasSpawnPoint(string ownerCountryTag, UnitType unitType, string requiredSpawnTileTag)
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        foreach (var spObj in spawnPoints)
        {
            SpawnPoint sp = spObj.GetComponent<SpawnPoint>();
            if (sp == null) continue;
            if (sp.allowedType != unitType) continue;

            bool ownerMatch =
                !string.IsNullOrEmpty(sp.ownerCountryTag)
                    ? sp.ownerCountryTag == ownerCountryTag
                    : sp.countryName == ownerCountryTag;

            if (!ownerMatch) continue;

            if (!string.IsNullOrEmpty(requiredSpawnTileTag))
            {
                if (sp.spawnTileTag != requiredSpawnTileTag) continue;
            }

            return true;
        }

        return false;
    }

    private Vector3 GetSpawnOffset(int index, UnitType unitType)
    {
        const float goldenAngle = 2.39996323f;

        float maxR =
            unitType == UnitType.Boat ? boatSpawnRadius :
            unitType == UnitType.Plane ? planeSpawnRadius :
            landSpawnRadius;

        float r = spawnSpacing * Mathf.Sqrt(index);
        if (r > maxR) r = maxR;

        float a = index * goldenAngle;

        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
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

            GameObject countryObj = GameObject.FindWithTag(countryName);
            if (countryObj == null)
            {
                Debug.LogWarning($"[Server] Tile with tag '{countryName}' not found!");
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
                    StartCoroutine(SendRpcWithDelay(unit, unit.transform.position, 0.05f));
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

                if (!isBounce && countryComp != null)
                    countryComp.SetOwner(winnerID);

                if (winningUnits.Count > 0)
                {
                    Color winnerColor = winningUnits[0].playerColor;
                    RpcUpdateCountryOwnership(countryName, winnerColor);
                }
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

    [ClientRpc]
    public void RpcUpdateCountryOwnership(string countryTag, Color playerColor)
    {
        GameObject countryObj = GameObject.FindWithTag(countryTag);
        if (countryObj == null) return;

        Renderer[] renderers = countryObj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        foreach (var rend in renderers)
            rend.material.color = playerColor;
    }
}
