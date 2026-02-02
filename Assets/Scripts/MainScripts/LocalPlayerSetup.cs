using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LocalPlayerSetup : MonoBehaviour
{
    public Button confirmButton;
    public Button cancelButton;
    public Button confirmMoveButton;
    public Button cancelMoveButton;

    public Button buildLandButton;
    public Button buildBoatButton;
    public Button buildPlaneButton;
    public Button buildPassButton;

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

        if (buildLandButton != null)
        {
            buildLandButton.gameObject.SetActive(false);
            buildLandButton.onClick.RemoveAllListeners();
            buildLandButton.onClick.AddListener(localPlayer.SelectBuildLand);
        }

        if (buildBoatButton != null)
        {
            buildBoatButton.gameObject.SetActive(false);
            buildBoatButton.onClick.RemoveAllListeners();
            buildBoatButton.onClick.AddListener(localPlayer.SelectBuildBoat);
        }

        if (buildPlaneButton != null)
        {
            buildPlaneButton.gameObject.SetActive(false);
            buildPlaneButton.onClick.RemoveAllListeners();
            buildPlaneButton.onClick.AddListener(localPlayer.SelectBuildPlane);
        }

        if (buildPassButton != null)
        {
            buildPassButton.gameObject.SetActive(false);
            buildPassButton.onClick.RemoveAllListeners();
            buildPassButton.onClick.AddListener(localPlayer.PassBuildPhase);
        }

        localPlayer.SetupBuildUI(buildLandButton, buildBoatButton, buildPlaneButton, buildPassButton);

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
