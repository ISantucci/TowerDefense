using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Punto de entrada sin escena: se registra antes de cargar la primera escena y, cada vez que carga una,
/// crea los objetos de runtime que esa escena necesita (nivel, UI dinámica, game feel).
/// Así ninguna escena tiene que editarse a mano para que existan los sistemas nuevos.
/// </summary>
public static class LevelBootstrap
{
    public const string GameplaySceneName = "SampleScene";
    public const string LevelSelectorSceneName = "LevelSelectorScene";
    public const string WinSceneName = "WinScene";
    public const string LoseSceneName = "LoseScene";
    public const string MainMenuSceneName = "MainMenuScene";

    static int lastHandledScene = int.MinValue;
    static bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        if (registered) return;
        registered = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterFirstScene()
    {
        Handle(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Handle(scene);
    }

    static void Handle(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (scene.handle == lastHandledScene) return;
        lastHandledScene = scene.handle;

        switch (scene.name)
        {
            case GameplaySceneName:
                SpawnGameplay();
                break;
            case LevelSelectorSceneName:
                LevelUI.BuildLevelSelector();
                break;
            case WinSceneName:
                LevelUI.BuildWinScreen();
                break;
            case LoseSceneName:
                LevelUI.BuildLoseScreen();
                break;
            case MainMenuSceneName:
                Time.timeScale = 1f;
                LevelUI.BuildMainMenuExtras();
                break;
        }
    }

    static void SpawnGameplay()
    {
        var level = LevelCatalog.Selected;
        if (level == null)
        {
            level = LevelCatalog.First;
            LevelCatalog.Selected = level;
        }
        if (level == null)
        {
            Debug.LogWarning("[LevelBootstrap] No hay LevelDefinition en Resources/Levels: se juega la escena tal cual.");
            return;
        }

        Time.timeScale = 1f;
        var go = new GameObject("LevelRuntime");
        var rt = go.AddComponent<LevelRuntime>();
        rt.Setup(level);

        GameFeel.Attach(rt);
        LevelUI.BuildGameplay(rt);
    }

    /// <summary>Carga la escena de juego con un nivel dado (desde el selector, el win screen o el editor).</summary>
    public static void PlayLevel(LevelDefinition level)
    {
        LevelCatalog.Selected = level;
        Time.timeScale = 1f;
        SceneManager.LoadScene(GameplaySceneName);
    }

    public static void GoToLevelSelector()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(LevelSelectorSceneName);
    }

    public static void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuSceneName);
    }
}
