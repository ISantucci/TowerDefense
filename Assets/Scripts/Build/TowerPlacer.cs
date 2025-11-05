// Assets/Scripts/Build/TowerPlacer.cs
using UnityEngine;
using UnityEngine.EventSystems;  // IMPORTANTE

public class TowerPlacer : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public LayerMask groundMask;     // tildá Ground
    public BuildGrid buildGrid;
    public TowerFactoryTD towerFactory;
    public BuildInvoker invoker;

    [Header("Selección actual")]
    public TowerId selectedTower = TowerId.Basic;
    public int selectedCost = 50;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryPlace();

        if (Input.GetKeyDown(KeyCode.Z)) invoker?.Undo();
        if (Input.GetKeyDown(KeyCode.Y)) invoker?.Redo();
    }

    public void SelectTower(TowerId id, int cost)
    {
        selectedTower = id;
        selectedCost = cost;
        Debug.Log($"[TowerPlacer] Set selección: {id} (${cost})");
    }

    void TryPlace()
    {
        // si el puntero está sobre UI, no coloco
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // útil para entender por qué no coloca
            Debug.Log("[TowerPlacer] Click sobre UI, ignoro");
            return;
        }

        var c = cam != null ? cam : Camera.main;
        if (!Physics.Raycast(c.ScreenPointToRay(Input.mousePosition),
                             out var hit, 300f, groundMask.value, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning("[TowerPlacer] Raycast no pegó Ground");
            return;
        }

        var p = buildGrid.Snap(hit.point);
        // p = buildGrid.SnapToGroundY(p, 10f, groundMask); // si querés ajustar Y

        if (!buildGrid.CanBuildAt(p))
        {
            Debug.Log("[TowerPlacer] Posición bloqueada por blockedMask");
            return;
        }

        if (towerFactory == null || invoker == null)
        {
            Debug.LogError("[TowerPlacer] Falta towerFactory o invoker asignado");
            return;
        }

        var cmd = new PlaceTowerCommand(towerFactory, selectedTower, p, Quaternion.identity, selectedCost);
        invoker.Do(cmd);
        Debug.Log($"[TowerPlacer] Colocada {selectedTower} en {p} (cost ${selectedCost})");
    }
}
