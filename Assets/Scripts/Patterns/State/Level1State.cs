using UnityEngine;
using UnityEngine.SceneManagement;

public class Level1State : IGameState
{
    public void Enter()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("SampleScene");
    }

    public void Exit()
    {
       
    }
}
