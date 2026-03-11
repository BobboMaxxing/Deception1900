using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(LineRenderer))]
public class MainUnit : NetworkBehaviour
{

    [SyncVar] public int ownerID;
    [SyncVar] public string currentCountry;
    [SyncVar] public Color playerColor;
    [SyncVar] public UnitType unitType;
    [SerializeField] private Renderer[] colorRenderers;

    [HideInInspector] public PlayerUnitOrder currentOrder;

    private LineRenderer lineRenderer;
    private Coroutine moveCoroutine;

    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float boatMoveDuration = 0.6f;
    [SerializeField] private float defaultMoveDuration = 0.5f;
    [SerializeField] private float boatCurveStrength = 0.22f;


    [SerializeField] private TMP_Text supportCountText;
    private int localIncomingSupportCount;

    [SerializeField] private float particleMoveThreshold = 0.75f;
    [SerializeField] private List<ParticleSystem> landMoveParticles = new List<ParticleSystem>();
    [SerializeField] private List<ParticleSystem> boatMoveParticles = new List<ParticleSystem>();
    [SerializeField] private List<ParticleSystem> planeMoveParticles = new List<ParticleSystem>();

    void Awake()
    {
        StopMoveParticles();
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;
        lineRenderer.enabled = false;
        if (selectionHighlight != null)
            selectionHighlight.SetActive(false);
        if (supportCountText != null)
        {
            supportCountText.gameObject.SetActive(false);
            supportCountText.text = "";
        }
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

        Vector3 current = transform.position;
        Vector3 flatTarget = new Vector3(target.x, current.y, target.z);
        float moveDistance = Vector3.Distance(current, flatTarget);

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        if (moveDistance >= particleMoveThreshold)
            PlayMoveParticles();
        else
            StopMoveParticles();

        if (unitType == UnitType.Boat)
            moveCoroutine = StartCoroutine(MoveBoatCurvedXZ(target));
        else
            moveCoroutine = StartCoroutine(MoveToPositionXZ(target));

        ClearMoveLine();
    }

    [SerializeField] private float turnSpeed = 12f;

    private IEnumerator MoveToPositionXZ(Vector3 target)
    {
        Vector3 start = transform.position;
        target.y = start.y;

        float time = 0f;
        float duration = defaultMoveDuration;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            Vector3 currentPos = transform.position;
            Vector3 nextPos = Vector3.Lerp(start, target, t);
            nextPos.y = start.y;

            Vector3 moveDir = nextPos - currentPos;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
            }

            transform.position = nextPos;
            yield return null;
        }

        transform.position = new Vector3(target.x, start.y, target.z);
        StopMoveParticles();
        moveCoroutine = null;
    }

    private Vector3 GetQuadraticBezierPoint(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }
    private IEnumerator MoveBoatCurvedXZ(Vector3 target)
    {
        Vector3 start = transform.position;
        target.y = start.y;

        Vector3 flatDir = target - start;
        flatDir.y = 0f;

        float distance = flatDir.magnitude;

        if (distance <= 0.01f)
        {
            transform.position = target;
            StopMoveParticles();
            moveCoroutine = null;
            yield break;
        }

        Vector3 dir = flatDir.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;

        float sign = Random.value < 0.5f ? -1f : 1f;
        Vector3 mid = (start + target) * 0.5f;
        Vector3 control = mid + side * (distance * boatCurveStrength * sign);
        control.y = start.y;

        float time = 0f;
        float duration = boatMoveDuration;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            Vector3 currentPos = transform.position;
            Vector3 nextPos = GetQuadraticBezierPoint(start, control, target, t);
            nextPos.y = start.y;

            Vector3 moveDir = nextPos - currentPos;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
            }

            transform.position = nextPos;
            yield return null;
        }

        transform.position = new Vector3(target.x, start.y, target.z);
        StopMoveParticles();
        moveCoroutine = null;
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
        StopMoveParticles();
    }

    [SerializeField] private GameObject selectionHighlight;

    public void SetSelectedVisual(bool state)
    {
        if (selectionHighlight != null)
            selectionHighlight.SetActive(state);
    }
    private void SetColor(Color c)
    {
        Color lighter = Color.Lerp(c, Color.white, 0.35f);
        lighter.a = c.a;

        if (colorRenderers == null) return;

        for (int i = 0; i < colorRenderers.Length; i++)
        {
            if (colorRenderers[i] == null) continue;
            colorRenderers[i].material.color = lighter;
        }
    }
    public void SetLocalIncomingSupportCount(int count)
    {
        localIncomingSupportCount = count;

        if (supportCountText == null) return;

        if (localIncomingSupportCount > 0)
        {
            supportCountText.gameObject.SetActive(true);
            supportCountText.text = $"+{localIncomingSupportCount}";
        }
        else
        {
            supportCountText.gameObject.SetActive(false);
            supportCountText.text = "";
        }
    }

    public void ClearLocalIncomingSupportCount()
    {
        SetLocalIncomingSupportCount(0);
    }

    #endregion
    #region Particle Effects
    private void PlayParticleList(List<ParticleSystem> particles)
    {
        if (particles == null) return;

        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i] != null)
                particles[i].Play();
        }
    }

    private void StopParticleList(List<ParticleSystem> particles)
    {
        if (particles == null) return;

        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i] != null)
                particles[i].Stop();
        }
    }

    private void PlayMoveParticles()
    {
        StopMoveParticles();

        if (unitType == UnitType.Plane)
            PlayParticleList(planeMoveParticles);
        else if (unitType == UnitType.Boat)
            PlayParticleList(boatMoveParticles);
        else
            PlayParticleList(landMoveParticles);
    }
        
    private void StopMoveParticles()
    {
        StopParticleList(landMoveParticles);
        StopParticleList(boatMoveParticles);
        StopParticleList(planeMoveParticles);
    }

    #endregion
}
