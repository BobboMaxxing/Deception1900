using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MainMenuController : MonoBehaviour
{
    [Header("EventSystem")]
    public GameObject mainMenuFirstButton;
    public GameObject settingsFirstButton;
    public GameObject QuitGameFirstButton;

    [Header("Panels+Ani")]
    public GameObject settingsPanel;
    public Animator FadeOutAnimatior;
    public Animator settingsAnimator;
    public GameObject CloseGamePanel;

   

    public void OpenSettings()
    {
        if (settingsPanel != null && settingsAnimator != null)
        {
            settingsPanel.SetActive(true);
            settingsAnimator.SetTrigger("Open");

            if (settingsFirstButton != null)
            {
                EventSystem.current.SetSelectedGameObject(settingsFirstButton);
            }
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null && settingsAnimator != null)
        {
            settingsAnimator.SetTrigger("Close");

            if (mainMenuFirstButton != null)
            {
                EventSystem.current.SetSelectedGameObject(mainMenuFirstButton);
            }
        }
    }

    public void CloseGame()
    {
        if (CloseGamePanel != null)
        {
            CloseGamePanel.SetActive(true);
            EventSystem.current.SetSelectedGameObject(QuitGameFirstButton);
        }
   
    }

    public void LoadScene(string sceneName)
    {
        FadeOutAnimatior.SetTrigger("FadeOut");
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGameYes()
    {
        FadeOutAnimatior.SetTrigger("FadeOut");
        Application.Quit();
    }

    public void QuitGameNo()
    {
        CloseGamePanel.SetActive(false);
        EventSystem.current.SetSelectedGameObject(mainMenuFirstButton);

    }
}
