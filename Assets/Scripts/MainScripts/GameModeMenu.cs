using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple menu shown after pressing Play. Player picks multiplayer or singleplayer with bots.
/// Stores the choice in static fields so the game scene can read it on load.
/// </summary>
public class GameModeMenu : MonoBehaviour
{
    /// <summary>How many AI bots to spawn (0 = multiplayer, no bots).</summary>
    public static int BotCount = 0;

    /// <summary>True if playing singleplayer with bots.</summary>
    public static bool IsSinglePlayer = false;

    [Header("UI Panels")]
    [Tooltip("Panel shown when choosing bot count")]
    public GameObject botCountPanel;

    [Header("Scene Names")]
    public string multiplayerScene = "TestmainMap";
    public string singleplayerScene = "TestmainMap";

    public void OnMultiplayer()
    {
        IsSinglePlayer = false;
        BotCount = 0;
        SceneManager.LoadScene(multiplayerScene);
    }

    public void OnSinglePlayer()
    {
        // Show bot count selection
        if (botCountPanel != null)
            botCountPanel.SetActive(true);
    }

    public void OnBotCount(int count)
    {
        IsSinglePlayer = true;
        BotCount = Mathf.Clamp(count, 1, 6);
        SceneManager.LoadScene(singleplayerScene);
    }

    // Shortcut buttons for common bot counts
    public void OnBots1() => OnBotCount(1);
    public void OnBots2() => OnBotCount(2);
    public void OnBots3() => OnBotCount(3);
    public void OnBots4() => OnBotCount(4);

    public void OnBack()
    {
        if (botCountPanel != null && botCountPanel.activeSelf)
        {
            botCountPanel.SetActive(false);
            return;
        }
        SceneManager.LoadScene("MainMenu");
    }
}
