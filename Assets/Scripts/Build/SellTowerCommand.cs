using UnityEngine;

public class SellTowerCommand : ICostCommand
{
    readonly TowerFactoryTD factory;
    readonly TowerId        towerId;
    readonly Vector3        position;
    readonly Quaternion     rotation;
    readonly int            refundAmount;

    readonly int savedDamageLevel;
    readonly int savedSpeedLevel;
    readonly int savedRangeLevel;

    Tower originalTower;
    Tower restoredTower;

    public bool IsDone { get; private set; }
    public int  Cost   => 0;

    public SellTowerCommand(Tower tower, TowerUpgradeComponent upgradeComp,
                            TowerFactoryTD factory, int refund)
    {
        this.factory      = factory;
        this.towerId      = tower.towerType;
        this.position     = tower.transform.position;
        this.rotation     = tower.transform.rotation;
        this.refundAmount = refund;
        originalTower     = tower;

        if (upgradeComp != null)
        {
            savedDamageLevel = upgradeComp.GetCurrentLevel(UpgradeStat.Damage);
            savedSpeedLevel  = upgradeComp.GetCurrentLevel(UpgradeStat.AttackSpeed);
            savedRangeLevel  = upgradeComp.GetCurrentLevel(UpgradeStat.Range);
        }
    }

    public void Execute()
    {
        if (IsDone) return;

        Tower target = restoredTower != null ? restoredTower : originalTower;
        if (target == null) return;

        EventQueueManager.Enqueue(GameplayEvent.AddMoney(refundAmount));
        Object.Destroy(target.gameObject);
        restoredTower = null;
        IsDone        = true;
    }

    public void Undo()
    {
        if (!IsDone) return;

        restoredTower = factory.Create(towerId, position, rotation);
        if (restoredTower == null) return;

        var uc = restoredTower.GetComponent<TowerUpgradeComponent>();
        uc?.RestoreState(savedDamageLevel, savedSpeedLevel, savedRangeLevel);

        EventQueueManager.Enqueue(GameplayEvent.AddMoney(-refundAmount));
        IsDone = false;
    }
}
