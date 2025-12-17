using UnityEngine;

public class PlaceTowerCommand : ICommand
{
    readonly TowerFactoryTD factory;
    readonly TowerId towerId;
    readonly Vector3 position;
    readonly Quaternion rotation;
    readonly int cost;

    Tower createdTower;

    public int Cost => cost;
    public bool IsDone { get; private set; }

    public PlaceTowerCommand(TowerFactoryTD factory, TowerId towerId, Vector3 position, Quaternion rotation, int cost)
    {
        this.factory = factory;
        this.towerId = towerId;
        this.position = position;
        this.rotation = rotation;
        this.cost = cost;
    }

    public void Execute()
    {
        if (IsDone) return;
        if (factory == null) return;

        createdTower = factory.Create(towerId, position, rotation);
        IsDone = createdTower != null;
    }

    public void Undo()
    {
        if (!IsDone) return;
        if (createdTower != null)
            Object.Destroy(createdTower.gameObject);

        createdTower = null;
        IsDone = false;
    }
}
