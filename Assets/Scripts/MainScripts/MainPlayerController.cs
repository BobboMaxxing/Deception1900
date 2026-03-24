using Mirror;
#if MIRROR_BOUNCYCASTLE
using Mirror.BouncyCastle.Asn1.X509;
#endif
using System.Collections;
using System.Collections.Generic;
using TMPro;
#if UNITY_EDITOR
using UnityEditor.ShaderGraph;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainPlayerController : NetworkBehaviour
{
    [Header("Player Info")]
    [SyncVar] public int playerID;
    [SyncVar] public Color playerColor = Color.white;
    [SyncVar] public string chosenCountry;

    [Header("Hover Highlight")]
    private PulsingHighlighter currentHoverHighlighter;
    private Country currentHoverCountry;

    [Header("Camera & Layers")]
    public Camera playerCamera;
    [SerializeField] private LayerMask countryLayer;

    [Header("UI")]
    private Button confirmButton;
    private Button cancelButton;
    private TMP_Text selectedCountryText;
    private TMP_Text moveStatusText;
    private Button confirmMoveButton;
    private Button cancelMoveButton;

    private Button buildLandButton;
    private Button buildBoatButton;
    private Button buildPlaneButton;
    private Button buildPassButton;

    [Header("Game References")]
    public MainUnitManager unitManager;
    public CameraMovment cameraMovment;

    private string pendingCountry;
    public bool hasChosenCountry = false;
    private List<GameObject> highlightedObjects = new List<GameObject>();
    private MainUnit selectedUnit;
    public bool canIssueOrders = true;

    [SyncVar] private bool isReady = false;
    public static Dictionary<int, bool> playersReady = new Dictionary<int, bool>();
    public static List<MainPlayerController> allPlayers = new List<MainPlayerController>();

    private UnitType pendingBuildType = UnitType.Land;
    private bool buildTypeSelected = false;
    private bool buildPhaseActiveLocal = false;
    private int remainingBuildsLocal = 0;
    private bool waitingBuildResponse = false;
    [SerializeField] private KeyCode reopenBuildSelectionKey = KeyCode.F;
    private bool buildTableSelectionOpen = false;

    private Dictionary<MainUnit, int> localSupportCounts = new Dictionary<MainUnit, int>();
    private List<PulsingHighlighter> activeBuildHighlighters = new List<PulsingHighlighter>();
    private List<Country> activeBuildHighlightCountries = new List<Country>();

    void OnDestroy()
    {
        ClearBuildHighlights();
        ClearHoverHighlight();

        if (allPlayers.Contains(this)) allPlayers.Remove(this);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (!playersReady.ContainsKey(playerID))
            playersReady[playerID] = false;

        if (!allPlayers.Contains(this))
            allPlayers.Add(this);

        if (unitManager == null)
            unitManager = MainUnitManager.Instance;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera != null) playerCamera.enabled = true;

        if (unitManager == null) unitManager = MainUnitManager.Instance;

        LocalPlayerSetup setup = Object.FindFirstObjectByType<LocalPlayerSetup>();
        setup?.Setup(this);

        UpdateReadyUI();
        UpdateOrderButtonsUI();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (buildPhaseActiveLocal)
        {
            canIssueOrders = false;

            if (!hasChosenCountry)
            {
                HandleCountryHover();
                HandleCountrySelection();
            }
            else
            {
                HandleBuildPhaseInput();
            }

            UpdateOrderButtonsUI();
            return;
        }

        canIssueOrders = true;

        HandleCountryHover();

        if (!hasChosenCountry)
        {
            HandleCountrySelection();
            UpdateOrderButtonsUI();
            return;
        }

        HandleUnitSelection();
        UpdateOrderButtonsUI();
    }


    private MainUnit GetUnitFromHit(RaycastHit hit)
    {
        return hit.collider.GetComponentInParent<MainUnit>()
            ?? hit.collider.GetComponentInChildren<MainUnit>();
    }

    private Country GetCountryFromHitRecursive(RaycastHit hit)
    {
        if (hit.collider == null) return null;

        Country country = hit.collider.GetComponent<Country>();
        if (country != null) return country;

        Transform parent = hit.collider.transform.parent;
        while (parent != null)
        {
            country = parent.GetComponent<Country>();
            if (country != null) return country;
            parent = parent.parent;
        }

        country = hit.collider.GetComponentInChildren<Country>();
        return country;
    }

    private Country pendingCountryComp;

    void HandleCountrySelection()
    {
        if (!isLocalPlayer || Input.GetMouseButtonDown(0) == false) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, countryLayer, QueryTriggerInteraction.Collide))
        {
            Country countryComp = GetCountryFromHitRecursive(hit);
            if (countryComp == null)
                return;

            if (!countryComp.CanBeSelected())
                return;

            pendingCountryComp = countryComp;
            pendingCountry = countryComp.tag;

            selectedCountryText?.SetText("Selected: " + countryComp.name);
            confirmButton?.gameObject.SetActive(true);
            cancelButton?.gameObject.SetActive(true);
        }
    }

    public void ConfirmCountryChoice()
    {
        if (pendingCountryComp == null)
            return;

        if (!pendingCountryComp.CanBeSelected())
            return;

        List<Country> allCountries = pendingCountryComp.GetAllSelectableCountries();

        foreach (var c in allCountries)
        {
            c.SetOwner(playerID);

            if (!isServer)
                CmdAssignCountryToPlayer(c.tag, playerID);
        }

        chosenCountry = pendingCountryComp.tag;
        hasChosenCountry = true;

        confirmButton?.gameObject.SetActive(false);
        cancelButton?.gameObject.SetActive(false);
        selectedCountryText?.SetText("Chosen: " + pendingCountryComp.name);

        AssignPlayerColorFromCountry();

        if (!isServer)
        {
            CmdSetPlayerColor(playerColor);
            CmdStartInitialBuildPhase();
        }
        else
        {
            MainGameManager.Instance.StartInitialBuildPhaseForPlayer(playerID);
        }

        pendingCountryComp = null;
        pendingCountry = "";
    }

    [Command]
    private void CmdSetPlayerColor(Color color)
    {
        playerColor = color;
    }

    [Command]
    private void CmdStartInitialBuildPhase()
    {
        MainGameManager.Instance.StartInitialBuildPhaseForPlayer(playerID);
    }

    [Command]
    private void CmdAssignCountryToPlayer(string countryTag, int playerID)
    {
        GameObject countryObj = GameObject.FindWithTag(countryTag);
        if (countryObj == null)
            return;

        Country countryComp = countryObj.GetComponent<Country>();
        if (countryComp == null)
            return;

        if (countryComp.ownerID == -1)
        {
            countryComp.SetOwner(playerID);
            RpcUpdateCountryOwnership(countryTag, playerID);
        }
    }

    [ClientRpc]
    private void RpcUpdateCountryOwnership(string countryTag, int playerID)
    {
        GameObject countryObj = GameObject.FindWithTag(countryTag);
        if (countryObj == null) return;

        Country countryComp = countryObj.GetComponent<Country>();
        if (countryComp != null)
            countryComp.SetOwner(playerID);
    }

    private Country FindCountryByTagRecursive(string tag)
    {
        GameObject obj = GameObject.FindWithTag(tag);
        if (obj == null) return null;

        Country country = obj.GetComponent<Country>();
        if (country != null) return country;

        country = obj.GetComponentInChildren<Country>();
        if (country != null) return country;

        country = obj.GetComponentInParent<Country>();
        if (country != null) return country;

        return null;
    }

    public void CancelCountryChoice()
    {
        pendingCountry = "";
        selectedCountryText?.SetText("Selection cleared");
        confirmButton?.gameObject.SetActive(false);
        cancelButton?.gameObject.SetActive(false);
        cameraMovment?.ResetCamera();
    }

    void AssignPlayerColorFromCountry()
    {
        if (string.IsNullOrEmpty(chosenCountry))
            return;

        Country chosenCountryComp = FindCountryByTagRecursive(chosenCountry);

        if (chosenCountryComp == null)
        {
            playerColor = Color.gray;
            return;
        }

        playerColor = chosenCountryComp.countryColor;
    }

    void HandleCountryHover()
    {
        if (!isLocalPlayer) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, countryLayer, QueryTriggerInteraction.Collide))
        {
            Country country = GetCountryFromHitRecursive(hit);

            if (country != null && country != currentHoverCountry)
            {
                ClearHoverHighlight();

                Renderer rend = country.GetComponentInChildren<Renderer>();
                if (rend == null) return;

                PulsingHighlighter highlighter =
                    rend.GetComponent<PulsingHighlighter>() ??
                    rend.gameObject.AddComponent<PulsingHighlighter>();

                highlighter.StartPulse();

                currentHoverCountry = country;
                currentHoverHighlighter = highlighter;
            }

            return;
        }

        ClearHoverHighlight();
    }

    void ClearHoverHighlight()
    {
        if (currentHoverHighlighter != null)
        {
            currentHoverHighlighter.StopPulse();
            currentHoverHighlighter = null;
            currentHoverCountry = null;
        }
    }

    private Renderer GetCountryHighlightRenderer(Country country)
    {
        if (country == null) return null;
        return country.GetComponentInChildren<Renderer>();
    }

    private void ClearBuildHighlights()
    {
        for (int i = 0; i < activeBuildHighlighters.Count; i++)
        {
            if (activeBuildHighlighters[i] != null)
                activeBuildHighlighters[i].StopPulse();
        }

        activeBuildHighlighters.Clear();
        activeBuildHighlightCountries.Clear();
    }

    private void AddBuildHighlight(Country country)
    {
        if (country == null) return;
        if (activeBuildHighlightCountries.Contains(country)) return;

        Renderer rend = GetCountryHighlightRenderer(country);
        if (rend == null) return;

        PulsingHighlighter highlighter =
            rend.GetComponent<PulsingHighlighter>() ??
            rend.gameObject.AddComponent<PulsingHighlighter>();

        highlighter.StartPulse();

        activeBuildHighlighters.Add(highlighter);
        activeBuildHighlightCountries.Add(country);
    }

    private bool CanBuildLandOn(Country country)
    {
        if (country == null) return false;
        if (country.ownerID != playerID) return false;
        if (country.isOcean) return false;
        if (MainUnitManager.Instance == null) return false;

        return MainUnitManager.Instance.HasSpawnPoint(country.tag, UnitType.Land, null);
    }

    private bool PlayerMeetsPlaneBuildRequirement()
    {
        Country[] allCountries = Object.FindObjectsOfType<Country>();

        int totalCenters = 0;
        int ownedCenters = 0;

        for (int i = 0; i < allCountries.Length; i++)
        {
            Country c = allCountries[i];
            if (c == null) continue;
            if (!c.isSupplyCenter) continue;

            totalCenters++;
            if (c.ownerID == playerID)
                ownedCenters++;
        }

        int required = Mathf.CeilToInt(totalCenters * 0.3f);
        return ownedCenters >= required;
    }

    private bool CanBuildPlaneOn(Country country)
    {
        if (country == null) return false;
        if (country.ownerID != playerID) return false;
        if (country.isOcean) return false;
        if (!country.isAirfield) return false;
        if (!PlayerMeetsPlaneBuildRequirement()) return false;
        if (MainUnitManager.Instance == null) return false;

        return MainUnitManager.Instance.HasSpawnPoint(country.tag, UnitType.Plane, null);
    }

    private bool CanBuildBoatOn(Country oceanCountry)
    {
        if (oceanCountry == null) return false;
        if (!oceanCountry.isOcean) return false;
        if (MainUnitManager.Instance == null) return false;

        for (int i = 0; i < oceanCountry.adjacentCountries.Count; i++)
        {
            Country adj = oceanCountry.adjacentCountries[i];
            if (adj == null) continue;
            if (adj.ownerID != playerID) continue;

            if (MainUnitManager.Instance.HasSpawnPoint(adj.tag, UnitType.Boat, oceanCountry.tag))
                return true;
        }

        return false;
    }

    private void RefreshBuildHighlights()
    {
        ClearBuildHighlights();

        if (!buildPhaseActiveLocal) return;
        if (!buildTypeSelected) return;

        Country[] allCountries = Object.FindObjectsOfType<Country>();

        for (int i = 0; i < allCountries.Length; i++)
        {
            Country country = allCountries[i];
            if (country == null) continue;

            bool valid = false;

            if (pendingBuildType == UnitType.Land)
                valid = CanBuildLandOn(country);
            else if (pendingBuildType == UnitType.Boat)
                valid = CanBuildBoatOn(country);
            else if (pendingBuildType == UnitType.Plane)
                valid = CanBuildPlaneOn(country);

            if (valid)
                AddBuildHighlight(country);
        }
    }

    private BuildPileSelectable GetBuildPileFromHit(RaycastHit hit)
    {
        return hit.collider.GetComponentInParent<BuildPileSelectable>()
            ?? hit.collider.GetComponentInChildren<BuildPileSelectable>();
    }

    private void OpenBuildTableSelection()
    {
        if (!buildPhaseActiveLocal) return;
        if (remainingBuildsLocal <= 0) return;

        buildTableSelectionOpen = true;
        buildTypeSelected = false;
        ClearBuildHighlights();

        if (cameraMovment != null)
        {
            cameraMovment.SetFocusClickEnabled(false);
            cameraMovment.SetManualInputLocked(true);
            cameraMovment.MoveToBuildTable();
        }

        moveStatusText?.SetText($"Build points: {remainingBuildsLocal}. Choose a unit from the pile.");
    }

    private void CloseBuildTableSelectionToMap()
    {
        buildTableSelectionOpen = false;

        if (cameraMovment != null)
        {
            cameraMovment.SetFocusClickEnabled(false);
            cameraMovment.SetManualInputLocked(false);
            cameraMovment.ResetCamera();
        }
    }

    private void HandleBuildPileSelection()
    {
        if (!buildTableSelectionOpen) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        Debug.Log($"Pile click hits: {hits.Length}");

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Debug.Log($"Hit object: {hits[i].collider.name}");

            BuildPileSelectable pile = GetBuildPileFromHit(hits[i]);
            if (pile == null) continue;

            if (pile.unitType == UnitType.Land)
                SelectBuildLand();
            else if (pile.unitType == UnitType.Boat)
                SelectBuildBoat();
            else if (pile.unitType == UnitType.Plane)
                SelectBuildPlane();

            CloseBuildTableSelectionToMap();
            return;
        }
    }

    private void HandleBuildPhaseInput()
    {
        if (remainingBuildsLocal > 0 && Input.GetKeyDown(reopenBuildSelectionKey))
            OpenBuildTableSelection();

        if (buildTableSelectionOpen)
            HandleBuildPileSelection();
        else
            HandleCountryHover();
    }

    void HandleUnitSelection()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, 9999f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (selectedUnit != null && isShiftHeld)
        {
            MainUnit targetUnit = null;

            for (int i = 0; i < hits.Length; i++)
            {
                MainUnit u = GetUnitFromHit(hits[i]);
                if (u == null) continue;
                if (u == selectedUnit) continue;
                targetUnit = u;
                break;
            }

            if (targetUnit == null)
            {
                moveStatusText?.SetText("Support: Click another unit to support.");
                ClearSelectedUnit();
                ShowMoveButtons(false);
                return;
            }

            string supportTarget =
                (targetUnit.currentOrder != null && targetUnit.currentOrder.orderType == UnitOrderType.Move)
                    ? targetUnit.currentOrder.targetCountry
                    : targetUnit.currentCountry;

            if (string.IsNullOrEmpty(supportTarget))
            {
                moveStatusText?.SetText("Support failed: target has no tile.");
                ClearSelectedUnit();
                ShowMoveButtons(false);
                return;
            }

            if (!CanSupportTo(selectedUnit, supportTarget))
            {
                moveStatusText?.SetText("Illegal support (must be adjacent).");
                ClearSelectedUnit();
                ShowMoveButtons(false);
                return;
            }

            CancelReadyIfNeeded();
            SetSupportOrderLocal(selectedUnit, targetUnit, supportTarget);
            CmdSupportUnit(selectedUnit.netId, targetUnit.netId, supportTarget);
            StartCoroutine(RebuildLocalSupportVisualsNextFrame());

            moveStatusText?.SetText("Support queued.");
            ClearSelectedUnit();
            ShowMoveButtons(false);
            return;
        }

        MainUnit hitUnit = null;
        for (int i = 0; i < hits.Length; i++)
        {
            hitUnit = GetUnitFromHit(hits[i]);
            if (hitUnit != null) break;
        }

        if (hitUnit != null && hitUnit.ownerID == playerID)
        {
            if (hitUnit.currentOrder != null)
                CancelReadyIfNeeded();
            ClearOrderLocal(hitUnit);

            SetSelectedUnit(hitUnit);
            ShowMoveButtons(true);
            return;
        }

        Country hitTile = null;
        for (int i = 0; i < hits.Length; i++)
        {
            hitTile = GetCountryFromHitRecursive(hits[i]);
            if (hitTile != null) break;
        }

        if (selectedUnit == null) return;

        if (hitTile == null)
        {
            ClearSelectedUnit();
            ShowMoveButtons(false);
            return;
        }

        Country targetCountryComp = hitTile;
        Country fromCountryComp = FindCountryByTagRecursive(selectedUnit.currentCountry);

        if (targetCountryComp == null || fromCountryComp == null)
        {
            ClearSelectedUnit();
            ShowMoveButtons(false);
            return;
        }

        if (selectedUnit.unitType == UnitType.Boat)
        {
            if (!targetCountryComp.isOcean)
            {
                moveStatusText?.SetText("Boats can only move on oceans.");
                ClearSelectedUnit();
                ShowMoveButtons(false);
                return;
            }

            bool boatDirect = fromCountryComp.adjacentCountries.Contains(targetCountryComp);
            if (!boatDirect)
            {
                moveStatusText?.SetText("Illegal move.");
                ClearSelectedUnit();
                ShowMoveButtons(false);
                return;
            }
        }
        else if (selectedUnit.unitType == UnitType.Plane)
        {
            if (!CanPlaneReach(fromCountryComp, targetCountryComp))
            {
                moveStatusText?.SetText("Planes can only move between reachable airfields.");
                ClearSelectedUnit();
                ShowMoveButtons(false);
                return;
            }
        }
        else
        {
            bool direct = fromCountryComp.adjacentCountries.Contains(targetCountryComp);
            bool bridge = CanLandBridgeMove(selectedUnit, fromCountryComp, targetCountryComp);

            if (!direct && !bridge)
            {
                moveStatusText?.SetText("Illegal move.");
                ClearSelectedUnit();
                ShowMoveButtons(false);
                return;
            }
        }

        Vector3 targetPos = targetCountryComp.centerWorldPos;
        selectedUnit.ShowLocalMoveLine(targetPos);

        CancelReadyIfNeeded();
        SetMoveOrderLocal(selectedUnit, targetCountryComp.tag, targetPos);
        CmdMoveUnit(selectedUnit.netId, targetCountryComp.tag, targetPos);
        StartCoroutine(RebuildLocalSupportVisualsNextFrame());

        ClearSelectedUnit();
        ShowMoveButtons(false);
    }

    private bool HasFriendlyBoatOnOcean(string oceanTag, int ownerId)
    {
        MainUnit[] units = Object.FindObjectsOfType<MainUnit>();
        for (int i = 0; i < units.Length; i++)
        {
            MainUnit u = units[i];
            if (u == null) continue;
            if (u.ownerID != ownerId) continue;
            if (u.unitType != UnitType.Boat) continue;
            if (u.currentCountry != oceanTag) continue;
            return true;
        }
        return false;
    }


    private bool CanLandBridgeMove(MainUnit unit, Country from, Country to)
    {
        if (unit == null || from == null || to == null) return false;
        if (unit.unitType != UnitType.Land) return false;
        if (from.isOcean) return false;
        if (to.isOcean) return false;

        for (int i = 0; i < from.adjacentCountries.Count; i++)
        {
            Country ocean = from.adjacentCountries[i];
            if (ocean == null) continue;
            if (!ocean.isOcean) continue;

            bool toTouchesSameOcean = false;
            for (int j = 0; j < to.adjacentCountries.Count; j++)
            {
                Country toAdj = to.adjacentCountries[j];
                if (toAdj == null) continue;
                if (!toAdj.isOcean) continue;

                if (toAdj.gameObject.tag == ocean.gameObject.tag)
                {
                    toTouchesSameOcean = true;
                    break;
                }
            }

            if (!toTouchesSameOcean) continue;

            if (HasFriendlyBoatOnOcean(ocean.gameObject.tag, unit.ownerID))
                return true;
        }

        return false;
    }

    private void SetSelectedUnit(MainUnit unit)
    {
        if (selectedUnit != null)
            selectedUnit.SetSelectedVisual(false);

        selectedUnit = unit;

        if (selectedUnit != null)
            selectedUnit.SetSelectedVisual(true);
    }

    private void ClearSelectedUnit()
    {
        if (selectedUnit != null)
            selectedUnit.SetSelectedVisual(false);

        selectedUnit = null;
    }

    public void ConfirmMoves()
    {
        if (!isLocalPlayer) return;

        moveStatusText?.SetText("Waiting for other players...");
        CmdConfirmMoves();
    }

    public void CancelMoves()
    {
        if (!isLocalPlayer) return;

        moveStatusText?.SetText("Ready canceled.");
        CmdCancelReady();
    }

    [Command]
    private void CmdMoveUnit(uint unitNetId, string targetCountryTag, Vector3 targetPos)
    {
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity identity)) return;

        MainUnit unit = identity.GetComponent<MainUnit>();
        if (unit == null || unit.ownerID != playerID) return;

        Country fromCountry = FindCountryByTagRecursive(unit.currentCountry);
        Country toCountry = FindCountryByTagRecursive(targetCountryTag);

        if (fromCountry == null || toCountry == null) return;

        bool direct = fromCountry.adjacentCountries.Contains(toCountry);

        if (unit.unitType == UnitType.Boat)
        {
            if (!toCountry.isOcean) return;
            if (!direct) return;
        }
        else if (unit.unitType == UnitType.Plane)
        {
            if (!CanPlaneReachServer(fromCountry, toCountry)) return;
        }
        else
        {
            if (toCountry.isOcean) return;

            bool bridge = CanLandBridgeMoveServer(unit.ownerID, fromCountry, toCountry);

            if (!direct && !bridge) return;
        }

        unit.currentOrder = new PlayerUnitOrder
        {
            orderType = UnitOrderType.Move,
            targetCountry = targetCountryTag,
            targetPosition = targetPos
        };
        StartCoroutine(RebuildLocalSupportVisualsNextFrame());
    }

    private bool CanSupportTo(MainUnit supporter, string targetTileTag)
    {
        if (supporter == null) return false;
        if (string.IsNullOrEmpty(targetTileTag)) return false;

        Country from = FindCountryByTagRecursive(supporter.currentCountry);
        Country to = FindCountryByTagRecursive(targetTileTag);

        if (from == null || to == null) return false;

        if (supporter.unitType == UnitType.Plane)
        {
            if (!from.isAirfield) return false;
            return from.adjacentCountries.Contains(to) || from.planeAdjacentCountries.Contains(to);
        }

        if (!from.adjacentCountries.Contains(to)) return false;

        if (supporter.unitType == UnitType.Boat)
            return true;

        return !to.isOcean;
    }

    [Command]
    private void CmdSupportUnit(uint supporterNetId, uint supportedNetId, string supportTargetCountry)
    {

        StartCoroutine(RebuildLocalSupportVisualsNextFrame());
        if (!NetworkServer.spawned.TryGetValue(supporterNetId, out NetworkIdentity supId)) return;
        if (!NetworkServer.spawned.TryGetValue(supportedNetId, out NetworkIdentity targetId)) return;


        MainUnit supporter = supId.GetComponent<MainUnit>();
        MainUnit supported = targetId.GetComponent<MainUnit>();
        if (supporter == null || supported == null) return;

        if (supporter.ownerID != playerID) return;

        if (string.IsNullOrEmpty(supportTargetCountry)) return;
        if (!CanSupportToServer(supporter, supportTargetCountry)) return;

        supporter.currentOrder = new PlayerUnitOrder
        {
            orderType = UnitOrderType.Support,
            supportedUnit = supported,
            targetCountry = supportTargetCountry
        };
    }
    private IEnumerator RebuildLocalSupportVisualsNextFrame()
    {
        yield return null;
        RebuildLocalSupportVisuals();
    }
    private void RebuildLocalSupportVisuals()
    {
        foreach (var kv in localSupportCounts)
        {
            if (kv.Key != null)
                kv.Key.ClearLocalIncomingSupportCount();
        }

        localSupportCounts.Clear();

        MainUnit[] allUnits = Object.FindObjectsOfType<MainUnit>();
        for (int i = 0; i < allUnits.Length; i++)
        {
            MainUnit unit = allUnits[i];
            if (unit == null) continue;
            if (unit.ownerID != playerID) continue;
            if (unit.currentOrder == null) continue;
            if (unit.currentOrder.orderType != UnitOrderType.Support) continue;
            if (unit.currentOrder.supportedUnit == null) continue;

            MainUnit supported = unit.currentOrder.supportedUnit;
            if (!localSupportCounts.ContainsKey(supported))
                localSupportCounts[supported] = 0;

            localSupportCounts[supported]++;
        }

        foreach (var kv in localSupportCounts)
        {
            if (kv.Key != null)
                kv.Key.SetLocalIncomingSupportCount(kv.Value);
        }
    }

    private void ClearAllLocalSupportVisuals()
    {
        foreach (var kv in localSupportCounts)
        {
            if (kv.Key != null)
                kv.Key.ClearLocalIncomingSupportCount();
        }

        localSupportCounts.Clear();
    }

    private void CancelReadyIfNeeded()
    {
        if (!isLocalPlayer) return;
        if (!hasChosenCountry) return;
        if (!isReady) return;

        CmdCancelReady();
    }
    private bool CanSupportToServer(MainUnit supporter, string targetTileTag)
    {
        if (supporter == null) return false;
        if (string.IsNullOrEmpty(targetTileTag)) return false;

        Country from = FindCountryByTagRecursive(supporter.currentCountry);
        Country to = FindCountryByTagRecursive(targetTileTag);

        if (from == null || to == null) return false;

        if (supporter.unitType == UnitType.Plane)
        {
            if (!from.isAirfield) return false;
            return from.adjacentCountries.Contains(to) || from.planeAdjacentCountries.Contains(to);
        }

        if (!from.adjacentCountries.Contains(to)) return false;

        if (supporter.unitType == UnitType.Boat)
            return true;

        return !to.isOcean;
    }

    private void ClearOrderLocal(MainUnit unit)
    {
        if (unit == null) return;

        if (unit.currentOrder != null)
            CmdClearUnitOrder(unit.netId);

        unit.currentOrder = null;
        unit.ClearLocalMoveLine();
        StartCoroutine(RebuildLocalSupportVisualsNextFrame());
    }
    private void SetMoveOrderLocal(MainUnit unit, string targetCountryTag, Vector3 targetPos)
    {
        if (unit == null) return;

        unit.currentOrder = new PlayerUnitOrder
        {
            orderType = UnitOrderType.Move,
            targetCountry = targetCountryTag,
            targetPosition = targetPos
        };
    }

    private void SetSupportOrderLocal(MainUnit supporter, MainUnit supported, string supportTargetCountry)
    {
        if (supporter == null) return;

        supporter.currentOrder = new PlayerUnitOrder
        {
            orderType = UnitOrderType.Support,
            supportedUnit = supported,
            targetCountry = supportTargetCountry
        };
    }

    [Command]
    private void CmdClearUnitOrder(uint unitNetId)
    {
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity identity)) return;

        MainUnit unit = identity.GetComponent<MainUnit>();
        if (unit == null) return;
        if (unit.ownerID != playerID) return;

        unit.currentOrder = null;
    }
    private bool CanPlaneReach(Country from, Country to)
    {
        if (from == null || to == null) return false;
        if (!from.isAirfield || !to.isAirfield) return false;

        if (from.adjacentCountries.Contains(to)) return true;
        if (from.planeAdjacentCountries.Contains(to)) return true;

        return false;
    }

    private bool CanPlaneReachServer(Country from, Country to)
    {
        if (from == null || to == null) return false;
        if (!from.isAirfield || !to.isAirfield) return false;

        if (from.adjacentCountries.Contains(to)) return true;
        if (from.planeAdjacentCountries.Contains(to)) return true;

        return false;
    }
    private bool CanLandBridgeMoveServer(int ownerId, Country from, Country to)
    {
        if (from == null || to == null) return false;
        if (from.isOcean) return false;
        if (to.isOcean) return false;

        for (int i = 0; i < from.adjacentCountries.Count; i++)
        {
            Country ocean = from.adjacentCountries[i];
            if (ocean == null) continue;
            if (!ocean.isOcean) continue;

            bool toTouchesSameOcean = false;
            for (int j = 0; j < to.adjacentCountries.Count; j++)
            {
                Country toAdj = to.adjacentCountries[j];
                if (toAdj == null) continue;
                if (!toAdj.isOcean) continue;

                if (toAdj.gameObject.tag == ocean.gameObject.tag)
                {
                    toTouchesSameOcean = true;
                    break;
                }
            }

            if (!toTouchesSameOcean) continue;

            if (HasFriendlyBoatOnOceanServer(ocean.gameObject.tag, ownerId))
                return true;
        }

        return false;
    }

    private bool HasFriendlyBoatOnOceanServer(string oceanTag, int ownerId)
    {
        MainUnit[] units = Object.FindObjectsOfType<MainUnit>();
        for (int i = 0; i < units.Length; i++)
        {
            MainUnit u = units[i];
            if (u == null) continue;
            if (u.ownerID != ownerId) continue;
            if (u.unitType != UnitType.Boat) continue;
            if (u.currentCountry != oceanTag) continue;
            return true;
        }
        return false;
    }


    [Command]
    private void CmdConfirmMoves()
    {
        if (unitManager == null)
            unitManager = MainUnitManager.Instance;

        if (!playersReady.ContainsKey(playerID))
            playersReady[playerID] = false;

        playersReady[playerID] = true;

        RpcUpdateReadyUI();

        if (AreAllPlayersReady())
        {
            unitManager.ExecuteTurnServer();
            MainGameManager.Instance?.NextTurnServer();

            foreach (var p in allPlayers)
                p.RpcResetReady();
        }
    }

    [Command]
    private void CmdCancelReady()
    {
        if (!playersReady.ContainsKey(playerID)) return;

        playersReady[playerID] = false;
        RpcUpdateReadyUI();
    }

    private bool AreAllPlayersReady()
    {
        foreach (var kv in playersReady)
        {
            if (!kv.Value)
                return false;
        }
        return true;
    }

    [ClientRpc]
    public void RpcUpdateReadyUI()
    {
        UpdateReadyUI();

        if (isLocalPlayer && playersReady.TryGetValue(playerID, out bool ready))
            isReady = ready;

        UpdateOrderButtonsUI();
    }

    private void UpdateReadyUI()
    {
        if (moveStatusText == null) return;
        int readyCount = 0;
        foreach (var kv in playersReady)
            if (kv.Value) readyCount++;
        moveStatusText.text = $"Players ready: {readyCount}/{allPlayers.Count}";
    }

    [ClientRpc]
    public void RpcResetReady()
    {
        isReady = false;

        MainUnit[] allUnits = Object.FindObjectsOfType<MainUnit>();
        for (int i = 0; i < allUnits.Length; i++)
        {
            MainUnit unit = allUnits[i];
            if (unit == null) continue;
            if (unit.ownerID != playerID) continue;

            unit.currentOrder = null;
            unit.ClearLocalMoveLine();
            unit.ClearLocalIncomingSupportCount();
        }

        ClearAllLocalSupportVisuals();

        if (playersReady.ContainsKey(playerID))
            playersReady[playerID] = false;

        UpdateReadyUI();
        UpdateOrderButtonsUI();
    }

    void ShowMoveButtons(bool state)
    {
        confirmMoveButton?.gameObject.SetActive(state);
        cancelMoveButton?.gameObject.SetActive(state);
    }

    public void SetupUIReferences(TMP_Text selectedCountryTextRef, TMP_Text moveStatusTextRef,
        Button confirmButtonRef, Button cancelButtonRef, Button confirmMoveButtonRef, Button cancelMoveButtonRef)
    {
        selectedCountryText = selectedCountryTextRef;
        moveStatusText = moveStatusTextRef;
        confirmButton = confirmButtonRef;
        cancelButton = cancelButtonRef;
        confirmMoveButton = confirmMoveButtonRef;
        cancelMoveButton = cancelMoveButtonRef;
    }

    public void SetupBuildUI(Button landButtonRef, Button boatButtonRef, Button planeButtonRef, Button passButtonRef)
    {
        buildLandButton = landButtonRef;
        buildBoatButton = boatButtonRef;
        buildPlaneButton = planeButtonRef;
        buildPassButton = passButtonRef;

        if (buildLandButton != null)
        {
            buildLandButton.gameObject.SetActive(false);
            buildLandButton.interactable = false;
        }

        if (buildBoatButton != null)
        {
            buildBoatButton.gameObject.SetActive(false);
            buildBoatButton.interactable = false;
        }

        if (buildPlaneButton != null)
        {
            buildPlaneButton.gameObject.SetActive(false);
            buildPlaneButton.interactable = false;
        }

        if (buildPassButton != null)
        {
            buildPassButton.gameObject.SetActive(false);
            buildPassButton.onClick.RemoveAllListeners();
            buildPassButton.onClick.AddListener(PassBuildPhase);
        }
    }

    private void UpdateOrderButtonsUI()
    {
        if (confirmMoveButton != null)
        {
            confirmMoveButton.gameObject.SetActive(hasChosenCountry);
            confirmMoveButton.interactable = hasChosenCountry && !isReady;
        }

        if (cancelMoveButton != null)
        {
            cancelMoveButton.gameObject.SetActive(hasChosenCountry && isReady);
            cancelMoveButton.interactable = hasChosenCountry && isReady;
        }
    }


    public void SelectBuildLand()
    {
        pendingBuildType = UnitType.Land;
        buildTypeSelected = true;
        RefreshBuildHighlights();
        if (buildPhaseActiveLocal) moveStatusText?.SetText("Land selected. Click a pulsing owned tile to build.");
    }

    public void SelectBuildBoat()
    {
        pendingBuildType = UnitType.Boat;
        buildTypeSelected = true;
        RefreshBuildHighlights();
        if (buildPhaseActiveLocal) moveStatusText?.SetText("Boat selected. Click a pulsing ocean tile to build.");
    }

    public void SelectBuildPlane()
    {
        pendingBuildType = UnitType.Plane;
        buildTypeSelected = true;
        RefreshBuildHighlights();

        if (buildPhaseActiveLocal)
        {
            if (PlayerMeetsPlaneBuildRequirement())
                moveStatusText?.SetText("Plane selected. Click a pulsing airfield tile to build.");
            else
                moveStatusText?.SetText("Plane selected, but you do not control enough supply centers.");
        }
    }

    public void PassBuildPhase()
    {
        if (!isLocalPlayer) return;
        if (!buildPhaseActiveLocal) return;

        buildPhaseActiveLocal = false;
        canIssueOrders = true;

        if (buildLandButton != null) buildLandButton.gameObject.SetActive(false);
        if (buildBoatButton != null) buildBoatButton.gameObject.SetActive(false);
        if (buildPlaneButton != null) buildPlaneButton.gameObject.SetActive(false);
        if (buildPassButton != null) buildPassButton.gameObject.SetActive(false);

        StopAllCoroutines();
        ClearBuildHighlights();
        moveStatusText?.SetText("Build phase passed.");
        buildTableSelectionOpen = false;

        if (cameraMovment != null)
        {
            cameraMovment.SetFocusClickEnabled(true);
            cameraMovment.SetManualInputLocked(false);
            cameraMovment.ResetCamera();
        }

        CmdPassBuildPhase(); 
    }

    [TargetRpc]
    public void TargetStartBuildPhase(NetworkConnection target, int buildCount)
    {
        StartBuildPhase(buildCount);
    }

    public void StartBuildPhase(int buildCount)
    {
        buildPhaseActiveLocal = true;
        canIssueOrders = false;
        buildTypeSelected = false;
        waitingBuildResponse = false;
        remainingBuildsLocal = buildCount;
        buildTableSelectionOpen = false;
        ClearBuildHighlights();

        if (buildLandButton != null) buildLandButton.gameObject.SetActive(false);
        if (buildBoatButton != null) buildBoatButton.gameObject.SetActive(false);
        if (buildPlaneButton != null) buildPlaneButton.gameObject.SetActive(false);
        if (buildPassButton != null) buildPassButton.gameObject.SetActive(true);

        StopAllCoroutines();

        if (remainingBuildsLocal <= 0)
        {
            moveStatusText?.SetText("No build points. Press Pass to continue.");
            return;
        }

        StartCoroutine(HandleBuildSelection());
        OpenBuildTableSelection();
    }

    private IEnumerator HandleBuildSelection()
    {
        while (buildPhaseActiveLocal && remainingBuildsLocal > 0)
        {
            if (buildTableSelectionOpen)
            {
                yield return null;
                continue;
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    yield return null;
                    continue;
                }

                if (waitingBuildResponse)
                {
                    yield return null;
                    continue;
                }

                if (!buildTypeSelected)
                {
                    moveStatusText?.SetText("Pick Land/Boat/Plane first.");
                    yield return null;
                    continue;
                }

                Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
                if (hits == null || hits.Length == 0)
                {
                    yield return null;
                    continue;
                }

                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                RaycastHit hit = hits[0];
                MainUnit clickedUnit = null;

                for (int i = 0; i < hits.Length; i++)
                {
                    MainUnit u = GetUnitFromHit(hits[i]);
                    if (u != null)
                    {
                        clickedUnit = u;
                        hit = hits[i];
                        break;
                    }
                }

                if (clickedUnit == null)
                {
                    hit = hits[0];
                }

                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                Country clicked = null;

                if (pendingBuildType == UnitType.Boat)
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        Country c = GetCountryFromHitRecursive(hits[i]);
                        if (c != null && c.isOcean)
                        {
                            clicked = c;
                            break;
                        }
                    }

                    if (clicked == null)
                    {
                        moveStatusText?.SetText("Boat: Click an ocean tile.");
                        yield return null;
                        continue;
                    }
                }
                else
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        Country c = GetCountryFromHitRecursive(hits[i]);
                        if (c != null && c.ownerID == playerID && !c.isOcean)
                        {
                            clicked = c;
                            break;
                        }
                    }

                    if (clicked == null)
                    {
                        yield return null;
                        continue;
                    }
                }

                waitingBuildResponse = true;
                CmdRequestBuildAt(clicked.gameObject.tag, playerColor, pendingBuildType);
                moveStatusText?.SetText("Requesting build...");
            }

            yield return null;
        }
    }

    [TargetRpc]
    public void TargetBuildResult(NetworkConnection target, bool success, int remainingCredits, string message)
    {

        waitingBuildResponse = false;

        if (moveStatusText != null && !string.IsNullOrEmpty(message))
            moveStatusText.SetText(message);

        if (!success)
        {
            RefreshBuildHighlights();
            return;
        }

        remainingBuildsLocal = remainingCredits;
        buildTypeSelected = false;
        ClearBuildHighlights();

        if (remainingBuildsLocal <= 0)
        {
            BuildPhaseFinishedLocal();
            CmdFinishBuildPhase();
        }
        else
        {
            OpenBuildTableSelection();
        }
    }

    private void BuildPhaseFinishedLocal()
    {
        buildPhaseActiveLocal = false;
        canIssueOrders = true;

        if (buildLandButton != null) buildLandButton.gameObject.SetActive(false);
        if (buildBoatButton != null) buildBoatButton.gameObject.SetActive(false);
        if (buildPlaneButton != null) buildPlaneButton.gameObject.SetActive(false);
        if (buildPassButton != null) buildPassButton.gameObject.SetActive(false);

        buildTableSelectionOpen = false;

        if (cameraMovment != null)
        {
            cameraMovment.SetFocusClickEnabled(true);
            cameraMovment.SetManualInputLocked(false);
            cameraMovment.ResetCamera();
        }

        moveStatusText?.SetText("Build phase complete.");
        ClearBuildHighlights();
    }


    [Command]
    private void CmdRequestBuildAt(string countryTag, Color color, UnitType unitType)
    {
        MainGameManager.Instance.ServerTrySpawnUnit(playerID, countryTag, color, unitType, connectionToClient);
    }
    [Command]
    private void CmdPassBuildPhase()
    {
        MainGameManager.Instance.ServerPassBuildPhase(playerID);
    }

    [Command]
    private void CmdFinishBuildPhase()
    {
        MainGameManager.Instance.FinishBuildPhaseForPlayer(playerID);
    }

    [Command]
    public void CmdRequestSaveLatest()
    {
        SaveSystem.SaveLatestServer();
    }
}