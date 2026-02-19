using Mirror;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(LineRenderer))]
public class MainUnit : NetworkBehaviour
{

    [SyncVar] public int ownerID;
    [SyncVar] public string currentCountry;
    [SyncVar] public Color playerColor;
    [SyncVar] public UnitType unitType;

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
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[Client] OnStartClient called for {name}, netId: {netId}");
        Debug.Log($"Enabled: {enabled}, GameObject active: {gameObject.activeSelf}");
    }
    #region Server → Client Initialization
    [ClientRpc]
    public void RpcInitialize(int playerID, Color color, UnitType type)
    {
        ownerID = playerID;
        playerColor = color;
        unitType = type;
        SetColor(color);
    }
    #endregion

    #region Server → Client Movement
    [ClientRpc]
    public void RpcMoveTo(Vector3 target)
    {

        if (!enabled || !gameObject.activeSelf)
        {
            Debug.LogWarning("Cannot move: component disabled or object inactive.");
            return;
        }

        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveToPositionXZ(target));
        ClearMoveLine();
    }

    private IEnumerator MoveToPositionXZ(Vector3 target)
    {
        Vector3 start = transform.position;

        // Lock Y for movement only
        target.y = start.y;

        float time = 0f;
        float duration = 0.5f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y = start.y; // hard guarantee

            transform.position = pos;
            yield return null;
        }

        transform.position = new Vector3(target.x, start.y, target.z);
    }
    #endregion

    #region Client-Side Move Line

    public void ShowLocalMoveLine(Vector3 targetPos)
    {
        if (lineRenderer == null) return;

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, new Vector3(targetPos.x, transform.position.y, targetPos.z));
    }

    public void ClearMoveLine()
    {
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
    }

    public void ClearLocalMoveLine()
    {
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
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
