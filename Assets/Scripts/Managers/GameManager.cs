using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Config inicial")]
    [SerializeField] int startLives = 20;
    [SerializeField] int startMoney = 200;

    public int Lives  { get; private set; }
    public int Money  { get; private set; }
    public int Score  { get; private set; }

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

        // stats iniciales (por si alguien los lee antes del primer nivel)
        Lives = startLives;
        Money = startMoney;
        Score = 0;

        GameEvents.RaiseLivesChanged(Lives);
        GameEvents.RaiseMoneyChanged(Money);
        GameEvents.RaiseScoreChanged(Score);

        // instanciamos estados una vez
        mainMenuState = new MainMenuState();
        level1State   = new Level1State();
        winState      = new WinState();
        loseState     = new LoseState();

        if (stateMachine == null)
            Debug.LogWarning("[GameManager] StateMachine no asignada en el inspector.");
    }

    void Start()
    {
        // SIEMPRE arrancamos en el menú principal
        if (stateMachine != null && mainMenuState != null)
            stateMachine.ChangeState(mainMenuState);
    }

    void OnEnable()
    {
        GameEvents.LevelWon  += OnLevelWon;
        GameEvents.LevelLost += OnLevelLost;
    }

    void OnDisable()
    {
        GameEvents.LevelWon  -= OnLevelWon;
        GameEvents.LevelLost -= OnLevelLost;
    }

    // ------------ API de stats ------------

    public void ResetStats()
    {
        Lives = startLives;
        Money = startMoney;
        Score = 0;

        GameEvents.RaiseLivesChanged(Lives);
        GameEvents.RaiseMoneyChanged(Money);
        GameEvents.RaiseScoreChanged(Score);
    }

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
            GameEvents.RaiseLevelLost();   // solo evento → la StateMachine se encarga
        }
    }

    // ------------ Hooks de estados ------------

    void OnLevelWon()
    {
        if (stateMachine != null && winState != null)
            stateMachine.ChangeState(winState);
    }

    void OnLevelLost()
    {
        if (stateMachine != null && loseState != null)
            stateMachine.ChangeState(loseState);
    }

    // ------------ API para UI ------------

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
