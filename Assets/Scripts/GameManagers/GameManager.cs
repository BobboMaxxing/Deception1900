using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static event System.Action OnNewTurnStarted;
    [SerializeField] private PlayerInputController playerController;

    [Header("UI")]
    [SerializeField] private TMP_Text seasonText;
    [SerializeField] private TMP_Text yearText;

    private string[] seasons = { "Spring", "Autumn" };
    private int currentSeasonIndex = 0;
    private int currentYear = 1901;

    [Header("Turn Management")]
    [SerializeField] private UnitManager unitManager;
    private bool isExecutingTurn = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        UpdateSeasonUI();
        OnNewTurnStarted?.Invoke();
    }

    public void ConfirmTurn()
    {
        if (isExecutingTurn) return;

        Debug.Log($"Turn confirmed: {seasons[currentSeasonIndex]} {currentYear}");
        StartCoroutine(ExecuteTurnRoutine());
    }

    private IEnumerator ExecuteTurnRoutine()
    {
        isExecutingTurn = true;

        // Execute all unit moves
        unitManager.ExecuteTurn();

        // Wait for all units to finish moving
        yield return new WaitForSeconds(2f); // adjust to longest move time

        // Advance season/year
        NextSeason();

        // Reset all units so they can receive new orders
        // Reset units
        unitManager.ResetUnitsForNextTurn();

        // Enable player input again
        if (playerController != null)
            playerController.EnableInput();

        isExecutingTurn = false;
    }

    private void NextSeason()
    {
        currentSeasonIndex++;
        if (currentSeasonIndex >= seasons.Length)
        {
            currentSeasonIndex = 0;
            currentYear++;
        }

        UpdateSeasonUI();
        Debug.Log($"New Turn: {seasons[currentSeasonIndex]} {currentYear}");
        OnNewTurnStarted?.Invoke();
    }

    private void UpdateSeasonUI()
    {
        if (seasonText != null) seasonText.text = $"Season: {seasons[currentSeasonIndex]}";
        if (yearText != null) yearText.text = $"Year: {currentYear}";
    }
}
