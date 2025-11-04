using Mirror;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(LineRenderer))]
public class MainUnit : NetworkBehaviour
{
    [SyncVar] public int ownerID;
    [SyncVar] public string currentCountry;
    [SyncVar] public Color playerColor;

    [HideInInspector] public PlayerUnitOrder currentOrder;

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

    #region Server → Client Initialization
    [ClientRpc]
    public void RpcInitialize(int playerID, Color color)
    {
        ownerID = playerID;
        playerColor = color;
        SetColor(color);
    }
    #endregion

    #region Server → Client Movement
    [ClientRpc]
    public void RpcMoveTo(Vector3 target)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);

        // Hide local line when server moves the unit
        if (lineRenderer != null) lineRenderer.enabled = false;

        moveCoroutine = StartCoroutine(MoveToPosition(target));
    }

    private IEnumerator MoveToPosition(Vector3 targetPos)
    {
        Vector3 startPos = transform.position;
        targetPos.y = startPos.y;

        while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                new Vector3(targetPos.x, 0, targetPos.z)) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos;
        moveCoroutine = null;
    }
    #endregion

    #region Client-Side Move Line

    public void ShowLocalMoveLine(Vector3 targetPos)
    {
        if (!isOwned) return; 

        if (lineRenderer == null) return;
        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, new Vector3(targetPos.x, transform.position.y, targetPos.z));
    }


    public void SetupLocalVisuals()
    {
        if (lineRenderer != null) lineRenderer.enabled = false;
        SetColor(playerColor);
    }
    #endregion

    #region Utility
    public void ClearOrder()
    {
        currentOrder = null;
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        if (lineRenderer != null) lineRenderer.enabled = false;
    }

    private void SetColor(Color c)
    {
        Color darker = c * 0.5f;
        darker.a = c.a;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            rend.material.color = darker;
        }
    }
    #endregion
}
