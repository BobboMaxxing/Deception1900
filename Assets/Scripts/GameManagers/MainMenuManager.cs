using UnityEngine;
using UnityEngine.UI;
using TMPro; // For TextMeshPro

public class MainMenuManager : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject countrySelectionPanel;

    [Header("Buttons")]
    [SerializeField] Button singleplayerButton;
    [SerializeField] Button multiplayerButton;

    [Header("Country Selection Buttons")]
    [SerializeField] Button[] countryButtons; // Array of 7 buttons for the countries

    void Start()
    {
        // Initially, show the main menu and hide the country selection menu
        mainMenuPanel.SetActive(true);
        countrySelectionPanel.SetActive(false);

        // Add listeners for the main menu buttons
        singleplayerButton.onClick.AddListener(OnSingleplayerClicked);
        multiplayerButton.onClick.AddListener(OnMultiplayerClicked);

        // Add listeners for each country button
        for (int i = 0; i < countryButtons.Length; i++)
        {
            int index = i; // Capture the index for the lambda
            countryButtons[i].onClick.AddListener(() => OnCountrySelected(index));
        }
    }

    // Called when the player clicks the Singleplayer button
    void OnSingleplayerClicked()
    {
        mainMenuPanel.SetActive(false);
        countrySelectionPanel.SetActive(true); // Show country selection
    }

    // Called when the player clicks the Multiplayer button (for future implementation)
    void OnMultiplayerClicked()
    {
        // Implement multiplayer functionality later
        Debug.Log("Multiplayer functionality is not implemented yet.");
    }

    // Called when a country button is clicked (country selection)
    void OnCountrySelected(int countryIndex)
    {
        Debug.Log("Selected country index: " + countryIndex);
        // You can now store the selected country and load the game for singleplayer
        // For example, load the corresponding scene or set up the game logic for that country
        countrySelectionPanel.SetActive(false); // Hide country selection
    }
}