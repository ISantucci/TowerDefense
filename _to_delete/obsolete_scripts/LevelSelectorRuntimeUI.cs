using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selector de niveles en runtime: título, grilla de cartas (3 columnas, 420x170) desde LevelCatalog.All,
/// bloqueo por progreso, estrellas, toggle de modo libre y botón para reiniciar el progreso.
/// No toca BtnBack; desactiva el BtnLevel1 legacy.
/// </summary>
public class LevelSelectorRuntimeUI : MonoBehaviour
{
    static readonly Vector2 CellSize = new Vector2(420f, 170f);
    const int Columns = 3;

    RectTransform content;
    RectTransform viewport;
    Text emptyLabel;
    Toggle freeToggle;
    Text progressLabel;
    bool suppressToggle;

    public static LevelSelectorRuntimeUI Create(Canvas canvas)
    {
        if (canvas == null) return null;
        var root = UiKit.CreateRect("LevelSelector_Runtime", canvas.transform);
        UiKit.SetFullscreen(root);
        var ui = root.gameObject.AddComponent<LevelSelectorRuntimeUI>();
        ui.Build(root);
        return ui;
    }

    void Build(RectTransform root)
    {
        // Título
        var title = UiKit.CreateText("Title", root, "Reinos de Supercell", 48, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Place(title.rectTransform, UiKit.TopCenter, UiKit.TopCenter, new Vector2(0f, -30f), new Vector2(1000f, 64f));
        UiKit.AddShadow(title.gameObject, new Color(0f, 0f, 0f, 0.85f), new Vector2(3f, -3f));
        UiKit.BestFit(title, 24, 48);

        var subtitle = UiKit.CreateText("Subtitle", root, "Elegí un reino para defender", 20, UiKit.MutedText, TextAnchor.MiddleCenter);
        UiKit.Place(subtitle.rectTransform, UiKit.TopCenter, UiKit.TopCenter, new Vector2(0f, -92f), new Vector2(1000f, 28f));
        UiKit.AddShadow(subtitle.gameObject, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));

        // Scroll view: del borde superior (-130) hasta -240 respecto del centro (deja lugar a BtnBack)
        float width = CellSize.x * Columns + 20f * (Columns - 1) + 20f + 20f;
        var viewImg = UiKit.CreatePanel("Viewport", root, new Color(0f, 0f, 0f, 0.35f), 2f);
        viewport = viewImg.rectTransform;
        viewport.anchorMin = new Vector2(0.5f, 0.5f);
        viewport.anchorMax = new Vector2(0.5f, 1f);
        viewport.pivot = new Vector2(0.5f, 1f);
        viewport.offsetMin = new Vector2(-width * 0.5f, -240f);
        viewport.offsetMax = new Vector2(width * 0.5f, -130f);
        viewImg.gameObject.AddComponent<RectMask2D>();

        var scroll = viewImg.gameObject.AddComponent<ScrollRect>();
        content = UiKit.CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, 0f);
        content.offsetMax = new Vector2(0f, 0f);

        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = CellSize;
        grid.spacing = new Vector2(20f, 20f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        scroll.inertia = true;

        emptyLabel = UiKit.CreateText("Empty", viewport, "No hay niveles en Resources/Levels", 22, UiKit.MutedText, TextAnchor.MiddleCenter);
        UiKit.Stretch(emptyLabel.rectTransform, 20f, 20f, 20f, 20f);
        emptyLabel.gameObject.SetActive(false);

        // Fila inferior: toggle de modo libre + progreso + reiniciar progreso (anclados al centro, como BtnBack)
        freeToggle = UiKit.CreateToggle("FreeModeToggle", root, "Modo libre (todo desbloqueado)", LevelCatalog.FreeMode, OnFreeModeChanged, 18);
        UiKit.Place(freeToggle.GetComponent<RectTransform>(), UiKit.Center, UiKit.MiddleLeft, new Vector2(-width * 0.5f, -272f), new Vector2(360f, 30f));

        progressLabel = UiKit.CreateText("Progress", root, string.Empty, 16, UiKit.MutedText, TextAnchor.MiddleCenter);
        UiKit.Place(progressLabel.rectTransform, UiKit.Center, UiKit.Center, new Vector2(0f, -272f), new Vector2(360f, 30f));
        UiKit.AddShadow(progressLabel.gameObject, new Color(0f, 0f, 0f, 0.8f), new Vector2(1f, -1f));

        var reset = UiKit.CreateButton("ResetProgress", root, "Reiniciar progreso", OnResetProgress, UiKit.ButtonColor, 15, new Vector2(200f, 34f));
        UiKit.Place(reset.GetComponent<RectTransform>(), UiKit.Center, UiKit.MiddleRight, new Vector2(width * 0.5f, -272f), new Vector2(200f, 34f));

        Rebuild();
    }

    // ───────────────────────── cartas ─────────────────────────

    public void Rebuild()
    {
        if (content == null) return;

        // sacar del layout antes de destruir para que la grilla no cuente hijos muertos este frame
        var old = new List<Transform>();
        for (int i = 0; i < content.childCount; i++) old.Add(content.GetChild(i));
        foreach (var t in old)
        {
            t.SetParent(null, false);
            Destroy(t.gameObject);
        }

        var levels = LevelCatalog.All;
        bool any = levels != null && levels.Count > 0;
        if (emptyLabel != null) emptyLabel.gameObject.SetActive(!any);

        int won = 0;
        if (any)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                if (l == null) continue;
                if (LevelCatalog.IsWon(l)) won++;
                BuildCard(l, i + 1);
            }
        }

        if (progressLabel != null)
            progressLabel.text = any ? "Campaña: " + won + "/" + levels.Count + " niveles" : string.Empty;

        if (freeToggle != null && freeToggle.isOn != LevelCatalog.FreeMode)
        {
            suppressToggle = true;
            freeToggle.isOn = LevelCatalog.FreeMode;
            suppressToggle = false;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        var scroll = viewport != null ? viewport.GetComponent<ScrollRect>() : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    void BuildCard(LevelDefinition level, int number)
    {
        bool unlocked = LevelCatalog.IsUnlocked(level);
        bool won = LevelCatalog.IsWon(level);
        int stars = LevelCatalog.Stars(level);
        Color fam = DefensePalette.Family(level.family);

        var card = UiKit.CreateImage("Level_" + level.levelId, content, Color.white, true);
        UiKit.AddOutline(card.gameObject, unlocked ? UiKit.WithAlpha(fam, 0.75f) : UiKit.OutlineColor, 2f);

        if (unlocked)
        {
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            btn.transition = Selectable.Transition.ColorTint;
            UiKit.SetHoverColors(btn, UiKit.CardColor);
            LevelDefinition captured = level;
            btn.onClick.AddListener(() => OnPlay(captured));
        }
        else
        {
            card.color = UiKit.CardColor;
        }

        // cuerpo (se atenúa si está bloqueado)
        var body = UiKit.CreateRect("Body", card.transform);
        UiKit.SetFullscreen(body);
        var bodyGroup = UiKit.Group(body.gameObject);
        bodyGroup.alpha = unlocked ? 1f : 0.35f;
        bodyGroup.blocksRaycasts = false;
        bodyGroup.interactable = false;

        var strip = UiKit.CreateImage("Strip", body, fam, false);
        strip.rectTransform.anchorMin = new Vector2(0f, 0f);
        strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        strip.rectTransform.pivot = new Vector2(0f, 0.5f);
        strip.rectTransform.anchoredPosition = Vector2.zero;
        strip.rectTransform.sizeDelta = new Vector2(12f, 0f);

        var num = UiKit.CreateText("Number", body, number.ToString(), 44, fam, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Place(num.rectTransform, UiKit.MiddleLeft, UiKit.MiddleLeft, new Vector2(18f, 20f), new Vector2(64f, 60f));
        UiKit.AddShadow(num.gameObject, new Color(0f, 0f, 0f, 0.7f), new Vector2(2f, -2f));

        var name = UiKit.CreateText("Name", body, level.displayName, 24, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        name.rectTransform.anchorMin = new Vector2(0f, 1f);
        name.rectTransform.anchorMax = new Vector2(1f, 1f);
        name.rectTransform.pivot = new Vector2(0f, 1f);
        name.rectTransform.anchoredPosition = new Vector2(88f, -14f);
        name.rectTransform.sizeDelta = new Vector2(-104f, 32f);
        UiKit.BestFit(name, 14, 24);

        var sub = UiKit.CreateText("Subtitle", body, level.subtitle, 15, UiKit.MutedText, TextAnchor.UpperLeft);
        sub.rectTransform.anchorMin = new Vector2(0f, 1f);
        sub.rectTransform.anchorMax = new Vector2(1f, 1f);
        sub.rectTransform.pivot = new Vector2(0f, 1f);
        sub.rectTransform.anchoredPosition = new Vector2(88f, -50f);
        sub.rectTransform.sizeDelta = new Vector2(-104f, 58f);
        UiKit.BestFit(sub, 11, 15);

        string info = DefensePalette.FamilyName(level.family) + "  ·  " + level.WaveCount() + " oleadas  ·  " + level.roster.Count + " torres";
        var infoText = UiKit.CreateText("Info", body, info, 13, fam, TextAnchor.MiddleLeft);
        infoText.rectTransform.anchorMin = new Vector2(0f, 0f);
        infoText.rectTransform.anchorMax = new Vector2(1f, 0f);
        infoText.rectTransform.pivot = new Vector2(0f, 0f);
        infoText.rectTransform.anchoredPosition = new Vector2(88f, 10f);
        infoText.rectTransform.sizeDelta = new Vector2(-104f, 20f);
        UiKit.BestFit(infoText, 9, 13);

        var starsRow = UiKit.CreateStars("Stars", body, stars, 22f);
        UiKit.Place(starsRow, UiKit.BottomLeft, UiKit.BottomLeft, new Vector2(16f, 8f), new Vector2(22f * 3f + 12f, 22f));

        if (won)
        {
            var badge = UiKit.CreateText("Won", card.transform, "SUPERADO", 12, UiKit.Good, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Place(badge.rectTransform, UiKit.TopRight, UiKit.TopRight, new Vector2(-12f, -8f), new Vector2(120f, 18f));
        }

        if (!unlocked)
        {
            var lockTitle = UiKit.CreateText("Locked", card.transform, "BLOQUEADO", 26, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(lockTitle.rectTransform, UiKit.Center, UiKit.Center, new Vector2(40f, 14f), new Vector2(300f, 36f));
            UiKit.AddShadow(lockTitle.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -2f));

            var lockHint = UiKit.CreateText("LockedHint", card.transform, "Ganá el nivel anterior", 15, UiKit.MutedText, TextAnchor.MiddleCenter);
            UiKit.Place(lockHint.rectTransform, UiKit.Center, UiKit.Center, new Vector2(40f, -16f), new Vector2(300f, 24f));
            UiKit.AddShadow(lockHint.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(1f, -1f));
        }
    }

    // ───────────────────────── acciones ─────────────────────────

    void OnPlay(LevelDefinition level)
    {
        UiKit.ClearSelection();
        if (level == null) return;
        if (!LevelCatalog.IsUnlocked(level)) return;
        LevelBootstrap.PlayLevel(level);
    }

    void OnFreeModeChanged(bool on)
    {
        if (suppressToggle) return;
        LevelCatalog.FreeMode = on;
        Rebuild();
    }

    void OnResetProgress()
    {
        UiKit.ClearSelection();
        LevelCatalog.ResetProgress();
        Rebuild();
    }
}
