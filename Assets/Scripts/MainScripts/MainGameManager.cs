using Mirror;
using TMPro;
using UnityEngine;

public class MainGameManager : NetworkBehaviour
{
    public static MainGameManager Instance;

    [SyncVar] public int currentYear = 1901;
    [SyncVar] public string currentSeason = "Spring";

    private string[] seasons = { "Spring", "Autumn" };
    private int currentSeasonIndex = 0;

    [Header("UI")]
    public TMP_Text seasonText;
    public TMP_Text yearText;

    void Awake() => Instance = this;
    void Start() => UpdateSeasonUI();

    [Server]
    public void NextTurnServer()
    {
        currentSeasonIndex++;
        if (currentSeasonIndex >= seasons.Length)
        {
            currentSeasonIndex = 0;
            currentYear++;
        }
        currentSeason = seasons[currentSeasonIndex];
        RpcUpdateSeasonUI(currentSeason, currentYear);
    }

    [ClientRpc]
    private void RpcUpdateSeasonUI(string season, int year)
    {
        if (seasonText != null) seasonText.text = $"Season: {season}";
        if (yearText != null) yearText.text = $"Year: {year}";
    }

    void UpdateSeasonUI()
    {
        if (seasonText != null) seasonText.text = $"Season: {currentSeason}";
        if (yearText != null) yearText.text = $"Year: {currentYear}";
    }
}
