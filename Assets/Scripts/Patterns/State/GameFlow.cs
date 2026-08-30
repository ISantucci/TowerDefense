using UnityEngine;

/// <summary>
/// Puerta única para cambiar de estado desde la UI. Usa la GameStateMachine del GameManager persistente
/// si existe; si no (menú recién abierto), la de la escena actual.
/// </summary>
public static class GameFlow
{
    public static GameStateMachine Machine
    {
        get
        {
            if (GameManager.I != null && GameManager.I.StateMachine != null) return GameManager.I.StateMachine;
            return SceneObjects.FindPreferPersistent<GameStateMachine>();
        }
    }

    static void Change(IGameState state)
    {
        var m = Machine;
        if (m == null)
        {
            Debug.LogError("[GameFlow] No hay GameStateMachine en la escena.");
            return;
        }
        m.ChangeState(state);
    }

    public static void StartLevel(LevelDefinition level)
    {
        if (level == null) return;
        Change(new LevelState(level, true));
    }

    public static void RetryLevel()
    {
        var l = LevelCatalog.Selected ?? LevelCatalog.First;
        if (l != null) Change(new LevelState(l, true));
    }

    public static void NextLevel()
    {
        var next = LevelCatalog.Next(LevelCatalog.Selected);
        if (next != null && LevelCatalog.IsUnlocked(next)) Change(new LevelState(next, true));
        else GoToLevelSelector();
    }

    public static bool HasNextLevel()
    {
        var next = LevelCatalog.Next(LevelCatalog.Selected);
        return next != null && LevelCatalog.IsUnlocked(next);
    }

    public static void GoToLevelSelector() => Change(new LevelSelectorState());
    public static void GoToMainMenu() => Change(new MainMenuState());
    public static void Win() => Change(new WinState());
    public static void Lose() => Change(new LoseState());
}
