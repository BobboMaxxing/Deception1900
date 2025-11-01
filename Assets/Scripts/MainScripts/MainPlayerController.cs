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
    public Color playerColor = Color.white;
    public string chosenCountry;

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

    public static List<MainPlayerController> allPlayers = new List<MainPlayerController>();

    #region Unity Callbacks
    void Awake()
    {
        if (!allPlayers.Contains(this))
            allPlayers.Add(this);
    }

    void OnDestroy()
    {
        if (allPlayers.Contains(this))
            allPlayers.Remove(this);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // Assign unique player ID
        playerID = allPlayers.IndexOf(this);

        // Find camera dynamically
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera != null)
            playerCamera.enabled = true;

        // Find UnitManager dynamically
        if (unitManager == null)
            unitManager = MainUnitManager.Instance;

        // Setup UI dynamically
        LocalPlayerSetup setup = FindObjectOfType<LocalPlayerSetup>();
        if (setup != null)
            setup.Setup(this);
    }

    void Update()
    {
        if (!hasChosenCountry && isLocalPlayer)
            HandleCountrySelection();

        if (canIssueOrders && isLocalPlayer)
            HandleUnitSelection();
    }
    #endregion

    #region UI Setup
    public void SetupUIReferences(
        TMP_Text selectedCountryTextRef,
        TMP_Text moveStatusTextRef,
        Button confirmButtonRef,
        Button cancelButtonRef,
        Button confirmMoveButtonRef,
        Button cancelMoveButtonRef)
    {
        selectedCountryText = selectedCountryTextRef;
        moveStatusText = moveStatusTextRef;
        confirmButton = confirmButtonRef;
        cancelButton = cancelButtonRef;
        confirmMoveButton = confirmMoveButtonRef;
        cancelMoveButton = cancelMoveButtonRef;
    }
    #endregion

    #region Country Selection
    void HandleCountrySelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, countryLayer))
            {
                pendingCountry = hit.transform.name;
                if (selectedCountryText != null)
                    selectedCountryText.text = "Selected: " + pendingCountry;
                if (confirmButton != null)
                    confirmButton.gameObject.SetActive(true);
                if (cancelButton != null)
                    cancelButton.gameObject.SetActive(true);
            }
        }
    }

    public void ConfirmCountryChoice()
    {
        if (string.IsNullOrEmpty(pendingCountry))
            return;

        chosenCountry = pendingCountry;
        hasChosenCountry = true;

        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (selectedCountryText != null) selectedCountryText.text = "Chosen: " + chosenCountry;

        AssignPlayerColorFromCountry();
        HighlightChosenCountryObjects();

        if (unitManager != null && isLocalPlayer)
            CmdRequestSpawnUnits(chosenCountry, playerID, playerColor, 3);
    }

    public void CancelCountryChoice()
    {
        pendingCountry = "";
        if (selectedCountryText != null) selectedCountryText.text = "Selection cleared";
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (cameraMovment != null) cameraMovment.ResetCamera();
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
        foreach (GameObject obj in objs)
        {
            if (obj == null) continue;
            SimpleHighlighter high = obj.GetComponent<SimpleHighlighter>();
            if (high == null) high = obj.AddComponent<SimpleHighlighter>();
            high.Highlight(highlightColor, highlightIntensity);
            highlightedObjects.Add(obj);
        }
    }

    public void ClearHighlights()
    {
        foreach (var obj in highlightedObjects)
        {
            if (obj == null) continue;
            SimpleHighlighter high = obj.GetComponent<SimpleHighlighter>();
            if (high != null) high.Unhighlight();
        }
        highlightedObjects.Clear();
    }
    #endregion

    #region Unit Orders
    void HandleUnitSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
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
                    string countryName = hit.collider.name;
                    CmdMoveUnit(selectedUnit.netId, countryName);
                    selectedUnit = null;
                }
            }
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

            unit.RpcMoveTo(targetPos);
        }
    }

    public static MainPlayerController GetPlayerByID(int id)
    {
        return allPlayers.Find(p => p.playerID == id);
    }

    public void ConfirmMoves()
    {
        ShowMoveButtons(false);
        if (moveStatusText != null) moveStatusText.text = "Moves confirmed — Executing...";
    }

    public void CancelMoves()
    {
        ShowMoveButtons(false);
        if (moveStatusText != null) moveStatusText.text = "Moves canceled — Plan again.";

        if (isServer)
        {
            foreach (MainUnit unit in unitManager.GetComponentsInChildren<MainUnit>())
                unit.ClearOrder();
        }
    }

    void ShowMoveButtons(bool state)
    {
        if (confirmMoveButton != null) confirmMoveButton.gameObject.SetActive(state);
        if (cancelMoveButton != null) cancelMoveButton.gameObject.SetActive(state);
    }
    #endregion

    #region Network Commands
    [Command]
    private void CmdRequestSpawnUnits(string countryName, int playerID, Color color, int count)
    {
        if (unitManager != null)
            unitManager.SpawnUnitsForCountryServer(countryName, playerID, color, count);
    }
    #endregion
}
