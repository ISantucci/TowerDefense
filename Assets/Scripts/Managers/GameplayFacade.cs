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


  
    public bool CanUndoBuild => buildInvoker && buildInvoker.CanUndo;
    public bool CanRedoBuild => false;

    public void UndoBuild()
    {
        if (towerPlacer != null && towerPlacer.HasSelection)
            towerPlacer.CancelSelection();
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

    public bool BuyUpgrade(TowerUpgradeComponent upgradeComp, UpgradeStat stat)
    {
        if (upgradeComp == null) return false;
        if (buildInvoker == null) return false;

        int cost = upgradeComp.GetNextCost(stat);
        var cmd  = new UpgradeTowerCommand(upgradeComp, stat, cost);
        buildInvoker.Do(cmd);
        return cmd.IsDone;
    }

    public bool SellTower(TowerUpgradeComponent upgradeComp)
    {
        if (upgradeComp == null) return false;
        var tower = upgradeComp.GetComponent<Tower>();
        if (tower == null) return false;
        if (buildInvoker == null) return false;

        int   baseCost = tower.data != null ? tower.data.cost : 0;
        int   spent    = upgradeComp.GetTotalSpentOnUpgrades();
        float pct      = gameManager != null ? gameManager.TowerSellRefundPercent : 0.5f;
        int   refund   = Mathf.RoundToInt((baseCost + spent) * pct);

        TowerFactoryTD factory = towerPlacer != null ? towerPlacer.towerFactory : null;
        if (factory == null) factory = FindFirstObjectByType<TowerFactoryTD>();
        if (factory == null) return false;

        buildInvoker.Do(new SellTowerCommand(tower, upgradeComp, factory, refund));
        return true;
    }
}
