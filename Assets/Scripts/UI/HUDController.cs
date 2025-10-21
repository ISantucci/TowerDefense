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

    [Header("Panels")]
    public GameObject panelWin;
    public GameObject panelLose;

    void OnEnable()
    {
        GameEvents.LivesChanged += OnLives;
        GameEvents.MoneyChanged += OnMoney;
        GameEvents.ScoreChanged += OnScore;
        GameEvents.WaveChanged += OnWave;
        GameEvents.LevelWon += OnWin;
        GameEvents.LevelLost += OnLose;
    }

    void OnDisable()
    {
        GameEvents.LivesChanged -= OnLives;
        GameEvents.MoneyChanged -= OnMoney;
        GameEvents.ScoreChanged -= OnScore;
        GameEvents.WaveChanged -= OnWave;
        GameEvents.LevelWon -= OnWin;
        GameEvents.LevelLost -= OnLose;
    }

    void Start()
    {
        panelWin?.SetActive(false);
        panelLose?.SetActive(false);
    }

    void OnLives(int v) => txtLives.text = $"LIVES: {v}";
    void OnMoney(int v) => txtMoney.text = $"MONEY: {v}";
    void OnScore(int v) => txtScore.text = $"SCORE: {v}";
    void OnWave(int cur, int total) => txtWave.text = $"WAVE: {cur}/{total}";

    void OnWin() { panelWin.SetActive(true); Time.timeScale = 0f; }
    void OnLose() { panelLose.SetActive(true); Time.timeScale = 0f; }

    public void BtnRestart()
    {
        LevelManager.I.RestartLevel();
    }
}
