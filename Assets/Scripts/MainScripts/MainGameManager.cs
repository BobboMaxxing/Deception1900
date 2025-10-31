using TMPro;
using UnityEngine;

public class MainGameManager : MonoBehaviour
{
    public static MainGameManager Instance;

    public int currentYear = 1901;
    public string[] seasons = { "Spring", "Autumn" };
    private int currentSeasonIndex = 0;

    [Header("UI")]
    public TMP_Text seasonText;
    public TMP_Text yearText;

    void Awake() => Instance = this;

    void Start() => UpdateSeasonUI();

    public void NextTurn()
    {
        currentSeasonIndex++;
        if (currentSeasonIndex >= seasons.Length)
        {
            currentSeasonIndex = 0;
            currentYear++;
        }

        UpdateSeasonUI();
        Debug.Log($"Turn: {seasons[currentSeasonIndex]} {currentYear}");
    }

    private void UpdateSeasonUI()
    {
        if (seasonText != null) seasonText.text = $"Season: {seasons[currentSeasonIndex]}";
        if (yearText != null) yearText.text = $"Year: {currentYear}";
    }

    public string CurrentSeason => seasons[currentSeasonIndex];
}
