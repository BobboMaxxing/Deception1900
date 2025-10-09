using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class Unit : MonoBehaviour
{
    public int ownerID { get; private set; }
    public string currentCountry;

    private LineRenderer lineRenderer;
    private UnitOrder currentOrder;
    private Vector3 targetPosition;
    private Coroutine moveCoroutine;

    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 5f;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;
        lineRenderer.enabled = false;
    }

    public void Initialize(int playerID)
    {
        ownerID = playerID;
    }

    public void SetOrder(UnitOrder order, Vector3 targetPos)
    {
        currentOrder = order;
        targetPosition = targetPos;
        DrawOrderLine(targetPos);
    }

    public UnitOrder GetOrder()
    {
        return currentOrder;
    }

    public void ExecuteMove(Vector3 moveTarget)
    {
        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        moveCoroutine = StartCoroutine(MoveToPosition(moveTarget));
    }

    public void ExecuteOrder()
    {
        if (currentOrder == null) return;

        if (currentOrder.orderType == OrderType.Move && !string.IsNullOrEmpty(currentOrder.targetCountry))
        {
            GameObject targetObj = GameObject.Find(currentOrder.targetCountry);
            if (targetObj != null)
            {
                Vector3 moveTarget = targetObj.transform.position;
                moveTarget.y = transform.position.y;
                ExecuteMove(moveTarget);
            }
        }
    }

    private IEnumerator MoveToPosition(Vector3 targetPos)
    {
        while (Vector3.Distance(transform.position, targetPos) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            DrawOrderLine(targetPos);
            yield return null;
        }

        transform.position = targetPos;

        if (currentOrder != null && currentOrder.orderType == OrderType.Move)
            currentCountry = currentOrder.targetCountry;

        ClearLine();
        currentOrder = null;
        moveCoroutine = null;
    }

    private void DrawOrderLine(Vector3 targetPos)
    {
        if (lineRenderer == null) return;
        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, targetPos);
    }

    public void ClearLine()
    {
        if (lineRenderer == null) return;
        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
    }

    public void ClearOrder()
    {
        currentOrder = null;
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        ClearLine();
    }
}
