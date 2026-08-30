using UnityEngine;
using UnityEngine.UI;

public class WinUI : MonoBehaviour
{
    public Button btnContinue;
    public Button btnQuit;

    [Header("Campaña")]
    public Button btnNextLevel;
    public Button btnChooseLevel;
    public Text resultText;

    void Start()
    {
        if (btnContinue != null) btnContinue.onClick.AddListener(OnContinue);
        if (btnQuit != null) btnQuit.onClick.AddListener(OnQuit);
        if (btnNextLevel != null)
        {
            btnNextLevel.onClick.AddListener(() => GameFlow.NextLevel());
            btnNextLevel.gameObject.SetActive(GameFlow.HasNextLevel());
        }
        if (btnChooseLevel != null) btnChooseLevel.onClick.AddListener(() => GameFlow.GoToLevelSelector());

        if (resultText != null)
        {
            var l = LevelCatalog.Selected;
            if (l != null)
            {
                int stars = LevelCatalog.Stars(l);
                string s = "";
                for (int i = 0; i < 3; i++) s += i < stars ? "\u2605" : "\u2606";
                resultText.text = l.displayName + "  " + s;
            }
            else resultText.text = "";
        }
    }

    void OnContinue()
    {
        GameFlow.GoToMainMenu();
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
