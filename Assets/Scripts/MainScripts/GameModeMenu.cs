using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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

    [Header("UI")]
    [Tooltip("Panel shown when choosing bot count")]
    public GameObject botCountPanel;
    [Tooltip("TMP_InputField for typing bot count (1-6)")]
    public TMP_InputField botCountInput;
    [Tooltip("Start button inside the bot count panel")]
    public GameObject startButton;

    [Header("Scene Names")]
    public string multiplayerScene = "TestmainMap";
    public string singleplayerScene = "TestmainMap";

    void Start()
    {
        if (botCountInput != null)
        {
            botCountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            botCountInput.characterLimit = 1;
            botCountInput.text = "1";
            botCountInput.onValueChanged.AddListener(OnInputChanged);
        }
    }

    private void OnInputChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (int.TryParse(value, out int num))
        {
            num = Mathf.Clamp(num, 1, 6);
            if (num.ToString() != value)
                botCountInput.text = num.ToString();
        }
        else
        {
            botCountInput.text = "1";
        }
    }

    public void OnMultiplayer()
    {
        IsSinglePlayer = false;
        BotCount = 0;
        SceneManager.LoadScene(multiplayerScene);
    }

    public void OnSinglePlayer()
    {
        if (botCountPanel != null)
            botCountPanel.SetActive(true);
    }

    public void OnStartWithBots()
    {
        int count = 1;
        if (botCountInput != null && int.TryParse(botCountInput.text, out int parsed))
            count = Mathf.Clamp(parsed, 1, 6);

        IsSinglePlayer = true;
        BotCount = count;
        SceneManager.LoadScene(singleplayerScene);
    }

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
