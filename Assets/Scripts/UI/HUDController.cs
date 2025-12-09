using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

public class HUDController : MonoBehaviour
{
    [Header("Texts")]
    public UnityEngine.UI.Text txtLives;
    public UnityEngine.UI.Text txtMoney;
    public UnityEngine.UI.Text txtScore;
    public UnityEngine.UI.Text txtWave;




    void OnEnable()
    {
        GameEvents.LivesChanged += OnLives;
        GameEvents.MoneyChanged += OnMoney;
        GameEvents.ScoreChanged += OnScore;
        GameEvents.WaveChanged += OnWave;

    }

    void OnDisable()
    {
        GameEvents.LivesChanged -= OnLives;
        GameEvents.MoneyChanged -= OnMoney;
        GameEvents.ScoreChanged -= OnScore;
        GameEvents.WaveChanged -= OnWave;

    }

    void Start()
    {

    }

    void OnLives(int v) => txtLives.text = $"LIVES: {v}";
    void OnMoney(int v) => txtMoney.text = $"MONEY: {v}";
    void OnScore(int v) => txtScore.text = $"SCORE: {v}";
    void OnWave(int cur, int total) => txtWave.text = $"WAVE: {cur}/{total}";


    public void BtnRestart()
    {
        LevelManager.I.RestartLevel();
    }
}
