using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{

    [SerializeField] GameStateMachine stateMachine;

    [Header("Buttons")]
    public Button btnPlay;
    public Button btnOptions;
    public Button btnQuit;

    void Start()
    {
        // Play
        if (btnPlay != null)
            btnPlay.onClick.AddListener(OnPlay);

        // Options (por ahora solo logea algo)
        if (btnOptions != null)
            btnOptions.onClick.AddListener(OnOptions);

        // Quit
        if (btnQuit != null)
            btnQuit.onClick.AddListener(OnQuit);
    }

    void OnPlay()
    {
        stateMachine.ChangeState(new LevelSelectorState());
    }

    void OnOptions()
    {
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
