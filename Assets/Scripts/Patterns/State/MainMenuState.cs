using UnityEngine.SceneManagement;

public class MainMenuState : IGameState
{
    public void Enter() => SceneManager.LoadScene("MainMenuScene");
    public void Exit() { }
}

