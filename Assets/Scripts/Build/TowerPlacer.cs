using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TowerPlacer : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public LayerMask groundMask;
    public BuildGrid buildGrid;
    public TowerFactoryTD towerFactory;
    public BuildInvoker invoker;

    [Header("Selección")]
    public bool stickySelection = false;   // si false, se deselecciona después de colocar
    bool hasSelection = false;
    TowerId selectedTower;
    int selectedCost;

    // tabla de costos (puede venir de otro lado, pero algo así)
    public Dictionary<TowerId, int> towerCosts = new Dictionary<TowerId, int>
    {
        { TowerId.Basic, 50 },
        // { TowerId.Sniper, 120 }, etc.
    };

    // para avisar a los botones que apaguen el highlight
    public System.Action OnSelectionCleared;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryPlace();

        if (Input.GetKeyDown(KeyCode.Z)) invoker?.Undo();
        if (Input.GetKeyDown(KeyCode.Y)) invoker?.Redo();
    }

    public void SelectTower(TowerId id)
    {
        selectedTower = id;
        hasSelection = true;
        selectedCost = towerCosts.TryGetValue(id, out var c) ? c : 0;

        Debug.Log($"[TowerPlacer] Selección: {id} (${selectedCost})");
    }

    void ClearSelection()
    {
        hasSelection = false;
        OnSelectionCleared?.Invoke();
        Debug.Log("[TowerPlacer] Selección limpiada");
    }

    void TryPlace()
    {
        // ?? esto es lo que evita construir sin botón
        if (!hasSelection)
        {
            // Debug.Log("[TowerPlacer] No hay torre seleccionada");
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        var c = cam != null ? cam : Camera.main;
        if (!Physics.Raycast(c.ScreenPointToRay(Input.mousePosition),
                             out var hit, 300f, groundMask.value, QueryTriggerInteraction.Ignore))
            return;

        var p = buildGrid.Snap(hit.point);
        p = buildGrid.SnapToGroundY(p, 10f, groundMask);

        if (!buildGrid.CanBuildAt(p)) return;

        var cmd = new PlaceTowerCommand(towerFactory, selectedTower, p, Quaternion.identity, selectedCost);
        invoker.Do(cmd);

        // si el comando se ejecutó y no queremos "pintar", limpiamos selección
        if (!stickySelection && cmd.IsDone)
            ClearSelection();
    }
}
