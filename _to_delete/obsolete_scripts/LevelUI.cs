using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Punto de entrada de la UI de runtime (la llama LevelBootstrap al cargar cada escena).
/// Todo se construye por código bajo el Canvas de la escena; no hay prefabs ni cambios de escena.
/// </summary>
public static class LevelUI
{
    public const string GameplayCanvasName = "HUD_Canvas";
    public const string LegacyTowerPanelName = "TowerButtonsPanel";
    public const string LegacyLevelButtonName = "BtnLevel1";

    /// <summary>HUD de juego: catálogo de torres, oleadas, velocidad/pausa, preview de colocación.</summary>
    public static void BuildGameplay(LevelRuntime rt)
    {
        if (rt == null) return;

        var canvas = UiKit.FindCanvas(GameplayCanvasName);
        if (canvas == null)
        {
            Debug.LogWarning("[LevelUI] No hay Canvas en la escena de juego: no se construye el HUD de runtime.");
            return;
        }

        UiAudioBridge.ApplySaved();

        // El catálogo nuevo reemplaza a los botones legacy (Archer/Bomber).
        if (!UiKit.DeactivateChild(canvas.transform, LegacyTowerPanelName))
            Debug.Log("[LevelUI] No se encontró " + LegacyTowerPanelName + " (ya estaba apagado o no existe).");

        var root = UiKit.CreateRect("LevelUI_Root", canvas.transform);
        UiKit.SetFullscreen(root);
        root.SetAsLastSibling();

        // Orden de creación = orden de dibujo: oleadas, catálogo (con tooltip), velocidad (con overlay de pausa).
        WaveHUD.Create(rt, root);
        TowerCatalogUI.Create(rt, root);
        GameSpeedUI.Create(rt, root);
        PlacementPreview.Create(rt);
    }

    /// <summary>Cartas de nivel en LevelSelectorScene (reemplaza a BtnLevel1; BtnBack sigue como está).</summary>
    public static void BuildLevelSelector()
    {
        var canvas = UiKit.FindCanvas(null);
        if (canvas == null)
        {
            Debug.LogWarning("[LevelUI] No hay Canvas en el selector de niveles.");
            return;
        }

        UiAudioBridge.ApplySaved();
        UiKit.DeactivateChild(canvas.transform, LegacyLevelButtonName);
        LevelSelectorRuntimeUI.Create(canvas);
    }

    /// <summary>WinScene: siguiente nivel / elegir nivel (timeScale 0: sin corrutinas ni tiempo escalado).</summary>
    public static void BuildWinScreen()
    {
        var canvas = UiKit.FindCanvas(null);
        if (canvas == null) return;
        EndScreenUI.BuildWin(canvas);
    }

    /// <summary>LoseScene: reintentar / elegir nivel.</summary>
    public static void BuildLoseScreen()
    {
        var canvas = UiKit.FindCanvas(null);
        if (canvas == null) return;
        EndScreenUI.BuildLose(canvas);
    }

    /// <summary>Menú principal: progreso de campaña y volumen.</summary>
    public static void BuildMainMenuExtras()
    {
        var canvas = UiKit.FindCanvas(null);
        if (canvas == null) return;

        UiAudioBridge.ApplySaved();

        var root = UiKit.CreateRect("MainMenuExtras", canvas.transform);
        UiKit.SetFullscreen(root);

        int total = LevelCatalog.All.Count;
        int won = 0;
        for (int i = 0; i < total; i++)
            if (LevelCatalog.IsWon(LevelCatalog.All[i])) won++;

        var progressPanel = UiKit.CreatePanel("ProgressPanel", root, UiKit.PanelColor, 2f, false);
        UiKit.Place(progressPanel.rectTransform, UiKit.BottomLeft, UiKit.BottomLeft, new Vector2(20f, 20f), new Vector2(320f, 44f));
        var label = UiKit.CreateText("Progress", progressPanel.transform,
            total > 0 ? "Campaña: " + won + "/" + total + " niveles" : "Campaña: sin niveles cargados",
            18, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        UiKit.Stretch(label.rectTransform, 14f, 2f, 14f, 2f);
        UiKit.BestFit(label, 11, 18);

        var volumePanel = UiKit.CreatePanel("VolumePanel", root, UiKit.PanelColor, 2f);
        UiKit.Place(volumePanel.rectTransform, UiKit.BottomRight, UiKit.BottomRight, new Vector2(-20f, 20f), new Vector2(320f, 44f));
        var volLabel = UiKit.CreateText("VolumeLabel", volumePanel.transform, "Volumen", 15, UiKit.MutedText, TextAnchor.MiddleLeft);
        UiKit.Place(volLabel.rectTransform, UiKit.MiddleLeft, UiKit.MiddleLeft, new Vector2(14f, 0f), new Vector2(80f, 30f));
        var slider = UiKit.CreateSlider("VolumeSlider", volumePanel.transform, UiAudioBridge.Volume, OnMenuVolumeChanged);
        UiKit.Place(slider.GetComponent<RectTransform>(), UiKit.MiddleLeft, UiKit.MiddleLeft, new Vector2(96f, 0f), new Vector2(210f, 26f));
    }

    static void OnMenuVolumeChanged(float v)
    {
        UiAudioBridge.Volume = v;
    }
}
