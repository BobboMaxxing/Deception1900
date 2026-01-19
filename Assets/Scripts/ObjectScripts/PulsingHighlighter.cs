using UnityEngine;

public class PulsingHighlighter : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float pulseSpeed = 5f;
    public float pulseStrength = 0.4f;
    private Renderer rend;
    private Material runtimeMat;
    private Color _originalColor; 
    private bool isActive;

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
        _originalColor = runtimeMat.color;
    }

    void Update()
    {
        if (!isActive) return;

        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;

        float intensity = 1f + pulseStrength * pulse;

        runtimeMat.color = _originalColor * intensity;
    }

    public void StartPulse()
    {
        if (runtimeMat != null)
        {
            _originalColor = runtimeMat.color;
        }
        isActive = true;
    }

    public void StopPulse()
    {
        isActive = false;

        if (runtimeMat != null)
            runtimeMat.color = _originalColor;
    }
}