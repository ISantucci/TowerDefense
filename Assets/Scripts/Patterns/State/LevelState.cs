using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Estado "jugando un nivel": carga la escena del LevelDefinition. Si la escena ya está activa
/// (por ejemplo, cuando se le da Play a la escena del nivel en el Editor) no la recarga,
/// salvo que se pida explícitamente (reintentar).
/// </summary>
public class LevelState : IGameState
{
    readonly LevelDefinition level;
    readonly bool forceReload;

    public LevelDefinition Level => level;

    public LevelState(LevelDefinition level, bool forceReload = false)
    {
        this.level = level;
        this.forceReload = forceReload;
    }

    public void Enter()
    {
        Time.timeScale = 1f;
        if (level == null)
        {
            Debug.LogError("[LevelState] Sin LevelDefinition.");
            return;
        }
        LevelCatalog.Selected = level;

        string scene = string.IsNullOrEmpty(level.sceneName) ? LevelBootstrapNames.LegacyGameplayScene : level.sceneName;
        if (!forceReload && SceneManager.GetActiveScene().name == scene) return;
        SceneManager.LoadScene(scene);
    }

    public void Exit() { }
}

/// <summary>Nombres de escena que el flujo necesita conocer.</summary>
public static class LevelBootstrapNames
{
    public const string LegacyGameplayScene = "SampleScene";
    public const string MainMenu = "MainMenuScene";
    public const string LevelSelector = "LevelSelectorScene";
    public const string Win = "WinScene";
    public const string Lose = "LoseScene";
}
