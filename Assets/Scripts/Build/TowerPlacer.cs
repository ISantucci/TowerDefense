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

    public TowerId SelectedTowerId => selectedTower;
    public int SelectedCost => selectedCost;
    public TowerData SelectedData => (HasSelection && towerFactory != null) ? towerFactory.GetData(selectedTower) : null;

    public System.Action OnSelectionCleared;
    public System.Action OnTowerPlaced;
    public System.Action<TowerId> OnTowerSelected;
    public System.Action<Vector3> OnPlacementRejected;

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

        OnTowerSelected?.Invoke(id);
    }

    void AutoBind()
    {
        if (cam == null) cam = Camera.main;
        if (buildGrid == null) buildGrid = FindFirstObjectByType<BuildGrid>(FindObjectsInactive.Include);
        if (towerFactory == null) towerFactory = FindFirstObjectByType<TowerFactoryTD>(FindObjectsInactive.Include);
        if (invoker == null) invoker = FindFirstObjectByType<BuildInvoker>(FindObjectsInactive.Include);
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
            return;

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

        // En modo spots la torre se apoya exactamente en el centro del spot (y = 0).
        var rt = LevelController.Current;
        if (rt != null && rt.UsesSpots)
        {
            var spot = rt.SpotAt(p);
            if (spot == null || spot.IsOccupied)
            {
                OnPlacementRejected?.Invoke(p);
                return;
            }
            p = spot.worldPosition;
        }
        else if (!buildGrid.CanBuildAt(p))
        {
            OnPlacementRejected?.Invoke(p);
            return;
        }

        int cost = selectedCost;
        if (cost <= 0 && towerFactory != null) cost = towerFactory.GetCost(selectedTower);

        var cmd = new PlaceTowerCommand(towerFactory, selectedTower, p, Quaternion.identity, cost);
        invoker.Do(cmd);

        if (cmd.IsDone)
            OnTowerPlaced?.Invoke();
        else
            OnPlacementRejected?.Invoke(p);

        if (!stickySelection && cmd.IsDone)
            CancelSelection();
    }
}
