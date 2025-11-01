using Mirror;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class MainUnit : NetworkBehaviour
{
    [SyncVar] public int ownerID;
    [SyncVar] public string currentCountry;

    public PlayerUnitOrder currentOrder;

    private LineRenderer lineRenderer;
    private Coroutine moveCoroutine;
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

    [ClientRpc]
    public void RpcInitialize(int playerID, Color color)
    {
        ownerID = playerID;
        SetColor(color);
    }

    [ClientRpc]
    public void RpcMoveTo(Vector3 target)
    {
        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveToPosition(target));
    }

    private IEnumerator MoveToPosition(Vector3 targetPos)
    {
        while (Vector3.Distance(transform.position, targetPos) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
        moveCoroutine = null;
    }

    private void SetColor(Color c)
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.material.color = c;
    }

    public void ClearOrder()
    {
        currentOrder = null;
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
    }
}
