using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Velocidad (x1/x2/x3), pausa con overlay y volumen. Piezas cableadas en el prefab LevelHUD.
/// </summary>
public class GameSpeedUI : MonoBehaviour
{
    [Header("Velocidad y pausa")]
    public Button speedButton;
    public Text speedLabel;
    public Button pauseButton;
    public Text pauseLabel;
    public float[] speeds = { 1f, 2f, 3f };

    [Header("Overlay de pausa")]
    public GameObject pauseOverlay;
    public Button resumeButton;
    public Button restartButton;
    public Button selectLevelButton;
    public Button menuButton;
    public Button volumeDownButton;
    public Button volumeUpButton;
    public Text volumeLabel;

    LevelController lc;
    int speedIndex;

    public void Bind(LevelController controller)
    {
        lc = controller;
        speedIndex = 0;
        if (speedButton != null) speedButton.onClick.AddListener(CycleSpeed);
        if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton != null) resumeButton.onClick.AddListener(() => SetPaused(false));
        if (restartButton != null) restartButton.onClick.AddListener(() => { Clear(); GameFlow.RetryLevel(); });
        if (selectLevelButton != null) selectLevelButton.onClick.AddListener(() => { Clear(); GameFlow.GoToLevelSelector(); });
        if (menuButton != null) menuButton.onClick.AddListener(() => { Clear(); GameFlow.GoToMainMenu(); });
        if (volumeDownButton != null) volumeDownButton.onClick.AddListener(() => ChangeVolume(-0.1f));
        if (volumeUpButton != null) volumeUpButton.onClick.AddListener(() => ChangeVolume(0.1f));
        if (pauseOverlay != null) pauseOverlay.SetActive(false);
        RefreshLabels();
    }

    void Update()
    {
        if (lc == null || lc.IsFinished) return;
        if (Input.GetKeyDown(KeyCode.P)) TogglePause();
    }

    void CycleSpeed()
    {
        Clear();
        if (lc == null || speeds == null || speeds.Length == 0) return;
        speedIndex = (speedIndex + 1) % speeds.Length;
        lc.SetGameSpeed(speeds[speedIndex]);
        ProceduralAudio.Play(Sfx.Click);
        RefreshLabels();
    }

    void TogglePause()
    {
        if (lc == null) return;
        SetPaused(!lc.IsPaused);
    }

    void SetPaused(bool paused)
    {
        Clear();
        if (lc == null) return;
        lc.SetPaused(paused);
        if (pauseOverlay != null) pauseOverlay.SetActive(paused);
        ProceduralAudio.Play(Sfx.Click);
        RefreshLabels();
    }

    void ChangeVolume(float delta)
    {
        UiAudioBridge.Volume = Mathf.Clamp01(UiAudioBridge.Volume + delta);
        ProceduralAudio.Play(Sfx.Click);
        RefreshLabels();
    }

    void RefreshLabels()
    {
        if (speedLabel != null && speeds != null && speeds.Length > 0)
            speedLabel.text = "x" + speeds[Mathf.Clamp(speedIndex, 0, speeds.Length - 1)].ToString("0.#");
        if (pauseLabel != null) pauseLabel.text = (lc != null && lc.IsPaused) ? "▶" : "II";
        if (volumeLabel != null) volumeLabel.text = "Volumen " + Mathf.RoundToInt(UiAudioBridge.Volume * 100f) + "%";
    }

    static void Clear()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
}
