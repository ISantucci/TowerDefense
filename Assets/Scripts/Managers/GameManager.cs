using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Config inicial")]
    [SerializeField] int startLives = 20;
    [SerializeField] int startMoney = 200;

    public int Lives { get; private set; }
    public int Money { get; private set; }
    public int Score { get; private set; }

    // =========================
    //  STATE MACHINE
    // =========================
    [Header("Game State Machine")]
    [SerializeField] GameStateMachine stateMachine;   // arrastrás el GO con GameStateMachine

    IGameState mainMenuState;
    IGameState level1State;
    IGameState winState;
    IGameState loseState;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        Lives = startLives;
        Money = startMoney;
        Score = 0;

        GameEvents.RaiseLivesChanged(Lives);
        GameEvents.RaiseMoneyChanged(Money);
        GameEvents.RaiseScoreChanged(Score);

        // ---- instanciamos estados una sola vez ----
        // Si querés que reciban referencia al GM, podés pasar "this" en el ctor.
        mainMenuState = new MainMenuState();
        level1State = new Level1State();
        winState = new WinState();
        loseState = new LoseState();

        // Por ahora arrancamos directamente en Level1
        if (stateMachine != null)
            stateMachine.ChangeState(level1State);
        else
            Debug.LogWarning("[GameManager] StateMachine no asignada en el inspector.");
    }

    void OnEnable()
    {
        GameEvents.LevelWon += OnLevelWon;
        GameEvents.LevelLost += OnLevelLost;
    }

    void OnDisable()
    {
        GameEvents.LevelWon -= OnLevelWon;
        GameEvents.LevelLost -= OnLevelLost;
    }

    // =========================
    //  API dinero / score (igual que antes)
    // =========================

    public void AddMoney(int v)
    {
        Money += v;
        GameEvents.RaiseMoneyChanged(Money);
    }

    public bool SpendMoney(int v)
    {
        if (v <= 0) return true;
        if (Money < v) return false;
        Money -= v;
        GameEvents.RaiseMoneyChanged(Money);
        return true;
    }

    public void AddScore(int v)
    {
        Score += v;
        GameEvents.RaiseScoreChanged(Score);
    }

    public void SetMoneyLivesScore(int money, int lives, int score)
    {
        Money = money;
        Lives = lives;
        Score = score;

        GameEvents.RaiseMoneyChanged(Money);
        GameEvents.RaiseLivesChanged(Lives);
        GameEvents.RaiseScoreChanged(Score);
    }

    public void LoseLife(int v = 1)
    {
        Lives -= Mathf.Max(0, v);
        GameEvents.RaiseLivesChanged(Lives);

        if (Lives <= 0)
        {
            // AHORA: pasa por EventQueue
            EventQueueManager.Enqueue(
                new GameplayEvent(GameplayEventType.LevelLost)
            );
        }
    }



    // =========================
    //  Hooks de estados
    // =========================

    void OnLevelWon()
    {
        Debug.Log("[GameManager] Level WON -> WinState + WinScene");

        if (stateMachine != null && winState != null)
            stateMachine.ChangeState(winState);

       SceneManager.LoadScene("WinScene");
    }

    void OnLevelLost()
    {
        Debug.Log("[GameManager] Level LOST -> LoseState + LoseScene");

        if (stateMachine != null && loseState != null)
            stateMachine.ChangeState(loseState);

        // acá cargás la escena donde está tu DefeatScreen + LoseUI
        SceneManager.LoadScene("LoseScene");
    }




    // Opcionales: por si desde UI querés forzar cambio de estado
    public void GoToMainMenu()
    {
        if (stateMachine != null && mainMenuState != null)
            stateMachine.ChangeState(mainMenuState);
    }

    public void StartLevel1()
    {
        if (stateMachine != null && level1State != null)
            stateMachine.ChangeState(level1State);
    }
}
