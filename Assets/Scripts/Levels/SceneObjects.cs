using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Búsqueda de componentes que sobreviven a la recarga de escena.
/// `_Managers` es DontDestroyOnLoad: al recargar la escena de juego aparece un duplicado que GameManager destruye
/// al final del frame. Durante sceneLoaded coexisten los dos; hay que preferir siempre el persistente.
/// </summary>
public static class SceneObjects
{
    public const string PersistentSceneName = "DontDestroyOnLoad";

    public static bool IsPersistent(Component c)
    {
        return c != null && c.gameObject.scene.name == PersistentSceneName;
    }

    /// <summary>Devuelve la instancia persistente si existe; si no, la primera de la escena activa.</summary>
    public static T FindPreferPersistent<T>() where T : Component
    {
        var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return null;

        T fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null) continue;
            if (IsPersistent(c)) return c;
            if (fallback == null) fallback = c;
        }
        return fallback;
    }

    /// <summary>Devuelve la instancia que vive en la escena activa (no persistente); si no hay, cualquiera.</summary>
    public static T FindInActiveScene<T>() where T : Component
    {
        var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return null;

        Scene active = SceneManager.GetActiveScene();
        T fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null) continue;
            if (c.gameObject.scene == active) return c;
            if (fallback == null) fallback = c;
        }
        return fallback;
    }
}
