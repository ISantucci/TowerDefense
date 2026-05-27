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

    [Header("Selection")]
    public bool stickySelection = false;

    public bool HasSelection { get; private set; }
    TowerId selectedTower;
    int selectedCost;

    public System.Action OnSelectionCleared;
    public System.Action OnTowerPlaced;

    void Awake()
    {
        AutoBind();
    }

    void OnEnable()
    {
        AutoBind();
    }

    void Update()
    {
        if (cam == null || buildGrid == null || towerFactory == null || invoker == null)
            AutoBind();

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (HasSelection) CancelSelection();
            invoker?.Undo();
        }
    }

    public void SelectTower(TowerId id)
    {
        selectedTower = id;
        HasSelection = true;

        if (towerFactory == null) AutoBind();
        selectedCost = towerFactory != null ? towerFactory.GetCost(id) : 0;

        Debug.Log($"[TowerPlacer] Seleccionaste {id}");

    }

    void AutoBind()
    {
        if (cam == null) cam = Camera.main;
        if (buildGrid == null) buildGrid = FindObjectOfType<BuildGrid>(true);
        if (towerFactory == null) towerFactory = FindObjectOfType<TowerFactoryTD>(true);
        if (invoker == null) invoker = FindObjectOfType<BuildInvoker>(true);
    }

    public void CancelSelection()
    {
        HasSelection = false;
        OnSelectionCleared?.Invoke();
    }

    public void TryPlace()
    {
        if (!HasSelection) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("[TowerPlacer] Click bloqueado por UI (IsPointerOverGameObject = true)");
            return;
        }

        if (cam == null || buildGrid == null || towerFactory == null || invoker == null)
        {
            Debug.LogError($"[TowerPlacer] Missing refs. cam={cam != null}, grid={buildGrid != null}, factory={towerFactory != null}, invoker={invoker != null}");
            return;
        }

        if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition),
            out var hit, 300f, groundMask.value, QueryTriggerInteraction.Ignore))
            return;

        var p = buildGrid.Snap(hit.point);
        p = buildGrid.SnapToGroundY(p, 10f, groundMask);

        if (!buildGrid.CanBuildAt(p)) return;

        int cost = selectedCost;
        if (cost <= 0 && towerFactory != null) cost = towerFactory.GetCost(selectedTower);

        var cmd = new PlaceTowerCommand(towerFactory, selectedTower, p, Quaternion.identity, cost);
        invoker.Do(cmd);

        if (cmd.IsDone)
            OnTowerPlaced?.Invoke();

        if (!stickySelection && cmd.IsDone)
            CancelSelection();
    }
}
