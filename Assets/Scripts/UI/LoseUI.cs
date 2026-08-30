using UnityEngine;
using UnityEngine.UI;

public class LoseUI : MonoBehaviour
{
    [SerializeField] GameStateMachine stateMachine;

    public Button btnRestart;
    public Button btnQuit;

    [Header("Campaña")]
    public Button btnChooseLevel;
    public Text resultText;

    void Start()
    {
        if (btnRestart != null) btnRestart.onClick.AddListener(OnRestart);
        if (btnQuit != null) btnQuit.onClick.AddListener(OnQuit);
        if (btnChooseLevel != null) btnChooseLevel.onClick.AddListener(() => GameFlow.GoToLevelSelector());
        if (resultText != null)
        {
            var l = LevelCatalog.Selected;
            resultText.text = l != null ? l.displayName : "";
        }
    }

    void OnRestart()
    {
        GameFlow.RetryLevel();
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
