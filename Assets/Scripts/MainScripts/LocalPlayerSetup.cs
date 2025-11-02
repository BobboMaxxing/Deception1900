using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LocalPlayerSetup : MonoBehaviour
{
    public Button confirmButton;
    public Button cancelButton;
    public Button confirmMoveButton;
    public Button cancelMoveButton;

    public TMP_Text selectedCountryText;
    public TMP_Text moveStatusText;

    public Camera playerCamera;

    public void Setup(MainPlayerController localPlayer)
    {
        if (playerCamera != null)
            playerCamera.enabled = true;

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(false);
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(localPlayer.ConfirmCountryChoice);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(false);
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(localPlayer.CancelCountryChoice);
        }

        if (confirmMoveButton != null)
        {
            confirmMoveButton.gameObject.SetActive(false);
            confirmMoveButton.onClick.RemoveAllListeners();
            confirmMoveButton.onClick.AddListener(localPlayer.ConfirmMoves);
        }

        if (cancelMoveButton != null)
        {
            cancelMoveButton.gameObject.SetActive(false);
            cancelMoveButton.onClick.RemoveAllListeners();
            cancelMoveButton.onClick.AddListener(localPlayer.CancelMoves);
        }

        localPlayer.SetupUIReferences(
            selectedCountryText,
            moveStatusText,
            confirmButton,
            cancelButton,
            confirmMoveButton,
            cancelMoveButton
        );

        localPlayer.playerCamera = playerCamera;
    }
}
