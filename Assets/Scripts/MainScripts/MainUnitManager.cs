using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class MainUnitManager : NetworkBehaviour
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
    [SerializeField] private float spawnSpacingMultiplier = 2f;

    [Header("Tile Packing (units on same country)")]
    [SerializeField] private float packDelay = 0.18f;
    [SerializeField] private float packSpacing = 4.0f;
    [SerializeField] private float packLandRadius = 12.0f;
    [SerializeField] private float packBoatRadius = 8.0f;
    [SerializeField] private float packPlaneRadius = 7.0f;

    [Tooltip("Max radius per type. Increase if units still overlap.")]
    [SerializeField] private float landSpawnRadius = 30.0f;
    [SerializeField] private float boatSpawnRadius = 20.0f;
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

        float spacing = spawnSpacing * Mathf.Max(0.01f, spawnSpacingMultiplier);

        float r = spacing * Mathf.Sqrt(index);
        if (r > maxR) r = maxR;

        float a = index * goldenAngle;

        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
    }

    private Vector3 GetPackOffset(int index, UnitType unitType)
    {
        const float goldenAngle = 2.39996323f;

        float maxR =
            unitType == UnitType.Boat ? packBoatRadius :
            unitType == UnitType.Plane ? packPlaneRadius :
            packLandRadius;

        float r = packSpacing * Mathf.Sqrt(index);
        if (r > maxR) r = maxR;

        float a = index * goldenAngle;

        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
    }

    private IEnumerator ApplyPackedPositions(Dictionary<MainUnit, Vector3> positions, float delay)
    {
        yield return new WaitForSeconds(delay);

        foreach (var kv in positions)
        {
            MainUnit u = kv.Key;
            if (u == null) continue;
            u.RpcMoveTo(kv.Value);
        }
    }

    private Dictionary<MainUnit, Vector3> BuildPackedPositions(Dictionary<MainUnit, string> finalTile)
    {
        Dictionary<string, List<MainUnit>> byTile = new Dictionary<string, List<MainUnit>>();

        for (int i = 0; i < allUnits.Count; i++)
        {
            MainUnit u = allUnits[i];
            if (u == null) continue;

            if (!finalTile.TryGetValue(u, out string t)) t = u.currentCountry;
            if (string.IsNullOrEmpty(t)) continue;

            if (!byTile.TryGetValue(t, out var list))
            {
                list = new List<MainUnit>();
                byTile[t] = list;
            }
            list.Add(u);
        }

        Dictionary<MainUnit, Vector3> result = new Dictionary<MainUnit, Vector3>();

        foreach (var kv in byTile)
        {
            string tileTag = kv.Key;
            List<MainUnit> units = kv.Value;

            Country c = FindCountryByTag(tileTag);
            if (c == null) continue;

            units.Sort((a, b) => a.netId.CompareTo(b.netId));

            Vector3 center = c.centerWorldPos;

            for (int i = 0; i < units.Count; i++)
            {
                MainUnit u = units[i];
                if (u == null) continue;

                Vector3 pos = center + GetPackOffset(i, u.unitType);
                result[u] = pos;
            }
        }

        return result;
    }


    [Server]
    public void ExecuteTurnServer()
    {
        List<MainUnit> movers = new List<MainUnit>();
        List<MainUnit> supporters = new List<MainUnit>();

        for (int i = 0; i < allUnits.Count; i++)
        {
            MainUnit u = allUnits[i];
            if (u == null) continue;
            if (u.currentOrder == null) continue;

            if (u.currentOrder.orderType == UnitOrderType.Move) movers.Add(u);
            else if (u.currentOrder.orderType == UnitOrderType.Support) supporters.Add(u);
        }

        Dictionary<string, List<MainUnit>> attackersByTarget = new Dictionary<string, List<MainUnit>>();
        for (int i = 0; i < movers.Count; i++)
        {
            MainUnit m = movers[i];
            if (m == null) continue;

            string t = m.currentOrder.targetCountry;
            if (string.IsNullOrEmpty(t)) continue;

            if (!attackersByTarget.TryGetValue(t, out var list))
            {
                list = new List<MainUnit>();
                attackersByTarget[t] = list;
            }
            list.Add(m);
        }

        HashSet<MainUnit> cutSupports = new HashSet<MainUnit>();
        for (int i = 0; i < supporters.Count; i++)
        {
            MainUnit s = supporters[i];
            if (s == null) continue;

            string tile = s.currentCountry;
            if (string.IsNullOrEmpty(tile)) continue;

            if (attackersByTarget.TryGetValue(tile, out var attackersHere))
            {
                for (int k = 0; k < attackersHere.Count; k++)
                {
                    MainUnit a = attackersHere[k];
                    if (a == null) continue;
                    if (a.ownerID != s.ownerID)
                    {
                        cutSupports.Add(s);
                        break;
                    }
                }
            }
        }

        Dictionary<MainUnit, int> supportPowerOnUnit = new Dictionary<MainUnit, int>();
        for (int i = 0; i < supporters.Count; i++)
        {
            MainUnit s = supporters[i];
            if (s == null) continue;
            if (cutSupports.Contains(s)) continue;

            if (s.currentOrder == null) continue;
            if (s.currentOrder.orderType != UnitOrderType.Support) continue;

            MainUnit supported = s.currentOrder.supportedUnit;
            if (supported == null) continue;

            string supportedDest =
                (supported.currentOrder != null && supported.currentOrder.orderType == UnitOrderType.Move)
                    ? supported.currentOrder.targetCountry
                    : supported.currentCountry;

            if (string.IsNullOrEmpty(supportedDest)) continue;
            if (s.currentOrder.targetCountry != supportedDest) continue;

            if (!supportPowerOnUnit.ContainsKey(supported))
                supportPowerOnUnit[supported] = 0;
            supportPowerOnUnit[supported] += 1;
        }

        Dictionary<MainUnit, Vector3> startPos = new Dictionary<MainUnit, Vector3>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            MainUnit u = allUnits[i];
            if (u == null) continue;
            startPos[u] = u.transform.position;
        }

        List<(MainUnit unit, string toTag, Vector3 toPos)> winners = new List<(MainUnit, string, Vector3)>();
        List<(MainUnit unit, Vector3 intoPos, Vector3 backPos)> bouncers = new List<(MainUnit, Vector3, Vector3)>();
        List<(MainUnit unit, string battleTag)> dislodged = new List<(MainUnit, string)>();

        Dictionary<MainUnit, string> finalTile = new Dictionary<MainUnit, string>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            MainUnit u = allUnits[i];
            if (u == null) continue;
            finalTile[u] = u.currentCountry;
        }

        foreach (var kvp in attackersByTarget)
        {
            string battleTag = kvp.Key;
            List<MainUnit> attackers = kvp.Value;

            Country battleCountry = FindCountryByTag(battleTag);
            if (battleCountry == null)
            {
                for (int i = 0; i < attackers.Count; i++)
                {
                    MainUnit a = attackers[i];
                    if (a != null) a.currentOrder = null;
                }
                continue;
            }

            List<MainUnit> defendersAll = GetUnitsOnTile(battleTag);
            List<MainUnit> defenders = new List<MainUnit>();
            for (int i = 0; i < defendersAll.Count; i++)
            {
                MainUnit d = defendersAll[i];
                if (d == null) continue;

                if (d.currentOrder != null && d.currentOrder.orderType == UnitOrderType.Move)
                {
                    string dt = d.currentOrder.targetCountry;
                    if (!string.IsNullOrEmpty(dt) && dt != battleTag) continue;
                }

                defenders.Add(d);
            }

            HashSet<int> involvedOwners = new HashSet<int>();
            for (int i = 0; i < attackers.Count; i++)
                if (attackers[i] != null) involvedOwners.Add(attackers[i].ownerID);
            for (int i = 0; i < defenders.Count; i++)
                if (defenders[i] != null) involvedOwners.Add(defenders[i].ownerID);

            Vector3 battlePos = battleCountry.centerWorldPos;

            if (involvedOwners.Count <= 1)
            {
                for (int i = 0; i < attackers.Count; i++)
                {
                    MainUnit a = attackers[i];
                    if (a == null) continue;

                    winners.Add((a, battleTag, battlePos));
                    finalTile[a] = battleTag;
                    a.currentOrder = null;
                }
                continue;
            }

            Dictionary<int, int> atkStrength = new Dictionary<int, int>();
            Dictionary<int, List<MainUnit>> atkUnitsByOwner = new Dictionary<int, List<MainUnit>>();

            for (int i = 0; i < attackers.Count; i++)
            {
                MainUnit a = attackers[i];
                if (a == null) continue;

                int owner = a.ownerID;

                if (!atkStrength.ContainsKey(owner)) atkStrength[owner] = 0;
                atkStrength[owner] += 1;

                if (supportPowerOnUnit.TryGetValue(a, out int sup))
                    atkStrength[owner] += sup;

                if (!atkUnitsByOwner.TryGetValue(owner, out var list))
                {
                    list = new List<MainUnit>();
                    atkUnitsByOwner[owner] = list;
                }
                list.Add(a);
            }

            Dictionary<int, int> defStrength = new Dictionary<int, int>();
            for (int i = 0; i < defenders.Count; i++)
            {
                MainUnit d = defenders[i];
                if (d == null) continue;

                int owner = d.ownerID;
                if (!defStrength.ContainsKey(owner)) defStrength[owner] = 0;
                defStrength[owner] += 1;

                if (supportPowerOnUnit.TryGetValue(d, out int ds))
                    defStrength[owner] += ds;
            }

            int bestAtkOwner = -1;
            int bestAtk = -1;
            bool atkTie = false;

            foreach (var s in atkStrength)
            {
                if (s.Value > bestAtk)
                {
                    bestAtk = s.Value;
                    bestAtkOwner = s.Key;
                    atkTie = false;
                }
                else if (s.Value == bestAtk)
                {
                    atkTie = true;
                }
            }

            int bestDef = 0;
            foreach (var s in defStrength)
                if (s.Value > bestDef) bestDef = s.Value;

            if (atkTie || bestAtk <= bestDef)
            {
                for (int i = 0; i < attackers.Count; i++)
                {
                    MainUnit a = attackers[i];
                    if (a == null) continue;
                    bouncers.Add((a, battlePos, startPos[a]));
                    a.currentOrder = null;
                }
                continue;
            }

            MainUnit winningMover = atkUnitsByOwner[bestAtkOwner][0];

            winners.Add((winningMover, battleTag, battlePos));
            finalTile[winningMover] = battleTag;
            winningMover.currentOrder = null;

            for (int i = 0; i < attackers.Count; i++)
            {
                MainUnit a = attackers[i];
                if (a == null) continue;
                if (a == winningMover) continue;

                bouncers.Add((a, battlePos, startPos[a]));
                a.currentOrder = null;
            }

            for (int i = 0; i < defenders.Count; i++)
            {
                MainUnit d = defenders[i];
                if (d == null) continue;
                if (d.ownerID == bestAtkOwner) continue;

                dislodged.Add((d, battleTag));
                finalTile[d] = null;
                d.currentOrder = null;
            }
        }

        HashSet<string> occupiedFinal = new HashSet<string>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            MainUnit u = allUnits[i];
            if (u == null) continue;
            if (finalTile.TryGetValue(u, out var t) && !string.IsNullOrEmpty(t))
                occupiedFinal.Add(t);
        }

        List<(MainUnit unit, string toTag, Vector3 toPos)> retreats = new List<(MainUnit, string, Vector3)>();
        List<MainUnit> toDestroy = new List<MainUnit>();

        for (int i = 0; i < dislodged.Count; i++)
        {
            MainUnit def = dislodged[i].unit;
            string battleTag = dislodged[i].battleTag;
            if (def == null) continue;

            Country battleCountry = FindCountryByTag(battleTag);
            if (battleCountry == null)
            {
                toDestroy.Add(def);
                continue;
            }

            string retreatTag = PickRetreatTile(def, battleCountry, occupiedFinal);
            if (string.IsNullOrEmpty(retreatTag))
            {
                toDestroy.Add(def);
                continue;
            }

            Country retreatCountry = FindCountryByTag(retreatTag);
            if (retreatCountry == null)
            {
                toDestroy.Add(def);
                continue;
            }

            occupiedFinal.Add(retreatTag);
            finalTile[def] = retreatTag;
            retreats.Add((def, retreatTag, retreatCountry.centerWorldPos));
        }

        for (int i = 0; i < bouncers.Count; i++)
        {
            var b = bouncers[i];
            if (b.unit == null) continue;
            StartCoroutine(BounceUnit(b.unit, b.intoPos, b.backPos, 0.08f));
        }

        for (int i = 0; i < winners.Count; i++)
        {
            var w = winners[i];
            if (w.unit == null) continue;

            w.unit.currentCountry = w.toTag;
            StartCoroutine(SendRpcWithDelay(w.unit, w.toPos, 0.05f));

            Country c = FindCountryByTag(w.toTag);
            if (c != null && !c.isOcean)
            {
                c.SetOwner(w.unit.ownerID);
                RpcUpdateCountryOwnership(w.toTag, w.unit.playerColor);
            }
        }

        for (int i = 0; i < retreats.Count; i++)
        {
            var r = retreats[i];
            if (r.unit == null) continue;

            r.unit.currentCountry = r.toTag;
            StartCoroutine(SendRpcWithDelay(r.unit, r.toPos, 0.05f));
        }

        Dictionary<MainUnit, Vector3> packedPositions = BuildPackedPositions(finalTile);
        StartCoroutine(ApplyPackedPositions(packedPositions, packDelay));

        for (int i = 0; i < toDestroy.Count; i++)
        {
            MainUnit u = toDestroy[i];
            if (u == null) continue;
            allUnits.Remove(u);
            NetworkServer.Destroy(u.gameObject);
        }

        for (int i = 0; i < allUnits.Count; i++)
        {
            MainUnit u = allUnits[i];
            if (u == null) continue;
            if (u.currentOrder != null) u.currentOrder = null;
        }

        for (int i = 0; i < MainPlayerController.allPlayers.Count; i++)
            MainPlayerController.allPlayers[i]?.RpcResetReady();
    }

    private Country FindCountryByTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        GameObject obj = GameObject.FindWithTag(tag);
        if (obj == null) return null;
        return obj.GetComponent<Country>() ?? obj.GetComponentInChildren<Country>() ?? obj.GetComponentInParent<Country>();
    }

    private List<MainUnit> GetUnitsOnTile(string tileTag)
    {
        List<MainUnit> list = new List<MainUnit>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            MainUnit u = allUnits[i];
            if (u == null) continue;
            if (u.currentCountry == tileTag) list.Add(u);
        }
        return list;
    }

    private string PickRetreatTile(MainUnit unit, Country fromCountry, HashSet<string> occupiedFinal)
    {
        if (unit == null || fromCountry == null) return null;

        List<Country> owned = new List<Country>();
        List<Country> unowned = new List<Country>();

        for (int i = 0; i < fromCountry.adjacentCountries.Count; i++)
        {
            Country n = fromCountry.adjacentCountries[i];
            if (n == null) continue;

            if (unit.unitType == UnitType.Land && n.isOcean) continue;
            if (unit.unitType == UnitType.Boat && !n.isOcean) continue;

            string t = n.gameObject.tag;
            if (occupiedFinal.Contains(t)) continue;

            if (n.ownerID == unit.ownerID) owned.Add(n);
            else if (n.ownerID == -1) unowned.Add(n);
        }

        if (owned.Count > 0) return owned[0].gameObject.tag;
        if (unowned.Count > 0) return unowned[0].gameObject.tag;
        return null;
    }

    private IEnumerator BounceUnit(MainUnit unit, Vector3 intoPos, Vector3 backPos, float delayBetween)
    {
        if (unit == null) yield break;
        unit.RpcMoveTo(intoPos);
        yield return new WaitForSeconds(delayBetween);
        unit.RpcMoveTo(backPos);
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
