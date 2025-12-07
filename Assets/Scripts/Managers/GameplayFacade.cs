using UnityEngine;

public class GameplayFacade : MonoBehaviour
{
    public static GameplayFacade I { get; private set; }

    [Header("Core Systems")]
    [SerializeField] GameManager gameManager;
    [SerializeField] BuildInvoker buildInvoker;
    [SerializeField] BuildMementoManager mementoManager;
    [SerializeField] TowerPlacer towerPlacer;
   

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;

    }


    public int CurrentMoney => gameManager ? gameManager.Money : 0;
    public int CurrentLives => gameManager ? gameManager.Lives : 0;
    public int CurrentScore => gameManager ? gameManager.Score : 0;

    public void AddMoney(int amount)
    {
        gameManager?.AddMoney(amount);
    }

    public bool TrySpendMoney(int amount)
    {
        if (!gameManager) return false;
        return gameManager.SpendMoney(amount);
    }

  
    public bool CanUndoBuild => buildInvoker && buildInvoker.CanUndo;
    public bool CanRedoBuild => buildInvoker && buildInvoker.CanRedo;

    public void UndoBuild()
    {
        buildInvoker?.Undo();
    }

    public void RedoBuild()
    {
        buildInvoker?.Redo();
    }

    // ─────────────────────────────
    //  Sección: Memento (Save/Load layout)
    // ─────────────────────────────
    public bool CanLoadSnapshot => mementoManager && mementoManager.HasHistory;

    public void SaveLayout()
    {
        mementoManager?.SaveCurrent();
    }

    public void LoadLayout()
    {
        mementoManager?.RestoreLast();
    }

    // ─────────────────────────────
    //  Sección: Construcción con TowerPlacer
    //  (por ahora TowerPlacer ya maneja el input,
    //   más adelante podemos agregar métodos acá)
    // ─────────────────────────────
    public TowerPlacer GetTowerPlacer() => towerPlacer;

    
}
