using UnityEngine;
using UnityEngine.SceneManagement;

public class WinState : IGameState
{
    public void Enter() {
        SceneManager.LoadScene("WinScene");
        Time.timeScale = 0f;

    }
    public void Exit() { }
}
