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
    }

    void Update()
    {
        if (!isActive) return;
        if (runtimeMat == null) return;

        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float t = Mathf.SmoothStep(0f, 1f, pulse);
        runtimeMat.color = Color.Lerp(baseColor, Color.white, t * pulseStrength);
    }

    public void StartPulse()
    {
        if (runtimeMat == null) return;

        if (pulseRequests == 0)
            baseColor = runtimeMat.color;

        pulseRequests++;
        isActive = true;
    }

    public void StopPulse()
    {
        if (runtimeMat == null) return;

        pulseRequests = Mathf.Max(0, pulseRequests - 1);

        if (pulseRequests > 0)
            return;

        isActive = false;
        runtimeMat.color = baseColor;
    }

    public void ForceSetBaseColor(Color color)
    {
        baseColor = color;

        if (!isActive && runtimeMat != null)
            runtimeMat.color = baseColor;
    }
}