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

    void OnDestroy()
    {
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
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (MainGameManager.Instance != null && MainGameManager.Instance.IsPlayerBuilding(playerID))
        {
            canIssueOrders = false;
            HandleCountryHover();
            if (!hasChosenCountry) HandleCountrySelection();
            return;
        }

        canIssueOrders = true;

        HandleCountryHover();

        if (!hasChosenCountry)
        {
            HandleCountrySelection();
            return;
        }

        if (canIssueOrders)
            HandleUnitSelection();
    }

    private Country GetCountryFromHit(RaycastHit hit)
    {
        return hit.collider.GetComponentInParent<Country>()
            ?? hit.collider.GetComponentInChildren<Country>();
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

        switch (chosenCountry)
        {
            case "Germany":
                playerColor = Color.black;
                break;
            case "France":
                playerColor = Color.blue;
                break;
            case "Italy":
                playerColor = Color.green;
                break;
            case "Russia":
                playerColor = Color.cyan;
                break;
            case "Uk":
                playerColor = Color.yellow;
                break;
            case "Yugoslavia":
                playerColor = Color.magenta;
                break;
            case "Turkaye":
                playerColor = Color.red;
                break;
            default:
                playerColor = Color.gray;
                break;
        }
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


    void HandleUnitSelection()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, 9999f);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        MainUnit hitUnit = null;
        for (int i = 0; i < hits.Length; i++)
        {
            hitUnit = GetUnitFromHit(hits[i]);
            if (hitUnit != null) break;
        }

        if (hitUnit != null && hitUnit.ownerID == playerID)
        {
            selectedUnit = hitUnit;
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
            selectedUnit = null;
            ShowMoveButtons(false);
            return;
        }

        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (isShiftHeld)
        {
            MainUnit targetUnit = null;
            for (int i = 0; i < hits.Length; i++)
            {
                targetUnit = GetUnitFromHit(hits[i]);
                if (targetUnit != null) break;
            }

            string targetCountry = null;

            if (targetUnit != null)
            {
                targetCountry = targetUnit.currentOrder != null
                    ? targetUnit.currentOrder.targetCountry
                    : targetUnit.currentCountry;

                CmdSupportUnit(selectedUnit.netId, targetUnit.netId, targetCountry);
            }
            else
            {
                targetCountry = hitTile.tag;

                MainUnit movingUnit = FindUnitTargetingCountry(targetCountry, playerID);
                if (movingUnit != null)
                {
                    CmdSupportUnit(selectedUnit.netId, movingUnit.netId, targetCountry);
                }
            }

            selectedUnit = null;
            ShowMoveButtons(false);
            return;
        }

        Country targetCountryComp = hitTile;
        Country fromCountryComp = FindCountryByTagRecursive(selectedUnit.currentCountry);

        if (selectedUnit.unitType == UnitType.Boat && !targetCountryComp.isOcean)
        {
            moveStatusText?.SetText("Boats can only move on oceans.");
            selectedUnit = null;
            ShowMoveButtons(false);
            return;
        }

        if (targetCountryComp == null || fromCountryComp == null)
        {
            selectedUnit = null;
            ShowMoveButtons(false);
            return;
        }

        bool direct = fromCountryComp.adjacentCountries.Contains(targetCountryComp);
        bool bridge = CanLandBridgeMove(selectedUnit, fromCountryComp, targetCountryComp);

        if (!direct && !bridge)
        {
            moveStatusText?.SetText("Illegal move.");
            selectedUnit = null;
            ShowMoveButtons(false);
            return;
        }

        Vector3 targetPos = targetCountryComp.centerWorldPos;
        selectedUnit.ShowLocalMoveLine(targetPos);

        CmdMoveUnit(selectedUnit.netId, targetCountryComp.tag, targetPos);

        selectedUnit = null;
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

    private HashSet<string> GetAdjacentOceanTags(Country tile)
    {
        HashSet<string> result = new HashSet<string>();
        if (tile == null || tile.adjacentCountries == null) return result;

        foreach (var adj in tile.adjacentCountries)
        {
            if (adj == null) continue;
            if (!adj.isOcean) continue;
            result.Add(adj.gameObject.tag);
        }

        return result;
    }


    public void ConfirmMoves()
    {
        if (!isLocalPlayer) return;

        ShowMoveButtons(false);
        moveStatusText?.SetText("Waiting for other players...");

        CmdConfirmMoves();
    }

    public void CancelMoves()
    {
        if (!isLocalPlayer) return;

        ShowMoveButtons(false);
        moveStatusText?.SetText("Moves canceled — plan again.");

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
    }

    [Command]
    private void CmdSupportUnit(uint supporterNetId, uint supportedNetId, string supportTargetCountry)
    {
        if (!NetworkServer.spawned.TryGetValue(supporterNetId, out NetworkIdentity supId)) return;
        if (!NetworkServer.spawned.TryGetValue(supportedNetId, out NetworkIdentity targetId)) return;

        MainUnit supporter = supId.GetComponent<MainUnit>();
        MainUnit supported = targetId.GetComponent<MainUnit>();
        if (supporter == null || supported == null) return;
        if (supporter.ownerID != playerID) return;

        supporter.currentOrder = new PlayerUnitOrder
        {
            orderType = UnitOrderType.Support,
            supportedUnit = supported,
            targetCountry = supportTargetCountry
        };
    }

    private MainUnit FindUnitTargetingCountry(string targetCountry, int ownerId)
    {
        foreach (MainUnit unit in MainUnitManager.Instance.GetAllUnits())
        {
            if (unit.ownerID == ownerId &&
                unit.currentOrder != null &&
                unit.currentOrder.orderType == UnitOrderType.Move &&
                unit.currentOrder.targetCountry == targetCountry)
            {
                return unit;
            }
        }
        return null;
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
        if (playersReady.ContainsKey(playerID))
            playersReady[playerID] = false;
        UpdateReadyUI();
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

        if (buildLandButton != null) buildLandButton.gameObject.SetActive(false);
        if (buildBoatButton != null) buildBoatButton.gameObject.SetActive(false);
        if (buildPlaneButton != null) buildPlaneButton.gameObject.SetActive(false);

        if (buildPassButton != null)
        {
            buildPassButton.gameObject.SetActive(false);
            buildPassButton.onClick.RemoveAllListeners();
            buildPassButton.onClick.AddListener(PassBuildPhase);
        }
    }

    public void SelectBuildLand()
    {
        pendingBuildType = UnitType.Land;
        buildTypeSelected = true;
        if (buildPhaseActiveLocal) moveStatusText?.SetText("Land selected. Click an owned tile to build.");
    }

    public void SelectBuildBoat()
    {
        pendingBuildType = UnitType.Boat;
        buildTypeSelected = true;
        if (buildPhaseActiveLocal) moveStatusText?.SetText("Boat selected. Click an ocean tile to build.");
    }

    public void SelectBuildPlane()
    {
        pendingBuildType = UnitType.Plane;
        buildTypeSelected = true;
        if (buildPhaseActiveLocal) moveStatusText?.SetText("Plane selected. Click an owned tile to build.");
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
        moveStatusText?.SetText("Build phase passed.");

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

        if (buildLandButton != null) buildLandButton.gameObject.SetActive(true);
        if (buildBoatButton != null) buildBoatButton.gameObject.SetActive(true);
        if (buildPlaneButton != null) buildPlaneButton.gameObject.SetActive(true);
        if (buildPassButton != null) buildPassButton.gameObject.SetActive(true);

        if (remainingBuildsLocal <= 0)
        {
            moveStatusText?.SetText("No build points. Press Pass to continue.");
            StopAllCoroutines();
            return;
        }

        moveStatusText?.SetText($"Build points: {remainingBuildsLocal}. Pick Land/Boat/Plane, then click an owned tile.");

        StopAllCoroutines();
        StartCoroutine(HandleBuildSelection());
    }

    private IEnumerator HandleBuildSelection()
    {
        while (buildPhaseActiveLocal && remainingBuildsLocal > 0)
        {
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
            return;

        remainingBuildsLocal = remainingCredits;
        buildTypeSelected = false;

        if (remainingBuildsLocal <= 0)
        {
            BuildPhaseFinishedLocal();
            CmdFinishBuildPhase();
        }
        else
        {
            if (moveStatusText != null)
                moveStatusText.SetText($"Build points left: {remainingBuildsLocal}. Pick unit type again.");
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

        moveStatusText?.SetText("Build phase complete.");
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
}