using UnityEngine;

public class PulsingHighlighter : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float pulseSpeed = 2.5f;
    public float pulseStrength = 0.5f;
    public Color pulseColor = Color.white;

    private Renderer rend;
    private Material runtimeMat;
    private Color baseColor;
    private bool isActive;
    private int pulseRequests;

    private Color currentPulseTarget;
    private float timedPulseRemaining = -1f;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend == null)
        {
            Debug.LogWarning($"[PulsingHighlighter] No Renderer found on {name} or its children. Script disabled.");
            enabled = false;
            return;
        }

        runtimeMat = rend.material;
        baseColor = runtimeMat.color;
        currentPulseTarget = pulseColor;
    }

    void Update()
    {
        if (!isActive) return;
        if (runtimeMat == null) return;

        if (timedPulseRemaining > 0f)
        {
            timedPulseRemaining -= Time.deltaTime;
            if (timedPulseRemaining <= 0f)
            {
                StopTimedPulse();
                return;
            }
        }

        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float t = Mathf.SmoothStep(0f, 1f, pulse);
        runtimeMat.color = Color.Lerp(baseColor, currentPulseTarget, t * pulseStrength);
    }

    public void StartPulse()
    {
        StartPulseWithColor(pulseColor);
    }

    public void StartPulseWithColor(Color target)
    {
        if (runtimeMat == null) return;

        if (pulseRequests == 0)
            baseColor = runtimeMat.color;

        pulseRequests++;
        currentPulseTarget = target;
        isActive = true;
    }

    public void StartTimedPulse(Color target, float duration)
    {
        if (runtimeMat == null) return;

        if (pulseRequests == 0)
            baseColor = runtimeMat.color;

        pulseRequests++;
        currentPulseTarget = target;
        timedPulseRemaining = duration;
        isActive = true;
    }

    private void StopTimedPulse()
    {
        timedPulseRemaining = -1f;
        pulseRequests = Mathf.Max(0, pulseRequests - 1);

        if (pulseRequests > 0)
        {
            currentPulseTarget = pulseColor;
            return;
        }

        isActive = false;
        if (runtimeMat != null)
            runtimeMat.color = baseColor;
    }

    public void StopPulse()
    {
        if (runtimeMat == null) return;

        pulseRequests = Mathf.Max(0, pulseRequests - 1);

        if (pulseRequests > 0)
            return;

        isActive = false;
        timedPulseRemaining = -1f;
        runtimeMat.color = baseColor;
    }

    public void ForceSetBaseColor(Color color)
    {
        baseColor = color;

        if (!isActive && runtimeMat != null)
            runtimeMat.color = baseColor;
    }
}
