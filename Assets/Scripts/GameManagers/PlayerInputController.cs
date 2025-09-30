using UnityEngine;

public class PlayerInputController : MonoBehaviour
{
    public Camera playerCamera;
    public UnitManager unitManager;
    private Unit selectedUnit;

    void Update()
    {
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
                    // Select or re-select unit
                    selectedUnit = clickedUnit;
                    Debug.Log("Selected unit: " + selectedUnit.name);
                }
                else if (selectedUnit != null)
                {
                    // Clicked on a country
                    string countryName = hit.collider.name;

                    // Issue new move order, overwriting previous
                    UnitOrder order = new UnitOrder(OrderType.Move, countryName);
                    unitManager.IssueOrder(selectedUnit, order);

                    // Deselect after issuing
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

                // Show preview line in black
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
}
