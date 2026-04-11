using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialPromptUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject promptPanel;
    public Toggle dontShowAgainToggle;

    [Header("Scene")]
    public string tutorialSceneName = "TutorialScene";
    public string normalSceneName = "TestmainMap";

    private const string PREF_KEY = "SkipTutorialPrompt";

    void Start()
    {
        if (PlayerPrefs.GetInt(PREF_KEY, 0) == 1)
        {
            if (promptPanel != null)
                promptPanel.SetActive(false);
            return;
        }

        if (promptPanel != null)
            promptPanel.SetActive(true);
    }

    public void OnPlayTutorial()
    {
        SavePreference();
        SceneManager.LoadScene(tutorialSceneName);
    }

    public void OnSkipTutorial()
    {
        SavePreference();

        if (promptPanel != null)
            promptPanel.SetActive(false);
    }

    private void SavePreference()
    {
        if (dontShowAgainToggle != null && dontShowAgainToggle.isOn)
        {
            PlayerPrefs.SetInt(PREF_KEY, 1);
            PlayerPrefs.Save();
        }
    }

    public static void ResetTutorialPrompt()
    {
        PlayerPrefs.DeleteKey(PREF_KEY);
        PlayerPrefs.Save();
    }
}
