using UnityEngine;
using UnityEngine.SceneManagement;

public class Level1State : IGameState
{
    public void Enter()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("SampleScene");
        Debug.Log(Time.timeScale);

    }

    public void Exit()
    {
       
    }
}
