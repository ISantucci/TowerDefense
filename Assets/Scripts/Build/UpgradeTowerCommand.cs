using UnityEngine;

public class UpgradeTowerCommand : ICostCommand
{
    readonly UpgradeStat stat;
    readonly Vector3     position;
    readonly int         cost;

    TowerUpgradeComponent upgradeComp;

    public int  Cost   => cost;
    public bool IsDone { get; private set; }

    public UpgradeTowerCommand(TowerUpgradeComponent upgradeComp, UpgradeStat stat, int cost)
    {
        this.upgradeComp = upgradeComp;
        this.stat        = stat;
        this.cost        = cost;
        this.position    = upgradeComp != null ? upgradeComp.transform.position : Vector3.zero;
    }

    public void Execute()
    {
        if (IsDone) return;

        var uc = Resolve();
        if (uc == null) return;

        if (!uc.ApplyNextLevel(stat)) return;

        IsDone = true;
    }

    public void Undo()
    {
        if (!IsDone) return;

        var uc = Resolve();
        if (uc == null) return;

        if (!uc.RevertLastLevel(stat)) return;

        IsDone = false;
    }

    TowerUpgradeComponent Resolve()
    {
        if (upgradeComp != null) return upgradeComp;

        foreach (var t in Tower.Instances)
        {
            if (t == null) continue;
            if (Vector3.Distance(t.transform.position, position) < 0.1f)
            {
                var uc = t.GetComponent<TowerUpgradeComponent>();
                if (uc != null) { upgradeComp = uc; return uc; }
            }
        }
        return null;
    }
}
