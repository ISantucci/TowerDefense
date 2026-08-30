using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Paneles de runtime para WinScene / LoseScene (abajo a la derecha). Sin animaciones ni corrutinas:
/// ambas escenas corren con Time.timeScale = 0 y uGUI funciona igual.
/// </summary>
public static class EndScreenUI
{
    public static void BuildWin(Canvas canvas)
    {
        if (canvas == null) return;
        var level = LevelCatalog.Selected;
        var next = level != null ? LevelCatalog.Next(level) : null;

        var root = UiKit.CreateRect("WinRuntimeUI", canvas.transform);
        UiKit.SetFullscreen(root);

        var panel = UiKit.CreatePanel("Panel", root, UiKit.PanelStrong, 2f);
        UiKit.Place(panel.rectTransform, UiKit.BottomRight, UiKit.BottomRight, new Vector2(-30f, 30f), new Vector2(470f, 300f));
        AddColumn(panel.gameObject);

        Color fam = level != null ? DefensePalette.Family(level.family) : UiKit.Gold;
        var strip = UiKit.CreateImage("Strip", panel.transform, fam, false);
        UiKit.Layout(strip.gameObject, 0f, 4f);

        var title = UiKit.CreateText("Title", panel.transform, "Nivel superado", 20, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Layout(title.gameObject, 0f, 26f);

        string name = level != null ? level.displayName : "Nivel";
        var nameText = UiKit.CreateText("Name", panel.transform, name, 28, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Layout(nameText.gameObject, 0f, 36f);
        UiKit.BestFit(nameText, 16, 28);

        int stars = level != null ? LevelCatalog.Stars(level) : 0;
        var starsRow = UiKit.CreateStars("Stars", panel.transform, stars, 30f);
        UiKit.Layout(starsRow.gameObject, 0f, 34f);
        var starsLayout = starsRow.GetComponent<HorizontalLayoutGroup>();
        if (starsLayout != null) starsLayout.childAlignment = TextAnchor.MiddleCenter;

        int lives = GameManager.I != null ? GameManager.I.Lives : -1;
        string detail = lives >= 0 ? "Vidas restantes: " + lives : string.Empty;
        if (level != null) detail += (detail.Length > 0 ? "   ·   " : string.Empty) + "Campaña: " + WonCount() + "/" + LevelCatalog.All.Count;
        var detailText = UiKit.CreateText("Detail", panel.transform, detail, 14, UiKit.MutedText, TextAnchor.MiddleCenter);
        UiKit.Layout(detailText.gameObject, 0f, 20f);

        if (next != null)
        {
            LevelDefinition captured = next;
            var nextBtn = UiKit.CreateButton("NextLevel", panel.transform, "Siguiente nivel: " + next.displayName, () => Play(captured), UiKit.ButtonAccent, 18, new Vector2(400f, 46f));
            UiKit.Layout(nextBtn.gameObject, 0f, 46f);
        }
        else
        {
            var done = UiKit.CreateText("Done", panel.transform, "¡Campaña completa!", 18, UiKit.Good, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Layout(done.gameObject, 0f, 46f);
        }

        var chooseBtn = UiKit.CreateButton("ChooseLevel", panel.transform, "Elegir nivel", ChooseLevel, UiKit.ButtonColor, 18, new Vector2(400f, 46f));
        UiKit.Layout(chooseBtn.gameObject, 0f, 46f);

    }

    public static void BuildLose(Canvas canvas)
    {
        if (canvas == null) return;
        var level = LevelCatalog.Selected;
        if (level == null) level = LevelCatalog.First;

        var root = UiKit.CreateRect("LoseRuntimeUI", canvas.transform);
        UiKit.SetFullscreen(root);

        var panel = UiKit.CreatePanel("Panel", root, UiKit.PanelStrong, 2f);
        UiKit.Place(panel.rectTransform, UiKit.BottomRight, UiKit.BottomRight, new Vector2(-30f, 30f), new Vector2(440f, 230f));
        AddColumn(panel.gameObject);

        Color fam = level != null ? DefensePalette.Family(level.family) : UiKit.Danger;
        var strip = UiKit.CreateImage("Strip", panel.transform, fam, false);
        UiKit.Layout(strip.gameObject, 0f, 4f);

        var title = UiKit.CreateText("Title", panel.transform, "Nivel perdido", 20, UiKit.Danger, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Layout(title.gameObject, 0f, 26f);

        string name = level != null ? level.displayName : "Nivel";
        var nameText = UiKit.CreateText("Name", panel.transform, name, 26, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.Layout(nameText.gameObject, 0f, 34f);
        UiKit.BestFit(nameText, 16, 26);

        if (level != null)
        {
            LevelDefinition captured = level;
            var retry = UiKit.CreateButton("Retry", panel.transform, "Reintentar", () => Play(captured), UiKit.ButtonAccent, 18, new Vector2(380f, 46f));
            UiKit.Layout(retry.gameObject, 0f, 46f);
        }

        var chooseBtn = UiKit.CreateButton("ChooseLevel", panel.transform, "Elegir nivel", ChooseLevel, UiKit.ButtonColor, 18, new Vector2(380f, 46f));
        UiKit.Layout(chooseBtn.gameObject, 0f, 46f);

    }

    // ───────────────────────── helpers ─────────────────────────

    static VerticalLayoutGroup AddColumn(GameObject go)
    {
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 18, 18);
        v.spacing = 8f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return v;
    }

    static int WonCount()
    {
        int won = 0;
        var all = LevelCatalog.All;
        for (int i = 0; i < all.Count; i++)
            if (LevelCatalog.IsWon(all[i])) won++;
        return won;
    }

    static void Play(LevelDefinition level)
    {
        UiKit.ClearSelection();
        if (level == null || !LevelCatalog.IsUnlocked(level)) return;
        LevelBootstrap.PlayLevel(level);
    }

    static void ChooseLevel()
    {
        UiKit.ClearSelection();
        LevelBootstrap.GoToLevelSelector();
    }
}
