using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EscMenu : MonoBehaviour
{
    [SerializeField] private GameObject escPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject quitConfirmPanel;

    [SerializeField] private Button resumeButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button mainMenuButton;

    [SerializeField] private Button saveAndQuitButton;
    [SerializeField] private Button quitNoSaveButton;
    [SerializeField] private Button cancelQuitButton;

    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isOpen;

    void Awake()
    {
        if (escPanel != null) escPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(AskQuit);

        if (saveAndQuitButton != null) saveAndQuitButton.onClick.AddListener(SaveAndQuit);
        if (quitNoSaveButton != null) quitNoSaveButton.onClick.AddListener(QuitWithoutSave);
        if (cancelQuitButton != null) cancelQuitButton.onClick.AddListener(CancelQuit);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (quitConfirmPanel != null && quitConfirmPanel.activeSelf)
            {
                CancelQuit();
                return;
            }

            if (isOpen) Resume();
            else Open();
        }
    }

    private void Open()
    {
        isOpen = true;
        if (escPanel != null) escPanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);

        Time.timeScale = 0f;

        var lp = FindLocalPlayer();
        if (lp != null) lp.canIssueOrders = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isOpen = false;
        if (escPanel != null) escPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);

        Time.timeScale = 1f;

        var lp = FindLocalPlayer();
        if (lp != null) lp.canIssueOrders = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OpenOptions()
    {
        if (!isOpen) return;
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    public void Save()
    {
        var lp = FindLocalPlayer();
        if (lp == null) return;

        if (lp.isServer)
            SaveSystem.SaveLatestServer();
        else
            lp.CmdRequestSaveLatest();
    }

    public void AskQuit()
    {
        if (!isOpen) Open();

        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    public void CancelQuit()
    {
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
    }

    public void SaveAndQuit()
    {
        Save();
        QuitWithoutSave();
    }

    public void QuitWithoutSave()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private MainPlayerController FindLocalPlayer()
    {
        var players = FindObjectsOfType<MainPlayerController>();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].isLocalPlayer)
                return players[i];
        }
        return null;
    }
}
