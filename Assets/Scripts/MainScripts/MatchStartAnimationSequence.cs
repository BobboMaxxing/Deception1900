using System.Collections;
using Mirror;
using UnityEngine;

public class NetworkStartAnimationPlayer : MonoBehaviour
{
    [SerializeField] private Animator startAnimator;
    [SerializeField] private string startBoolName = "StartingAnim";
    [SerializeField] private float startAnimDuration = 1f;

    [SerializeField] private Animator flipAnimator;
    [SerializeField] private string flipBoolName = "FlipDone";

    private bool hasPlayed;

    void Update()
    {
        if (hasPlayed) return;
        if (!NetworkClient.isConnected) return;

        hasPlayed = true;
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        yield return null;

        if (startAnimator != null)
        {
            startAnimator.SetBool(startBoolName, true);
            yield return new WaitForSeconds(startAnimDuration);
        }

        if (flipAnimator != null)
        {
            flipAnimator.SetBool(flipBoolName, true);
        }
    }
}