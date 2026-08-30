using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class TowerSelectionHandler : MonoBehaviour
{
    [SerializeField] TowerPlacer towerPlacer;
    [SerializeField] TowerUpgradeUI upgradeUI;
    [SerializeField] Camera cam;

    [Tooltip("Distancia perpendicular maxima (unidades world) del rayo al centro de la torre para seleccionarla")]
    public float raySelectionRadius = 1.5f;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (towerPlacer == null) towerPlacer = SceneObjects.FindPreferPersistent<TowerPlacer>();
        ResolveUpgradeUI();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Se llama despues de que cada escena termina de cargar (incluyendo reloads).
    // _Managers es DontDestroyOnLoad, entonces TowerSelectionHandler sobrevive al reload
    // mientras que UpgradePanel (en HUD_Canvas) se destruye y recrea. Hay que re-cablear.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        upgradeUI  = null;
        towerPlacer = null;
        cam        = null;

        if (cam == null) cam = Camera.main;
        if (towerPlacer == null) towerPlacer = SceneObjects.FindPreferPersistent<TowerPlacer>();
        ResolveUpgradeUI();
    }

    void ResolveUpgradeUI()
    {
        if (upgradeUI != null) return;

        upgradeUI = FindFirstObjectByType<TowerUpgradeUI>(FindObjectsInactive.Include);

        if (upgradeUI == null)
            Debug.LogError("[TowerSelection] No se encontro TowerUpgradeUI en la escena actual.");
        else
            Debug.Log("[TowerSelection] TowerUpgradeUI encontrada: " + upgradeUI.name);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (towerPlacer != null && towerPlacer.HasSelection)
                towerPlacer.CancelSelection();
            else if (upgradeUI != null)
                upgradeUI.Hide();
            return;
        }

        if (!Input.GetMouseButtonDown(0)) return;

        if (towerPlacer == null) towerPlacer = SceneObjects.FindPreferPersistent<TowerPlacer>();
        if (towerPlacer != null && towerPlacer.HasSelection)
        {
            towerPlacer.TryPlace();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("[TowerSelection] Click bloqueado: cursor sobre UI.");
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null) { Debug.LogError("[TowerSelection] No hay Camera.main."); return; }

        Debug.Log("[TowerSelection] Click detectado. Lanzando raycast...");

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 300f);

        // --- Fase 1: buscar TowerUpgradeComponent en los hits del raycast ---
        TowerUpgradeComponent found = null;

        Debug.Log("[TowerSelection] Hits detectados: " + hits.Length);
        foreach (var hit in hits)
        {
            var uc = hit.collider.GetComponentInParent<TowerUpgradeComponent>();
            if (uc == null) uc = hit.collider.GetComponentInChildren<TowerUpgradeComponent>();

            Debug.Log("  - " + hit.collider.gameObject.name
                      + " / layer " + hit.collider.gameObject.layer
                      + " / dist " + hit.distance.ToString("F2")
                      + " / TowerUpgradeComponent: " + (uc != null));

            if (uc != null && found == null)
                found = uc;
        }

        // --- Fase 2: fallback por proximidad al rayo usando Tower.Instances ---
        if (found == null && Tower.Instances.Count > 0)
        {
            Debug.Log("[TowerSelection] Fase 2: buscando torre por proximidad al rayo de camara...");

            float bestDist = raySelectionRadius;
            foreach (var tower in Tower.Instances)
            {
                if (tower == null) continue;
                var uc = tower.GetComponent<TowerUpgradeComponent>();
                if (uc == null) continue;

                float perp = Vector3.Cross(ray.direction, tower.transform.position - ray.origin).magnitude;

                Debug.Log("  [Prox] " + tower.gameObject.name
                          + " / dist_perp " + perp.ToString("F2")
                          + " / umbral " + raySelectionRadius);

                if (perp < bestDist)
                {
                    bestDist = perp;
                    found    = uc;
                }
            }
        }

        // --- Resultado ---
        if (found != null)
        {
            Debug.Log("[TowerSelection] Torre seleccionada: " + found.gameObject.name);
            ResolveUpgradeUI();
            if (upgradeUI != null) upgradeUI.Show(found);
            else Debug.LogError("[TowerSelection] upgradeUI sigue siendo null tras ResolveUpgradeUI. Verificar que UpgradePanel este en la escena.");
            return;
        }

        Debug.Log("[TowerSelection] Ninguna torre encontrada. Cerrando panel.");
        if (upgradeUI != null) upgradeUI.Hide();
    }
}
