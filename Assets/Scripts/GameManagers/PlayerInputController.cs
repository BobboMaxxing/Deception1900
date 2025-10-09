using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PlayerInputController : MonoBehaviour
{
    public Camera playerCamera;
    public UnitManager unitManager;

    [Header("UI")]
    public Button confirmMoveButton;
    public Button cancelMoveButton;
    public TMP_Text moveStatusText;

    private Unit selectedUnit;
    private bool hasConfirmed = false;

    void Start()
    {
        if (confirmMoveButton != null)
        {
            confirmMoveButton.onClick.AddListener(ConfirmMoves);
            confirmMoveButton.gameObject.SetActive(false);
        }

        if (cancelMoveButton != null)
        {
            cancelMoveButton.onClick.AddListener(CancelMoves);
            cancelMoveButton.gameObject.SetActive(false);
        }

        if (moveStatusText != null)
        {
            moveStatusText.text = "Plan your moves";
        }
    }

    void Update()
    {
        if (hasConfirmed) return;

        HandleUnitSelection();
        HandleOrderPreview();
    }

    void HandleUnitSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Unit clickedUnit = hit.collider.GetComponent<Unit>();

                if (clickedUnit != null)
                {
                    selectedUnit = clickedUnit;
                    Debug.Log("Selected unit: " + selectedUnit.name);
                    ShowMoveButtons(true);
                }
                else if (selectedUnit != null)
                {
                    string countryName = hit.collider.name;

                    // Issue move order
                    UnitOrder order = new UnitOrder(OrderType.Move, countryName);
                    unitManager.IssueOrder(selectedUnit, order);

                    selectedUnit = null;
                }
            }
        }
    }

    void HandleOrderPreview()
    {
        if (selectedUnit != null)
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 mousePos = ray.GetPoint(enter);

                // Draw black line as preview
                UnitOrder previewOrder = new UnitOrder(OrderType.Move, "Preview");
                selectedUnit.SetOrder(previewOrder, mousePos);
                SetLineColor(selectedUnit, Color.black);
            }
        }
    }

    private void SetLineColor(Unit unit, Color color)
    {
        LineRenderer lr = unit.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.startColor = color;
            lr.endColor = color;
        }
    }

    void ConfirmMoves()
    {
        hasConfirmed = true;
        ShowMoveButtons(false);

        if (moveStatusText != null)
            moveStatusText.text = "Moves confirmed — Executing...";

        unitManager.ExecuteTurn();
    }

    void CancelMoves()
    {
        hasConfirmed = false;
        ShowMoveButtons(false);

        if (moveStatusText != null)
            moveStatusText.text = "Moves canceled — Plan again.";

        unitManager.ClearAllOrders();
    }

    void ShowMoveButtons(bool state)
    {
        if (confirmMoveButton != null) confirmMoveButton.gameObject.SetActive(state);
        if (cancelMoveButton != null) cancelMoveButton.gameObject.SetActive(state);
    }
}