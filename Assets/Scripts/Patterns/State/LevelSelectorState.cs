using UnityEngine.SceneManagement;

public class LevelSelectorState : IGameState
{
    public void Enter() => SceneManager.LoadScene("LevelSelectorScene");
    public void Exit() { }
}
