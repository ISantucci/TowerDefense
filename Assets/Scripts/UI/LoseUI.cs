using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoseUI : MonoBehaviour
{
    public Button btnRestart;
    public Button btnQuit;

    void Start()
    {
        if (btnRestart != null)
            btnRestart.onClick.AddListener(OnRestart);

        if (btnQuit != null)
            btnQuit.onClick.AddListener(OnQuit);
    }

    void OnRestart()
    {
       SceneManager.LoadScene("SampleScene");
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
