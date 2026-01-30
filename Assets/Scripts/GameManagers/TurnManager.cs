using UnityEngine;
using TMPro;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    [Header("UI")]
    [SerializeField] private TMP_Text turnText;

    [Header("Turn Settings")]
    [SerializeField] private string[] seasons = { "Spring", "Autumn" };
    private int currentSeasonIndex = 0;
    private int currentYear = 1901;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        UpdateTurnText();
    }

    public void AdvanceTurn()
    {
        currentSeasonIndex++;
        if (currentSeasonIndex >= seasons.Length)
        {
            currentSeasonIndex = 0;
            currentYear++;
        }

        UpdateTurnText();
        Debug.Log($"Turn advanced to {seasons[currentSeasonIndex]} {currentYear}");
    }

    private void UpdateTurnText()
    {
        if (turnText != null)
            turnText.text = $"{seasons[currentSeasonIndex]} {currentYear}";
    }
}
