using Mirror;
using Mirror.BouncyCastle.Asn1.X509;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainPlayerController : NetworkBehaviour
{
    [Header("Player Info")]
    [SyncVar] public int playerID;
    [SyncVar] public Color playerColor = Color.white;
    [SyncVar] public string chosenCountry;

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

    [Header("Highlight")]
    public Color highlightColor = Color.white;
    public float highlightIntensity = 0.33f;

    private string pendingCountry;
    public bool hasChosenCountry = false;
    private List<GameObject> highlightedObjects = new List<GameObject>();
    private MainUnit selectedUnit;
    public bool canIssueOrders = true;

    [SyncVar] private bool isReady = false;
    public static Dictionary<int, bool> playersReady = new Dictionary<int, bool>();
    public static List<MainPlayerController> allPlayers = new List<MainPlayerController>();

    #region Unity Callbacks

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
        if (MainGameManager.Instance != null && MainGameManager.Instance.IsPlayerBuilding(playerID))
        {
            canIssueOrders = false;
            return;
        }

        if (!hasChosenCountry && isLocalPlayer) HandleCountrySelection();
        if (canIssueOrders && isLocalPlayer) HandleUnitSelection();
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

    #endregion

    #region Country Selection

    private Country pendingCountryComp;

    void HandleCountrySelection()
    {
        if (!isLocalPlayer || Input.GetMouseButtonDown(0) == false) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, countryLayer))
        {
            Country countryComp = GetCountryFromHitRecursive(hit);
            if (countryComp == null)
            {
                Debug.LogWarning($"Clicked object {hit.collider.name} has no Country component.");
                return;
            }

            if (!countryComp.CanBeSelected())
            {
                Debug.LogWarning($"Country {countryComp.name} cannot be selected. ownerID={countryComp.ownerID}");
                return;
            }

            pendingCountryComp = countryComp;
            pendingCountry = countryComp.tag;

            selectedCountryText?.SetText("Selected: " + countryComp.name);
            confirmButton?.gameObject.SetActive(true);
            cancelButton?.gameObject.SetActive(true);

            Debug.Log($"[Client] Pending country selection: {countryComp.name}");
        }
    }

    public void ConfirmCountryChoice()
    {
        if (pendingCountryComp == null)
        {
            Debug.LogWarning("No country is pending selection.");
            return;
        }

        if (!pendingCountryComp.CanBeSelected())
        {
            Debug.LogWarning($"Country {pendingCountryComp.name} cannot be selected at confirmation.");
            return;
        }

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
        HighlightChosenCountryObjects();

        int totalUnits = 3;
        int countryCount = allCountries.Count;
        int unitsPerCountry = Mathf.FloorToInt((float)totalUnits / countryCount);
        int remainder = totalUnits % countryCount;

        for (int i = 0; i < allCountries.Count; i++)
        {
            int unitsToSpawn = unitsPerCountry + (i < remainder ? 1 : 0);

            if (!isServer)
            {
                CmdRequestSpawnUnitsServer(allCountries[i].tag, playerID, playerColor, unitsToSpawn);
                Debug.Log($"[Client {playerID}] Requesting {unitsToSpawn} units for {allCountries[i].name}");
            }
            else
            {
                unitManager.SpawnUnitsForCountryServer(allCountries[i].tag, playerID, playerColor, unitsToSpawn);
                Debug.Log($"[Server] Spawned {unitsToSpawn} units for player {playerID} in {allCountries[i].name}");
            }
        }

        pendingCountryComp = null;
        pendingCountry = "";
    }

    [Command]
    private void CmdAssignCountryToPlayer(string countryTag, int playerID)
    {
        GameObject countryObj = GameObject.FindWithTag(countryTag);
        if (countryObj == null)
        {
            Debug.LogWarning($"[Server] Country {countryTag} not found, cannot assign.");
            return;
        }

        Country countryComp = countryObj.GetComponent<Country>();
        if (countryComp == null)
        {
            Debug.LogWarning($"[Server] {countryTag} has no Country component.");
            return;
        }

        if (countryComp.ownerID == -1)
        {
            countryComp.SetOwner(playerID);
            RpcUpdateCountryOwnership(countryTag, playerID);
            Debug.Log($"[Server] Assigned {countryTag} to player {playerID}");
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



    [Command]
    private void CmdRequestSpawnUnitsServer(string countryName, int playerID, Color color, int count)
    {
        if (MainUnitManager.Instance == null)
        {
            Debug.LogError("[CmdRequestSpawnUnitsServer] UnitManager instance not found!");
            return;
        }

        Debug.Log($"[Server] Spawning units for Player {playerID} in {countryName}");
        MainUnitManager.Instance.SpawnUnitsForCountryServer(countryName, playerID, color, count);
    }

    [Command]
    private void CmdRequestSpawnUnits(string country, int playerID, Color color, int count)
    {
        unitManager?.SpawnUnitsForCountryServer(country, playerID, color, count);
        TargetSetupLocalUnits(connectionToClient, playerID);
    }

    [TargetRpc]
    private void TargetSetupLocalUnits(NetworkConnection target, int playerID)
    {
        foreach (var unit in unitManager.GetAllUnits())
            if (unit.ownerID == playerID)
                unit.SetupLocalVisuals();
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
                Debug.LogWarning($"No color assigned for country tag {chosenCountry}");
                break;
        }

        Debug.Log($"[Player {playerID}] Assigned color {playerColor} for country tag {chosenCountry}");
    }

    void HighlightChosenCountryObjects()
    {
        //ClearHighlights();
        //GameObject[] objs = GameObject.FindGameObjectsWithTag(chosenCountry);
        //foreach (var obj in objs)
        //{
        //    if (obj == null) continue;
        //    SimpleHighlighter high = obj.GetComponent<SimpleHighlighter>() ?? obj.AddComponent<SimpleHighlighter>();
        //    high.Highlight(highlightColor, highlightIntensity);
        //    highlightedObjects.Add(obj);
        //}
    }

    public void ClearHighlights()
    {
        foreach (var obj in highlightedObjects)
            obj?.GetComponent<SimpleHighlighter>()?.Unhighlight();
        highlightedObjects.Clear();
    }
    #endregion

    #region Unit Orders

   



    void HandleUnitSelection()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        MainUnit clickedUnit = GetUnitFromHit(hit);

        if (clickedUnit != null && clickedUnit.ownerID == playerID)
        {
            selectedUnit = clickedUnit;
            ShowMoveButtons(true);
            Debug.Log($"[Client] Selected unit: {clickedUnit.name}");
            return;
        }

        if (selectedUnit != null)
        {
            bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (isShiftHeld)
            {
                MainUnit targetUnit = hit.collider.GetComponent<MainUnit>();
                string targetCountry = null;

                if (targetUnit != null)
                {
                    targetCountry = targetUnit.currentOrder != null
                        ? targetUnit.currentOrder.targetCountry
                        : targetUnit.currentCountry;

                    Debug.Log($"[Client] Support order: {selectedUnit.name} supports {targetUnit.name} -> {targetCountry}");
                    CmdSupportUnit(selectedUnit.netId, targetUnit.netId, targetCountry);
                }
                else
                {
                    Country country = GetCountryFromHit(hit);

                    if (country != null)
                    {
                        targetCountry = country.tag;

                        MainUnit movingUnit = FindUnitTargetingCountry(targetCountry, playerID);
                        if (movingUnit != null)
                        {
                            Debug.Log($"[Client] Support order: {selectedUnit.name} supports {movingUnit.name} -> {targetCountry}");
                            CmdSupportUnit(selectedUnit.netId, movingUnit.netId, targetCountry);
                        }
                        else
                        {
                            Debug.LogWarning($"[Client] No friendly unit found moving into {targetCountry} to support.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[Client] No valid unit or country to support at target click.");
                    }
                }
            }
            else
            {
                Country targetCountryComp = GetCountryFromHit(hit);
                Country fromCountryComp = FindCountryByTagRecursive(selectedUnit.currentCountry);

                if (targetCountryComp == null || fromCountryComp == null)
                {
                    Debug.LogWarning("Missing Country component, cannot move.");
                    selectedUnit = null;
                    ShowMoveButtons(false);
                    return;
                }

                if (!fromCountryComp.adjacentCountries.Contains(targetCountryComp))
                {
                    Debug.Log($"[Client] Illegal move: {fromCountryComp.name} -> {targetCountryComp.name}. Not adjacent.");
                    selectedUnit = null;
                    ShowMoveButtons(false);
                    return;
                }
                Vector3 targetPos = targetCountryComp.centerWorldPos;

                selectedUnit.ShowLocalMoveLine(targetPos);

                CmdMoveUnit(selectedUnit.netId, targetCountryComp.tag, targetPos);
                Debug.Log($"[Client] Move order: {selectedUnit.name} -> {targetCountryComp.name}");
            }

            selectedUnit = null;
            ShowMoveButtons(false);
        }
    }



    public void ConfirmMoves()
    {
        if (!isLocalPlayer) return;

        Debug.Log($"[ConfirmMoves] called | isLocalPlayer={isLocalPlayer} | netId={netId}");

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

        unit.currentOrder = new PlayerUnitOrder
        {
            orderType = UnitOrderType.Move,
            targetCountry = targetCountryTag,
            targetPosition = targetPos
        };

        Debug.Log($"[Server] CmdMoveUnit set currentOrder for {unit.name} from {unit.currentCountry} -> {targetCountryTag}");
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

        Debug.Log($"[Server] Player {playerID}: {supporter.name} SUPPORT {supported.name} -> {supportTargetCountry}");
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

    [Command]
    private void CmdConfirmMoves()
    {
        Debug.Log($"[SERVER] CmdConfirmMoves received from netId={netId}, playerID={playerID}");

        if (unitManager == null)
            unitManager = MainUnitManager.Instance;

        if (!playersReady.ContainsKey(playerID))
            playersReady[playerID] = false;

        playersReady[playerID] = true;

        Debug.Log("[SERVER] Updated playersReady:");
        foreach (var kv in playersReady)
            Debug.Log($"  Player {kv.Key} ready={kv.Value}");

        Debug.Log("[SERVER] allPlayers list:");
        foreach (var p in allPlayers)
            if (p != null) Debug.Log($"  Player netId={p.netId}, playerID={p.playerID}");

        RpcUpdateReadyUI();

        if (AreAllPlayersReady())
        {
            Debug.Log("[SERVER] All players ready — executing turn");

            unitManager.ExecuteTurnServer();
            MainGameManager.Instance?.NextTurnServer();

            foreach (var p in allPlayers)
                p.RpcResetReady();
        }
        else
        {
            Debug.Log("[SERVER] Not all players ready yet");
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
        foreach (var kv in MainPlayerController.playersReady)
        {
        }

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
    #endregion

    #region NewUnitSpawning

    [TargetRpc]
    public void TargetStartBuildPhase(NetworkConnection target, int buildCount)
    {
        Debug.Log($"[Client] TargetStartBuildPhase received. Build count: {buildCount}");
        StartBuildPhase(buildCount);
        Debug.Log($"[Client] TargetStartBuildPhase running on object with playerID={playerID}, isLocalPlayer={isLocalPlayer}, connectionToServer={connectionToServer != null}");

    }
    public void StartBuildPhase(int buildCount)
    {
        moveStatusText?.SetText($"You gained {buildCount} new supply center(s)! Click owned supply centers to build.");
        canIssueOrders = false;

        StopAllCoroutines();
        StartCoroutine(HandleBuildSelection(buildCount));
    }

    private bool isProcessingClick = false;

    private IEnumerator HandleBuildSelection(int remainingBuilds)
    {
        Debug.Log($"[Client {playerID}] Starting HandleBuildSelection with {remainingBuilds} builds. isLocalPlayer={isLocalPlayer}");

        while (remainingBuilds > 0)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Country clicked = GetCountryFromHitRecursive(hit);

                    Debug.Log("Build raycast hit");

                    if (clicked != null && clicked.isSupplyCenter)
                    {
                        Debug.Log($"[Client {playerID}] clicked {clicked.name}, sending build request (ownerID={clicked.ownerID})");
                        RequestBuild(clicked.gameObject.tag);
                        remainingBuilds--;
                    }
                    else
                    {
                        Debug.Log("Clicked object not a supply center or missing Country component.");
                    }
                }
            }
            yield return null;
        }


        BuildPhaseFinishedLocal();
        if (isLocalPlayer)
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

        if (string.IsNullOrEmpty(countryTag) || countryTag == "Untagged")
        {
            Debug.LogError("[Client] Tried to build with INVALID country tag");
            return;
        }

        CmdRequestBuildAt(countryTag, playerColor);
    }

    [Command]
    private void CmdRequestBuildAt(string countryTag, Color color)
    {
        MainGameManager.Instance.ServerTrySpawnUnit(playerID, countryTag, color, connectionToClient);
        Debug.Log($"[Server] Player {playerID} building unit at {countryTag} with color {color}");
    }

    [Command]
    private void CmdFinishBuildPhase()
    {
        MainGameManager.Instance.FinishBuildPhaseForPlayer(playerID);
    }
    #endregion
}