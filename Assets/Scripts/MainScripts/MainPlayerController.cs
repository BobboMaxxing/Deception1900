using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainPlayerController : NetworkBehaviour
{
    [Header("Player Info")]
    public string playerName = "Player";
    public int playerID;
    public Color playerColor = Color.white;
    public string chosenCountry;

    [Header("Camera & Layers")]
    public Camera playerCamera;
    [SerializeField] private LayerMask countryLayer;

    [Header("UI")]
    public Button confirmButton;
    public Button cancelButton;
    public TMP_Text selectedCountryText;
    public TMP_Text moveStatusText;
    public Button confirmMoveButton;
    public Button cancelMoveButton;

    [Header("Game References")]
    public MainUnitManager unitManager;
    public CameraMovment cameraMovment;

    [Header("Highlight")]
    public Color highlightColor = Color.yellow;
    public float highlightIntensity = 1.2f;

    private string pendingCountry;
    public bool hasChosenCountry = false;
    private List<GameObject> highlightedObjects = new List<GameObject>();
    private MainUnit selectedUnit;
    public bool canIssueOrders = true;

    public static List<MainPlayerController> allPlayers = new List<MainPlayerController>();

    #region Unity Callbacks
    void Awake()
    {
        if (!allPlayers.Contains(this)) allPlayers.Add(this);

        if (unitManager == null) unitManager = MainUnitManager.Instance;
        if (cameraMovment == null) cameraMovment = FindObjectOfType<CameraMovment>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(false);
            confirmButton.onClick.AddListener(ConfirmCountryChoice);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(false);
            cancelButton.onClick.AddListener(CancelCountryChoice);
        }

        if (confirmMoveButton != null)
        {
            confirmMoveButton.gameObject.SetActive(false);
            confirmMoveButton.onClick.AddListener(ConfirmMoves);
        }

        if (cancelMoveButton != null)
        {
            cancelMoveButton.gameObject.SetActive(false);
            cancelMoveButton.onClick.AddListener(CancelMoves);
        }
    }

    void OnDestroy()
    {
        if (allPlayers.Contains(this)) allPlayers.Remove(this);
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (!hasChosenCountry) HandleCountrySelection();
        if (canIssueOrders) HandleUnitSelection();
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
                if (selectedCountryText != null) selectedCountryText.text = "Selected: " + pendingCountry;
                if (confirmButton != null) confirmButton.gameObject.SetActive(true);
                if (cancelButton != null) cancelButton.gameObject.SetActive(true);
            }
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

        MainUnit unit = identity.gameObject.GetComponent<MainUnit>();
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

    void ConfirmMoves()
    {
        ShowMoveButtons(false);
        if (moveStatusText != null) moveStatusText.text = "Moves confirmed — Executing...";
    }

    void CancelMoves()
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
