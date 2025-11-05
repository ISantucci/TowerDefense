using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Config inicial")]
    [SerializeField] int startLives = 20;
    [SerializeField] int startMoney = 200;

    public int Lives { get; private set; }
    public int Money { get; private set; }
    public int Score { get; private set; }

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        Lives = startLives;
        Money = startMoney;
        Score = 0;

        
        GameEvents.RaiseLivesChanged(Lives);
        GameEvents.RaiseMoneyChanged(Money);
        GameEvents.RaiseScoreChanged(Score);
    }

    
    public void AddMoney(int v)
    {
        Money += v;
        GameEvents.RaiseMoneyChanged(Money);
    }

    public bool SpendMoney(int v)
    {
        if (v <= 0) return true;
        if (Money < v) return false;
        Money -= v;
        GameEvents.RaiseMoneyChanged(Money);
        return true;
    }

    public void AddScore(int v)
    {
        Score += v;
        GameEvents.RaiseScoreChanged(Score);
    }
    public void SetMoneyLivesScore(int money, int lives, int score)
    {
        Money = money; Lives = lives; Score = score;
        GameEvents.RaiseMoneyChanged(Money);
        GameEvents.RaiseLivesChanged(Lives);
        GameEvents.RaiseScoreChanged(Score);
    }



    public void LoseLife(int v = 1)
    {
        Lives -= Mathf.Max(0, v);
        GameEvents.RaiseLivesChanged(Lives);

        if (Lives <= 0)
        {
            GameEvents.RaiseLevelLost();
            Time.timeScale = 0f; 
        }
    }
}
