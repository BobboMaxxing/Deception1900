using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Unit : MonoBehaviour
{
    public int ownerID { get; private set; }
    public string currentCountry;

    private LineRenderer lineRenderer;
    private UnitOrder currentOrder;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;
    }

    public void Initialize(int playerID)
    {
        ownerID = playerID;
    }

    public void SetOrder(UnitOrder order, Vector3 targetPos)
    {
        currentOrder = order;
        DrawOrderLine(targetPos);
    }

    public UnitOrder GetOrder()
    {
        return currentOrder;
    }

    public void ExecuteMove(Vector3 targetPos)
    {
        transform.position = targetPos;
        if (currentOrder != null && currentOrder.type == OrderType.Move)
        {
            currentCountry = currentOrder.targetCountry;
        }
        ClearLine();
        currentOrder = null;
    }

    public void ClearLine()
    {
        lineRenderer.positionCount = 0;
    }

    private void DrawOrderLine(Vector3 targetPos)
    {
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, targetPos);
    }
}
