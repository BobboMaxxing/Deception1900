using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainPlayerController : NetworkBehaviour
{
    [Header("Player Info")]
    public string playerName = "Player";
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

    void Awake()
    {
        if (!allPlayers.Contains(this)) allPlayers.Add(this);
    }

    void OnDestroy()
    {
        if (allPlayers.Contains(this)) allPlayers.Remove(this);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        playerID = allPlayers.IndexOf(this);

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera != null) playerCamera.enabled = true;

        if (unitManager == null) unitManager = MainUnitManager.Instance;

        LocalPlayerSetup setup = FindObjectOfType<LocalPlayerSetup>();
        if (setup != null) setup.Setup(this);

        UpdateReadyUI();
    }

    void Update()
    {
        if (!hasChosenCountry && isLocalPlayer)
            HandleCountrySelection();

        if (canIssueOrders && isLocalPlayer)
            HandleUnitSelection();
    }

    #region Country Selection
    void HandleCountrySelection()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, countryLayer))
        {
            pendingCountry = hit.transform.name;
            if (selectedCountryText != null)
                selectedCountryText.text = "Selected: " + pendingCountry;
            if (confirmButton != null) confirmButton.gameObject.SetActive(true);
            if (cancelButton != null) cancelButton.gameObject.SetActive(true);
        }
    }

    public void ConfirmCountryChoice()
    {
        if (string.IsNullOrEmpty(pendingCountry)) return;

        chosenCountry = pendingCountry;
        hasChosenCountry = true;

        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (selectedCountryText != null) selectedCountryText.text = "Chosen: " + chosenCountry;

        AssignPlayerColorFromCountry();
        HighlightChosenCountryObjects();

        if (isServer)
        {
            unitManager.SpawnUnitsForCountryServer(chosenCountry, playerID, playerColor, 3);
        }
        else
        {
            unitManager.SpawnUnitsForCountryLocal(chosenCountry, playerID, playerColor, 3);
            CmdRequestSpawnUnits(chosenCountry, playerID, playerColor, 3);
        }
    }

    public void CancelCountryChoice()
    {
        pendingCountry = "";
        if (selectedCountryText != null) selectedCountryText.text = "Selection cleared";
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
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
        {
            if (obj == null) continue;
            obj.GetComponent<SimpleHighlighter>()?.Unhighlight();
        }
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
                Vector3 targetPos = GameObject.Find(hit.collider.name).transform.position;

                if (selectedUnit.isServer)
                {
                    CmdMoveUnit(selectedUnit.netId, hit.collider.name);
                }
                else
                {
                    selectedUnit.currentOrder = new PlayerUnitOrder
                    {
                        orderType = UnitOrderType.Move,
                        targetCountry = hit.collider.name,
                        targetPosition = targetPos
                    };
                    selectedUnit.ShowLocalMoveLine(targetPos);
                }

                selectedUnit = null;
            }
        }
    }

    public void ConfirmMoves()
    {
        if (!isLocalPlayer) return;

        CmdSetReady(true);
        ShowMoveButtons(false);
        if (moveStatusText != null) moveStatusText.text = "Waiting for other players...";
    }

    public void CancelMoves()
    {
        if (!isLocalPlayer) return;

        ShowMoveButtons(false);
        if (moveStatusText != null) moveStatusText.text = "Moves canceled — Plan again.";

        if (isServer)
        {
            foreach (var unit in unitManager.GetAllUnits())
                unit.ClearOrder();
            CmdSetReady(false);
        }
    }

    [Command]
    private void CmdMoveUnit(uint unitNetId, string targetCountry)
    {
        NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity identity);
        if (identity == null) return;

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

            TargetShowMoveLine(connectionToClient, unit.netId, targetPos);
        }
    }

    [TargetRpc]
    private void TargetShowMoveLine(NetworkConnection target, uint unitNetId, Vector3 targetPos)
    {
        if (NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity identity))
        {
            MainUnit unit = identity.GetComponent<MainUnit>();
            if (unit != null && unit.ownerID == playerID)
                unit.ShowLocalMoveLine(targetPos);
        }
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

    [Command]
    private void CmdSetReady(bool readyState)
    {
        isReady = readyState;

        if (!playersReady.ContainsKey(playerID))
            playersReady.Add(playerID, readyState);
        else
            playersReady[playerID] = readyState;

        RpcUpdateReadyUI();

        bool allReady = true;
        foreach (var player in allPlayers)
        {
            if (!player.hasChosenCountry || !playersReady.ContainsKey(player.playerID) || !playersReady[player.playerID])
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            unitManager.ExecuteTurnServer();
            MainGameManager.Instance?.NextTurnServer();

            foreach (var p in allPlayers)
                p.RpcResetReady();
        }
    }

    [ClientRpc]
    public void RpcUpdateReadyUI() => UpdateReadyUI();

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
        if (playersReady.ContainsKey(playerID)) playersReady[playerID] = false;
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

    public static MainPlayerController GetPlayerByID(int id)
    {
        return allPlayers.Find(p => p.playerID == id);
    }
    #endregion
}

