using UnityEngine;

/// <summary>Compatibilidad: "nivel 1" ahora es el primer nivel del catálogo (Resources/Levels).</summary>
public class Level1State : IGameState
{
    public void Enter()
    {
        var first = LevelCatalog.First;
        if (first == null)
        {
            Debug.LogWarning("[Level1State] No hay niveles en Resources/Levels: se carga la escena legacy.");
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(LevelBootstrapNames.LegacyGameplayScene);
            return;
        }
        new LevelState(first, true).Enter();
    }

    public void Exit() { }
}
