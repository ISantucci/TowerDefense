using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo de niveles (Resources/Levels) + progreso del jugador (PlayerPrefs) + nivel seleccionado.
/// Estático a propósito: sobrevive a los cambios de escena sin depender de un GameObject.
/// </summary>
public static class LevelCatalog
{
    const string ResourcesFolder = "Levels";
    const string PrefWonPrefix = "td.level.won.";
    const string PrefBestLives = "td.level.bestlives.";
    const string PrefFreeMode = "td.freeMode";

    static List<LevelDefinition> cache;

    /// <summary>Nivel elegido en el selector. Si es null cuando carga la escena de juego, se usa el primero.</summary>
    public static LevelDefinition Selected { get; set; }

    public static IReadOnlyList<LevelDefinition> All
    {
        get
        {
            if (cache == null) Reload();
            return cache;
        }
    }

    public static void Reload()
    {
        cache = new List<LevelDefinition>();
        var loaded = Resources.LoadAll<LevelDefinition>(ResourcesFolder);
        if (loaded != null)
        {
            foreach (var l in loaded)
                if (l != null) cache.Add(l);
        }
        cache.Sort((a, b) =>
        {
            int c = a.order.CompareTo(b.order);
            return c != 0 ? c : string.CompareOrdinal(a.levelId, b.levelId);
        });
    }

    public static LevelDefinition First => All.Count > 0 ? All[0] : null;

    public static int IndexOf(LevelDefinition l)
    {
        if (l == null) return -1;
        for (int i = 0; i < All.Count; i++)
            if (All[i] == l || All[i].levelId == l.levelId) return i;
        return -1;
    }

    public static LevelDefinition Next(LevelDefinition current)
    {
        int i = IndexOf(current);
        if (i < 0 || i + 1 >= All.Count) return null;
        return All[i + 1];
    }

    public static LevelDefinition ByIndex(int i) => (i >= 0 && i < All.Count) ? All[i] : null;

    /// <summary>Nivel cuya escena es la dada (null si la escena no es de un nivel).</summary>
    public static LevelDefinition ByScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return null;
        foreach (var l in All)
            if (l != null && l.sceneName == sceneName) return l;
        return null;
    }

    // ───────── Progreso ─────────

    public static bool IsWon(LevelDefinition l)
    {
        if (l == null) return false;
        return PlayerPrefs.GetInt(PrefWonPrefix + l.levelId, 0) == 1;
    }

    public static int BestLives(LevelDefinition l)
    {
        if (l == null) return 0;
        return PlayerPrefs.GetInt(PrefBestLives + l.levelId, 0);
    }

    public static void MarkWon(LevelDefinition l, int livesLeft)
    {
        if (l == null) return;
        PlayerPrefs.SetInt(PrefWonPrefix + l.levelId, 1);
        if (livesLeft > BestLives(l)) PlayerPrefs.SetInt(PrefBestLives + l.levelId, livesLeft);
        PlayerPrefs.Save();
    }

    /// <summary>Modo libre: todos los niveles desbloqueados (para probar sin recorrer la campaña).</summary>
    public static bool FreeMode
    {
        get { return PlayerPrefs.GetInt(PrefFreeMode, 0) == 1; }
        set { PlayerPrefs.SetInt(PrefFreeMode, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>Un nivel está disponible si es el primero, si ya se ganó, si se ganó el anterior o si está el modo libre.</summary>
    public static bool IsUnlocked(LevelDefinition l)
    {
        if (l == null) return false;
        if (FreeMode) return true;
        int i = IndexOf(l);
        if (i <= 0) return true;
        return IsWon(l) || IsWon(All[i - 1]);
    }

    /// <summary>Estrellas 0..3 según las vidas que quedaron (respecto de las iniciales).</summary>
    public static int Stars(LevelDefinition l)
    {
        if (l == null || !IsWon(l)) return 0;
        int lives = BestLives(l);
        float f = l.startLives > 0 ? (float)lives / l.startLives : 0f;
        if (f >= 0.9f) return 3;
        if (f >= 0.5f) return 2;
        return 1;
    }

    public static void ResetProgress()
    {
        foreach (var l in All)
        {
            PlayerPrefs.DeleteKey(PrefWonPrefix + l.levelId);
            PlayerPrefs.DeleteKey(PrefBestLives + l.levelId);
        }
        PlayerPrefs.Save();
    }
}
