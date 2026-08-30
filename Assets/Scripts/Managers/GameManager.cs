using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Config inicial")]
    [SerializeField] int startLives = 20;
    [SerializeField] int startMoney = 200;

    [Header("Venta de Torres")]
    [SerializeField, Range(0f, 1f)] private float towerSellRefundPercent = 0.5f;
    public float TowerSellRefundPercent => towerSellRefundPercent;

    /// <summary>El nivel fija su propio porcentaje de reembolso al vender (LevelDefinition.sellRefund).</summary>
    public void SetSellRefundPercent(float pct)
    {
        towerSellRefundPercent = Mathf.Clamp01(pct);
    }

    public int Lives { get; private set; }
    public int Money { get; private set; }
    public int Score { get; private set; }

    [Header("Game State Machine")]
    [SerializeField] GameStateMachine stateMachine;

    public GameStateMachine StateMachine => stateMachine;

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

        mainMenuState = new MainMenuState();
        level1State = new Level1State();
        winState = new WinState();
        loseState = new LoseState();

        if (stateMachine != null)
        {
            // Primera creación (se le dio Play a una escena de nivel): el estado inicial es ESE nivel,
            // sin recargar la escena. Si la escena no es de un nivel, se conserva el flujo original.
            var current = LevelCatalog.ByScene(SceneManager.GetActiveScene().name);
            if (current != null) stateMachine.ChangeState(new LevelState(current, false));
            else stateMachine.ChangeState(level1State);
        }

        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.SetStats, Money, Lives, Score));


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

    public void AddMoney(int v)
    {
        Money += v;
    }

    public void AddScore(int v)
    {
        Score += v;
    }

    public void SetMoneyLivesScore(int money, int lives, int score)
    {
        Money = money;
        Lives = lives;
        Score = score;
    }

    public bool LoseLife(int v = 1)
    {
        Lives -= Mathf.Max(0, v);
        return Lives <= 0;
    }

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

    public void GoToMainMenu()
    {
        if (stateMachine != null && mainMenuState != null)
            stateMachine.ChangeState(mainMenuState);
    }

    public void StartLevel1()
    {
        var first = LevelCatalog.First;
        if (first != null) StartLevel(first);
        else if (stateMachine != null && level1State != null)
            stateMachine.ChangeState(level1State);
    }

    /// <summary>Entra a un nivel por la máquina de estados (recarga la escena del nivel).</summary>
    public void StartLevel(LevelDefinition level)
    {
        if (level == null || stateMachine == null) return;
        stateMachine.ChangeState(new LevelState(level, true));
    }
}
