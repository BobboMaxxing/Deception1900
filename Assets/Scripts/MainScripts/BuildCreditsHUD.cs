using UnityEngine;
using TMPro;

public class BuildCreditsHUD : MonoBehaviour
{
    [Header("References")]
    public TMP_Text creditsText;

    [Header("Punch Animation")]
    public float punchScale = 1.4f;
    public float punchSpeed = 8f;

    private int displayedCredits = 0;
    private bool isAnimating;
    private float animTime;
    private Vector3 originalScale = Vector3.one;
    private bool initialized;

    void Awake()
    {
        Initialize();
        gameObject.SetActive(false);
    }

    private void Initialize()
    {
        if (initialized) return;

        if (creditsText == null)
            creditsText = GetComponentInChildren<TMP_Text>(true);

        if (creditsText != null)
            originalScale = creditsText.transform.localScale;

        initialized = true;
    }

    void Update()
    {
        if (!isAnimating) return;

        animTime += Time.deltaTime * punchSpeed;

        if (animTime >= Mathf.PI)
        {
            isAnimating = false;
            creditsText.transform.localScale = originalScale;
            return;
        }

        float punch = 1f + (punchScale - 1f) * Mathf.Sin(animTime);
        creditsText.transform.localScale = originalScale * punch;
    }

    public void Show(int credits)
    {
        Initialize();
        gameObject.SetActive(true);
        displayedCredits = credits;
        UpdateText();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        isAnimating = false;
        if (creditsText != null)
            creditsText.transform.localScale = originalScale;
    }

    public void SetCredits(int credits)
    {
        if (credits < displayedCredits)
        {
            isAnimating = true;
            animTime = 0f;
        }

        displayedCredits = credits;
        UpdateText();
    }

    private void UpdateText()
    {
        if (creditsText != null)
            creditsText.SetText($"Build Points: {displayedCredits}");
    }
}
