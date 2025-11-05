// PlaceTowerCommand.cs
using UnityEngine;

public class PlaceTowerCommand : ICommand
{
    private readonly TowerFactoryTD factory;
    private readonly TowerId towerId;
    private readonly Vector3 position;
    private readonly Quaternion rotation;
    private readonly int cost;

    private Tower created;
    public bool IsDone { get; private set; }  // <-- nuevo

    public PlaceTowerCommand(TowerFactoryTD factory, TowerId id, Vector3 pos, Quaternion rot, int cost)
    {
        this.factory = factory; towerId = id; position = pos; rotation = rot; this.cost = cost;
    }

    public void Execute()
    {
        IsDone = false;

        if (!GameManager.I.SpendMoney(cost))
        {
            Debug.LogWarning($"[PlaceTowerCommand] No alcanza dinero (${cost}).");
            return;
        }

        created = factory.Create(towerId, position, rotation);

        if (created == null)
        {
            Debug.LogError("[PlaceTowerCommand] factory.Create() devolvió null. Hago refund.");
            GameManager.I.AddMoney(cost); // rollback
            return;
        }

        IsDone = true;
        Debug.Log($"[PlaceTowerCommand] Torre {towerId} creada en {position}.");
    }

    public void Undo()
    {
        if (!IsDone) return;
        if (created != null)
        {
            Object.Destroy(created.gameObject);
            GameManager.I.AddMoney(cost);
            Debug.Log($"[PlaceTowerCommand] Undo {towerId}. Refund ${cost}.");
        }
        IsDone = false;
    }
}
