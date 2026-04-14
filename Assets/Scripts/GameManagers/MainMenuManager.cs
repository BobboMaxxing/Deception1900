using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject countrySelectionPanel;

    [Header("Buttons")]
    [SerializeField] Button singleplayerButton;
    [SerializeField] Button multiplayerButton;

    [Header("Country Selection Buttons")]
    [SerializeField] Button[] countryButtons;

    void Start()
    {
        mainMenuPanel.SetActive(true);
        countrySelectionPanel.SetActive(false);

        singleplayerButton.onClick.AddListener(OnSingleplayerClicked);
        multiplayerButton.onClick.AddListener(OnMultiplayerClicked);

        for (int i = 0; i < countryButtons.Length; i++)
        {
            int index = i;
            countryButtons[i].onClick.AddListener(() => OnCountrySelected(index));
        }
    }

    void OnSingleplayerClicked()
    {
        mainMenuPanel.SetActive(false);
        countrySelectionPanel.SetActive(true);
    }

    void OnMultiplayerClicked()
    {
        Debug.Log("Multiplayer functionality is not implemented yet.");
    }

    void OnCountrySelected(int countryIndex)
    {
        Debug.Log("Selected country index: " + countryIndex);
        countrySelectionPanel.SetActive(false);
    }
}