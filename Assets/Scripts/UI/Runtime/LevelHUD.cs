using UnityEngine;

/// <summary>
/// Raíz del HUD del nivel (prefab LevelHUD, instanciado dentro de HUD_Canvas en cada escena de nivel).
/// Todo lo que muestra está en la jerarquía y se cablea en el Inspector; los scripts sólo llenan datos.
/// </summary>
public class LevelHUD : MonoBehaviour
{
    [Header("Paneles (cableados en el prefab)")]
    public TowerCatalogUI towerCatalog;
    public WaveHUD waveHud;
    public GameSpeedUI speedUi;
    public PlacementPreview placementPreview;

    public static LevelHUD Current { get; private set; }

    void Awake()
    {
        Current = this;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        var lc = LevelController.Current;
        if (lc == null)
        {
            Debug.LogError("[LevelHUD] No hay LevelController en la escena.", this);
            return;
        }
        if (towerCatalog != null) towerCatalog.Bind(lc);
        if (waveHud != null) waveHud.Bind(lc);
        if (speedUi != null) speedUi.Bind(lc);
        if (placementPreview != null) placementPreview.Bind(lc);
        UiAudioBridge.ApplySaved();
    }
}
