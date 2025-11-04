using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    void Awake()
    {
        if (!allPlayers.Contains(this)) allPlayers.Add(this);
    }



    void OnDestroy()
    {
        if (allPlayers.Contains(this)) allPlayers.Remove(this);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!playersReady.ContainsKey(playerID)) playersReady[playerID] = false;
        if (unitManager == null)
            unitManager = MainUnitManager.Instance;

        if (!playersReady.ContainsKey(playerID))
            playersReady[playerID] = false;
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
        if (!hasChosenCountry && isLocalPlayer) HandleCountrySelection();
        if (canIssueOrders && isLocalPlayer) HandleUnitSelection();
    }
    #endregion

    #region Country Selection
    void HandleCountrySelection()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, countryLayer))
        {
            pendingCountry = hit.transform.name;
            selectedCountryText?.SetText("Selected: " + pendingCountry);
            confirmButton?.gameObject.SetActive(true);
            cancelButton?.gameObject.SetActive(true);
        }
    }

    public void ConfirmCountryChoice()
    {
        if (string.IsNullOrEmpty(pendingCountry)) return;

        chosenCountry = pendingCountry;
        hasChosenCountry = true;

        CmdSetChosenCountry(chosenCountry);

        confirmButton?.gameObject.SetActive(false);
        cancelButton?.gameObject.SetActive(false);
        selectedCountryText?.SetText("Chosen: " + chosenCountry);

        AssignPlayerColorFromCountry();
        HighlightChosenCountryObjects();

        if (!isServer)
        {
            Debug.Log($"[Client {playerID}] Requesting server to spawn units for {chosenCountry}");
            CmdRequestSpawnUnitsServer(chosenCountry, playerID, playerColor, 3);
        }

        else
        {
            unitManager.SpawnUnitsForCountryServer(chosenCountry, playerID, playerColor, 3);
            Debug.Log("cant spawn units for" + playerID);
        }
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
    private void CmdSetChosenCountry(string country)
    {
        chosenCountry = country;
        hasChosenCountry = true;
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

    private IEnumerator SendSpawnCommandDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        if (isLocalPlayer)
            CmdRequestSpawnUnits(chosenCountry, playerID, playerColor, 3);
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
        GameObject countryObj = GameObject.Find(chosenCountry);
        if (countryObj != null)
        {
            Renderer rend = countryObj.GetComponent<Renderer>();
            if (rend != null) playerColor = rend.material.color;
        }
    }

    void HighlightChosenCountryObjects()
    {
        ClearHighlights();
        GameObject[] objs = GameObject.FindGameObjectsWithTag(chosenCountry);
        foreach (var obj in objs)
        {
            if (obj == null) continue;
            SimpleHighlighter high = obj.GetComponent<SimpleHighlighter>() ?? obj.AddComponent<SimpleHighlighter>();
            high.Highlight(highlightColor, highlightIntensity);
            highlightedObjects.Add(obj);
        }
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
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            MainUnit clickedUnit = hit.collider.GetComponent<MainUnit>();
            if (clickedUnit != null && clickedUnit.ownerID == playerID)
            {
                selectedUnit = clickedUnit;
                ShowMoveButtons(true);
            }
            else if (selectedUnit != null)
            {
                Vector3 targetPos = hit.point;
                selectedUnit.ShowLocalMoveLine(targetPos);

                CmdMoveUnit(selectedUnit.netId, hit.collider.name);
                selectedUnit = null;
            }
        }
    }

    public void ConfirmMoves()
    {
        if (!isLocalPlayer) return;
        ShowMoveButtons(false);
        moveStatusText?.SetText("Waiting for other players...");
        CmdSetReady(true);
    }

    public void CancelMoves()
    {
        if (!isLocalPlayer) return;
        ShowMoveButtons(false);
        moveStatusText?.SetText("Moves canceled — Plan again.");
        CmdSetReady(false);
    }

    [Command]
    private void CmdMoveUnit(uint unitNetId, string targetCountry)
    {
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity identity)) return;
        MainUnit unit = identity.GetComponent<MainUnit>();
        if (unit != null && unit.ownerID == playerID)
        {
            Vector3 targetPos = GameObject.Find(targetCountry).transform.position;
            unit.currentOrder = new PlayerUnitOrder
            {
                orderType = UnitOrderType.Move,
                targetCountry = targetCountry,
                targetPosition = targetPos
            };
        }
    }

    [Command]
    private void CmdSetReady(bool readyState)
    {
        SetReadyServer(readyState);
    }

    [Server]
    private void SetReadyServer(bool readyState)
    {
        if (unitManager == null)
        {
            Debug.LogError("[Server] UnitManager not found!");
            return;
        }

        isReady = readyState;
        playersReady[playerID] = readyState;

        RpcUpdateReadyUI();

        bool allReady = true;
        foreach (var player in allPlayers)
        {
            if (player == null)
            {
                Debug.LogWarning("[Server] Found null player reference in allPlayers!");
                allReady = false;
                continue;
            }

            if (!player.hasChosenCountry)
            {
                Debug.Log($"[Server] Player {player.playerID} has not chosen a country yet.");
                allReady = false;
            }

            if (!playersReady.TryGetValue(player.playerID, out bool ready) || !ready)
            {
                Debug.Log($"[Server] Player {player.playerID} is not ready.");
                allReady = false;
            }
        }

        if (!allReady) return;

        Debug.Log("[Server] All players ready — executing turn...");
        StartCoroutine(DelayedTurnExecution());
    }

    private IEnumerator DelayedTurnExecution()
    {
        yield return new WaitForSeconds(0.5f);

        if (unitManager == null)
        {
            Debug.LogError("[Server] UnitManager is missing before executing turn!");
            yield break;
        }

        try
        {
            unitManager.ExecuteTurnServer();
            MainGameManager.Instance?.NextTurnServer();

            foreach (var p in allPlayers)
            {
                if (p != null && p.connectionToClient != null)
                    p.RpcResetReady();
            }

            Debug.Log("[Server] Turn executed and players reset.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Server] Error during turn execution: " + ex);
        }
    }

    [ClientRpc] public void RpcUpdateReadyUI() => UpdateReadyUI();
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
}
