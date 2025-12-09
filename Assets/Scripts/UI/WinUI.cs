using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class WinUI : MonoBehaviour
{
    public Button btnContinue;
    public Button btnQuit;

    void Start()
    {
        if (btnContinue != null)
            btnContinue.onClick.AddListener(OnContinue);

        if (btnQuit != null)
            btnQuit.onClick.AddListener(OnQuit);
    }

    void OnContinue()
    {
        // Volver al menú principal
        SceneManager.LoadScene("MainMenuScene");
    }

    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
