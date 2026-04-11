using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class DialogManager : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    public TMP_Text dialogText;
    public Animator cornerManAnimator;
    public GameObject dialogPanel;

    [Header("Settings")]
    public float typeSpeed = 0.03f;
    public float autoDismissDelay = 3f;

    private string currentMessage;
    private bool isTyping;
    private bool fullyDisplayed;
    private Coroutine typeRoutine;
    private Coroutine dismissRoutine;

    private static DialogManager instance;
    public static DialogManager Instance => instance;

    void Awake()
    {
        instance = this;

        if (dialogPanel != null)
            dialogPanel.SetActive(false);
    }

    public static void Show(string message)
    {
        // Block gameplay dialogs during tutorial so they don't overlap
        if (TutorialManager.IsActive)
            return;

        if (instance != null)
            instance.ShowDialog(message);
    }

    /// <summary>Called only by TutorialManager — always shows, even during tutorial.</summary>
    public static void TutorialShow(string message)
    {
        if (instance != null)
            instance.ShowDialog(message);
    }

    public void ShowDialog(string message)
    {
        if (typeRoutine != null)
            StopCoroutine(typeRoutine);
        if (dismissRoutine != null)
            StopCoroutine(dismissRoutine);

        currentMessage = message;
        isTyping = true;
        fullyDisplayed = false;

        if (dialogPanel != null)
            dialogPanel.SetActive(true);

        if (dialogText != null)
            dialogText.SetText("");

        if (cornerManAnimator != null)
            cornerManAnimator.SetTrigger("Talking");

        typeRoutine = StartCoroutine(TypeText());
    }

    private IEnumerator TypeText()
    {
        if (dialogText == null) yield break;

        for (int i = 0; i < currentMessage.Length; i++)
        {
            if (!isTyping) break;

            dialogText.SetText(currentMessage.Substring(0, i + 1));
            yield return new WaitForSeconds(typeSpeed);
        }

        FinishTyping();
    }

    private void FinishTyping()
    {
        isTyping = false;
        fullyDisplayed = true;

        if (dialogText != null)
            dialogText.SetText(currentMessage);

        if (dismissRoutine != null)
            StopCoroutine(dismissRoutine);

        dismissRoutine = StartCoroutine(AutoDismiss());
    }

    private IEnumerator AutoDismiss()
    {
        yield return new WaitForSeconds(autoDismissDelay);
        Dismiss();
    }

    public void Dismiss()
    {
        if (typeRoutine != null)
            StopCoroutine(typeRoutine);
        if (dismissRoutine != null)
            StopCoroutine(dismissRoutine);

        isTyping = false;
        fullyDisplayed = false;

        if (dialogPanel != null)
            dialogPanel.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isTyping)
            FinishTyping();
        else if (fullyDisplayed)
            Dismiss();
    }

    public void OnExternalClick()
    {
        if (isTyping)
            FinishTyping();
        else if (fullyDisplayed)
            Dismiss();
    }
}
