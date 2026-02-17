using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuContinue : MonoBehaviour
{
    [SerializeField] private Button continueButton;

    void Start()
    {
        if (continueButton == null) return;

        bool has = SaveSystem.HasLatestSave();
        continueButton.gameObject.SetActive(has);
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(Continue);
    }

    private void Continue()
    {
        var data = SaveSystem.LoadLatest();
        if (data == null) return;

        SaveLoadFlag.ShouldLoadLatest = true;
        SceneManager.LoadScene(data.sceneName);
    }
}