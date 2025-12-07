using UnityEngine;

public class PlaceTowerCommand : ICommand
{
    readonly TowerFactoryTD factory;
    readonly TowerId towerId;
    readonly Vector3 position;
    readonly Quaternion rotation;
    readonly int cost;

    Tower createdTower;
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
        IsDone = false;

       
        if (!GameManager.I.SpendMoney(cost))
        {
            Debug.LogWarning($"[PlaceTowerCommand] No alcanza dinero. Cost={cost}, Money={GameManager.I.Money}");
            return;
        }

        createdTower = factory.Create(towerId, position, rotation);
        if (createdTower == null)
        {
            Debug.LogError("[PlaceTowerCommand] Factory devolvió null, devolviendo plata.");
            GameManager.I.AddMoney(cost);
            return;
        }

        IsDone = true;
        Debug.Log($"[PlaceTowerCommand] Torre {towerId} creada en {position}, cost={cost}");
    }

    public void Undo()
    {
        if (!IsDone || createdTower == null) return;

        Object.Destroy(createdTower.gameObject);
        GameManager.I.AddMoney(cost);
        Debug.Log($"[PlaceTowerCommand] Undo torre {towerId}, reembolso={cost}");
        IsDone = false;
    }
}
