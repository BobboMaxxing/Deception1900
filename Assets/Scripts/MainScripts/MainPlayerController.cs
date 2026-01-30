using System;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainPlayerController : NetworkBehaviour
{
    [Header("Player Info")]
    [SyncVar] public int playerID;
    [SyncVar] public Color playerColor = Color.white;
    [SyncVar] public string chosenCountryRegionId;

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

    [Header("Game References")]
    public MainUnitManager unitManager;
    public CameraMovment cameraMovment;

    private string pendingCountryRegionId;
    private Country pendingCountryComp;

    public bool hasChosenCountry = false;
    private MainUnit selectedUnit;
    public bool canIssueOrders = true;

    [SyncVar] private bool isReady = false;
    public static Dictionary<int, bool> playersReady = new Dictionary<int, bool>();
    public static List<MainPlayerController> allPlayers = new List<MainPlayerController>();

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

        LocalPlayerSetup setup = UnityEngine.Object.FindFirstObjectByType<LocalPlayerSetup>();
        setup?.Setup(this);

        UpdateReadyUI();
    }

    void Update()
    {
        if (MainGameManager.Instance != null && MainGameManager.Instance.IsPlayerBuilding(playerID))
        {
            canIssueOrders = false;
            return;
        }

        if (!hasChosenCountry && isLocalPlayer) HandleCountrySelection();
        if (canIssueOrders && isLocalPlayer) HandleUnitSelection();
        if (isLocalPlayer) HandleCountryHover();
    }


    private Country GetCountryFromHitRecursive(RaycastHit hit)
    {
        if (hit.collider == null) return null;

        var t = hit.collider.transform;

        while (t != null)
        {
            Country c = t.GetComponent<Country>();
            if (c != null) return c;

            c = t.GetComponentInChildren<Country>(true);
            if (c != null) return c;

            t = t.parent;
        }

        return null;
    }

    private MainUnit GetUnitFromHit(RaycastHit hit)
    {
        return hit.collider.GetComponentInParent<MainUnit>()
            ?? hit.collider.GetComponentInChildren<MainUnit>();
    }

    private void DebugClick(string label)
    {
        if (!isLocalPlayer) return;

        Debug.Log($"[{label}] Click: frame={Time.frameCount} mouse={Input.mousePosition} overUI={(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())}");

        if (playerCamera == null)
        {
            Debug.Log($"[{label}] playerCamera is NULL");
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        Debug.Log($"[{label}] RaycastAll hits = {hits.Length}");

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            var unitBlock = h.collider.GetComponentInParent<MainUnit>() != null;
            var countryFound = GetCountryFromHitRecursive(h);

            Debug.Log($"[{label}] hit[{i}] name={h.collider.name} layer={LayerMask.LayerToName(h.collider.gameObject.layer)} dist={h.distance:0.00} unitBlock={unitBlock} country={(countryFound ? countryFound.name : "null")} regionId={(countryFound ? countryFound.regionId : "null")} canSelect={(countryFound ? countryFound.CanBeSelected().ToString() : "n/a")}");
        }
    }

    private bool TryPickCountryUnderMouse(out Country country)
    {
        country = null;
        if (playerCamera == null) return false;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            Mathf.Infinity,
            ~0,
            QueryTriggerInteraction.Collide
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            if (h.collider.GetComponentInParent<MainUnit>() != null)
                continue;

            Country c = GetCountryFromHitRecursive(h);
            if (c == null) continue;

            country = c;
            return true;
        }

        return false;
    }

    void HandleCountrySelection()
    {
        if (!isLocalPlayer || !Input.GetMouseButtonDown(0)) return;

        DebugClick("CountrySelect");

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (!TryPickCountryUnderMouse(out Country countryComp)) return;
        if (!countryComp.CanBeSelected()) return;

        pendingCountryComp = countryComp;
        pendingCountryRegionId = countryComp.regionId;

        selectedCountryText?.SetText("Selected: " + countryComp.name);
        confirmButton?.gameObject.SetActive(true);
        cancelButton?.gameObject.SetActive(true);
    }

    public void ConfirmCountryChoice()
    {
        if (pendingCountryComp == null) return;
        if (!pendingCountryComp.CanBeSelected()) return;

        List<Country> allCountries = pendingCountryComp.GetAllSelectableCountries();

        foreach (var c in allCountries)
        {
            c.SetOwner(playerID);

            if (!isServer)
                CmdAssignCountryToPlayer(c.regionId, playerID);
        }

        chosenCountryRegionId = pendingCountryComp.regionId;
        hasChosenCountry = true;

        confirmButton?.gameObject.SetActive(false);
        cancelButton?.gameObject.SetActive(false);
        selectedCountryText?.SetText("Chosen: " + pendingCountryComp.name);

        AssignPlayerColorFromCountryRegionId();

        int totalUnits = 3;
        int countryCount = allCountries.Count;
        int unitsPerCountry = Mathf.FloorToInt((float)totalUnits / countryCount);
        int remainder = totalUnits % countryCount;

        for (int i = 0; i < allCountries.Count; i++)
        {
            int unitsToSpawn = unitsPerCountry + (i < remainder ? 1 : 0);

            if (!isServer)
                CmdRequestSpawnUnitsServer(allCountries[i].regionId, playerID, playerColor, unitsToSpawn);
            else
                unitManager.SpawnUnitsForRegionServer(allCountries[i].regionId, playerID, playerColor, unitsToSpawn);
        }

        pendingCountryComp = null;
        pendingCountryRegionId = "";
    }

    [Command]
    private void CmdAssignCountryToPlayer(string countryRegionId, int playerID)
    {
        if (RegionDirectory.Instance == null) return;

        Country countryComp = RegionDirectory.Instance.GetCountryOrNull(countryRegionId);
        if (countryComp == null) return;

        if (countryComp.ownerID == -1)
        {
            countryComp.SetOwner(playerID);
            RpcSetCountryOwner(countryRegionId, playerID);
        }
    }

    [ClientRpc]
    private void RpcSetCountryOwner(string countryIdOrTag, int newOwnerId)
    {
        Country c = CountryLookup.FindCountry(countryIdOrTag);
        if (c == null) return;

        c.SetOwner(newOwnerId);

        var renderers = c.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        foreach (var r in renderers)
            r.material.color = playerColor;
    }

    [Command]
    private void CmdRequestSpawnUnitsServer(string regionId, int playerID, Color color, int count)
    {
        if (MainUnitManager.Instance == null) return;
        MainUnitManager.Instance.SpawnUnitsForRegionServer(regionId, playerID, color, count);
    }

    public void CancelCountryChoice()
    {
        pendingCountryRegionId = "";
        selectedCountryText?.SetText("Selection cleared");
        confirmButton?.gameObject.SetActive(false);
        cancelButton?.gameObject.SetActive(false);
        cameraMovment?.ResetCamera();
    }

    void AssignPlayerColorFromCountryRegionId()
    {
        switch (chosenCountryRegionId)
        {
            case "Germany": playerColor = Color.black; break;
            case "France": playerColor = Color.blue; break;
            case "Italy": playerColor = Color.green; break;
            case "Russia": playerColor = Color.cyan; break;
            case "Uk": playerColor = Color.yellow; break;
            case "Yugoslavia": playerColor = Color.magenta; break;
            case "Turkaye": playerColor = Color.red; break;
            default: playerColor = Color.gray; break;
        }
    }

    void HandleCountryHover()
    {
        if (!isLocalPlayer) return;

        if (TryPickCountryUnderMouse(out Country country))
        {
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

        if (selectedUnit != null)
        {
            if (!TryPickCountryUnderMouse(out Country targetCountryComp)) return;
            if (targetCountryComp == null) return;

            bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (isShiftHeld)
            {
                string targetRegionId = targetCountryComp.regionId;

                MainUnit movingUnit = FindUnitTargetingRegion(targetRegionId, playerID);
                if (movingUnit != null)
                {
                    CmdSupportUnit(selectedUnit.netId, movingUnit.netId, targetRegionId);
                    selectedUnit = null;
                    ShowMoveButtons(false);
                }

                return;
            }

            if (RegionDirectory.Instance == null) return;

            Country fromCountryComp = RegionDirectory.Instance.GetCountryOrNull(selectedUnit.currentRegionId);
            if (fromCountryComp == null)
            {
                Debug.Log($"[UnitMove] fromCountryComp null. selectedUnit.currentRegionId='{selectedUnit.currentRegionId}'");
                return;
            }

            if (!fromCountryComp.adjacentCountries.Contains(targetCountryComp)) return;

            Vector3 targetPos = targetCountryComp.centerWorldPos;
            selectedUnit.ShowLocalMoveLine(targetPos);
            CmdMoveUnit(selectedUnit.netId, targetCountryComp.regionId, targetPos);

            selectedUnit = null;
            ShowMoveButtons(false);
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitAny))
        {
            MainUnit clickedUnit = GetUnitFromHit(hitAny);
            if (clickedUnit != null && clickedUnit.ownerID == playerID)
            {
                selectedUnit = clickedUnit;
                ShowMoveButtons(true);
            }
        }
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
    private void CmdMoveUnit(uint unitNetId, string targetRegionId, Vector3 targetPos)
    {
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity identity)) return;
        MainUnit unit = identity.GetComponent<MainUnit>();
        if (unit == null || unit.ownerID != playerID) return;

        unit.currentOrder = new PlayerUnitOrder
        {
            orderType = UnitOrderType.Move,
            targetRegionId = targetRegionId,
            targetPosition = targetPos
        };
    }

    [Command]
    private void CmdSupportUnit(uint supporterNetId, uint supportedNetId, string supportTargetRegionId)
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
            targetRegionId = supportTargetRegionId
        };
    }

    private MainUnit FindUnitTargetingRegion(string targetRegionId, int ownerId)
    {
        foreach (MainUnit unit in MainUnitManager.Instance.GetAllUnits())
        {
            if (unit.ownerID == ownerId &&
                unit.currentOrder != null &&
                unit.currentOrder.orderType == UnitOrderType.Move &&
                unit.currentOrder.targetRegionId == targetRegionId)
                return unit;
        }
        return null;
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
            if (!kv.Value) return false;
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

    #region Build Phase (Client + Server)

    [TargetRpc]
    public void TargetStartBuildPhase(NetworkConnection target, int buildCount)
    {
        StartBuildPhase(buildCount);
    }

    private Coroutine buildRoutine;

    public void StartBuildPhase(int buildCount)
    {
        if (!isLocalPlayer) return;

        canIssueOrders = false;
        moveStatusText?.SetText($"Build phase: You have {buildCount} build(s). Click owned supply centers to build.");

        if (buildRoutine != null) StopCoroutine(buildRoutine);
        buildRoutine = StartCoroutine(HandleBuildSelection(buildCount));
    }

    private IEnumerator HandleBuildSelection(int remainingBuilds)
    {
        while (remainingBuilds > 0)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    yield return null;
                    continue;
                }

                Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Country clicked = CountryLookup.FromHit(hit);
                    if (clicked != null && clicked.isSupplyCenter)
                    {
                        if (clicked.ownerID == playerID)
                        {
                            RequestBuild(clicked.tag);
                            remainingBuilds--;
                            moveStatusText?.SetText($"Build phase: Remaining builds = {remainingBuilds}");
                        }
                    }
                }
            }

            yield return null;
        }

        BuildPhaseFinishedLocal();
        CmdFinishBuildPhase();
    }

    private void BuildPhaseFinishedLocal()
    {
        canIssueOrders = true;
        moveStatusText?.SetText("Build phase complete.");
    }

    private void RequestBuild(string countryTag)
    {
        if (!isLocalPlayer) return;
        if (string.IsNullOrEmpty(countryTag) || countryTag == "Untagged") return;

        CmdRequestBuildAt(countryTag, playerColor);
    }

    [Command]
    private void CmdRequestBuildAt(string countryTag, Color color)
    {
        if (MainGameManager.Instance == null) return;
        MainGameManager.Instance.ServerTrySpawnUnit(playerID, countryTag, color, connectionToClient);
    }

    [Command]
    private void CmdFinishBuildPhase()
    {
        if (MainGameManager.Instance == null) return;
        MainGameManager.Instance.FinishBuildPhaseForPlayer(playerID);
    }

    #endregion
}
