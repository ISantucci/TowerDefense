using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoseUI : MonoBehaviour
{
    [SerializeField] GameStateMachine stateMachine;

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
        stateMachine.ChangeState(new Level1State());
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
