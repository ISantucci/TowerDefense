using UnityEngine;
using UnityEngine.SceneManagement;

public class LoseState : IGameState
{
    public void Enter() {

        SceneManager.LoadScene("LoseScene");
        Time.timeScale = 1f;
    }
    public void Exit() { }
}
