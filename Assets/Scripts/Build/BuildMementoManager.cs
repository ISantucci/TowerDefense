// Assets/Scripts/Build/BuildMementoManager.cs
using UnityEngine;

public class BuildMementoManager : MonoBehaviour
{
    public TowerFactoryTD towerFactory;   
    public BuildInvoker invoker;          

    BuildSnapshot lastSnapshot;

  
    public BuildSnapshot CreateSnapshot()
    {
        var snap = new BuildSnapshot();

        foreach (var tower in FindObjectsOfType<Tower>())
        {
            snap.towers.Add(new BuildSnapshot.TowerData
            {
                towerId = tower.towerType,
                position = tower.transform.position,
                rotation = tower.transform.rotation
            });
        }

        snap.money = GameManager.I.Money;
        snap.lives = GameManager.I.Lives;
        snap.score = GameManager.I.Score;

        Debug.Log($"[Memento] CREATE -> towers={snap.towers.Count}, money={snap.money}");
        return snap;
    }

    // --- ORIGINATOR: restaurar desde snapshot ---
    public void RestoreSnapshot(BuildSnapshot snap)
    {
        if (snap == null) return;

        Debug.Log($"[Memento] RESTORE -> towers={snap.towers.Count}, money={snap.money}");

        // 1) destruir TODAS las torres registradas
        var currentTowers = Tower.Instances.ToArray();
        foreach (var t in currentTowers)
        {
            if (t != null)
                Destroy(t.gameObject); // t está en el root
        }

        // 2) recrear torres desde el snapshot
        foreach (var td in snap.towers)
        {
            towerFactory.Create(td.towerId, td.position, td.rotation);
        }

        // 3) restaurar estado del jugador
        GameManager.I.SetMoneyLivesScore(snap.money, snap.lives, snap.score);

        // 4) limpiar historial
        invoker?.ClearHistory();
        Debug.Log("[Memento] Snapshot restaurado (historial limpiado).");
    }



    // --- API para HUD ---
    public void SaveCurrent()
    {
        lastSnapshot = CreateSnapshot();
        Debug.Log("[Memento] Snapshot guardado (slot único).");
    }


    public void RestoreLast()
    {
        if (lastSnapshot == null)
        {
            Debug.Log("[Memento] No hay snapshot para cargar.");
            return;
        }

        RestoreSnapshot(lastSnapshot);
    }

    public bool HasHistory => lastSnapshot != null;
}
