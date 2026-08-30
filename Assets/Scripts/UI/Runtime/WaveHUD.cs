using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Panel de oleadas del prefab LevelHUD: nombre del nivel (se desvanece), "Oleada n / N", resumen de enemigos,
/// barra de cuenta regresiva con botón "¡Siguiente oleada!", contador de enemigos y toasts.
/// Todas las piezas están en el prefab; acá sólo se escriben datos.
/// </summary>
public class WaveHUD : MonoBehaviour
{
    [Header("Banner del nivel")]
    public CanvasGroup levelBanner;
    public Text levelNameText;
    public Text levelSubtitleText;
    public float bannerHold = 3.5f;
    public float bannerFade = 1.2f;

    [Header("Oleada")]
    public Text waveText;
    public Text summaryText;
    public Text enemiesText;

    [Header("Cuenta regresiva")]
    public Image countdownFill;
    public Text countdownText;
    public Button nextWaveButton;
    public Text nextWaveLabel;

    [Header("Toasts")]
    public RectTransform toastRoot;
    public Text toastTemplate;
    public float toastLife = 1.8f;
    public float toastHeight = 34f;

    static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    static readonly Color Danger = new Color(1f, 0.4f, 0.35f);

    LevelController lc;
    TowerPlacer placer;
    int shownWave, totalWaves, prepBonus, shownBonus = -1;
    bool inPrep, finished, bannerDone;
    float bannerTime;

    class ToastItem { public RectTransform rect; public CanvasGroup group; public float t; }
    readonly List<ToastItem> toasts = new List<ToastItem>();

    public void Bind(LevelController controller)
    {
        lc = controller;
        placer = lc != null ? lc.Placer : null;

        if (lc != null && lc.Level != null)
        {
            if (levelNameText != null) levelNameText.text = lc.Level.displayName;
            if (levelSubtitleText != null) levelSubtitleText.text = lc.Level.subtitle;
        }
        if (levelBanner != null) { levelBanner.alpha = 1f; levelBanner.gameObject.SetActive(true); }
        if (toastTemplate != null) toastTemplate.gameObject.SetActive(false);

        LevelEvents.WaveCountdown += OnCountdown;
        LevelEvents.WavePrepStarted += OnPrepStarted;
        LevelEvents.WaveStarted += OnWaveStarted;
        LevelEvents.WaveCleared += OnWaveCleared;
        LevelEvents.EnemiesChanged += OnEnemiesChanged;
        LevelEvents.LevelFinished += OnLevelFinished;
        if (placer != null) placer.OnPlacementRejected += OnPlacementRejected;
        if (nextWaveButton != null) nextWaveButton.onClick.AddListener(OnNextWaveClicked);

        SeedFromSpawner();
    }

    void OnDestroy()
    {
        LevelEvents.WaveCountdown -= OnCountdown;
        LevelEvents.WavePrepStarted -= OnPrepStarted;
        LevelEvents.WaveStarted -= OnWaveStarted;
        LevelEvents.WaveCleared -= OnWaveCleared;
        LevelEvents.EnemiesChanged -= OnEnemiesChanged;
        LevelEvents.LevelFinished -= OnLevelFinished;
        if (placer != null) placer.OnPlacementRejected -= OnPlacementRejected;
    }

    /// <summary>Los primeros eventos salen antes de que exista el HUD: se lee el estado actual del spawner.</summary>
    void SeedFromSpawner()
    {
        var sp = lc != null ? lc.Spawner : null;
        totalWaves = sp != null ? sp.TotalWaves : (lc != null && lc.Level != null ? lc.Level.WaveCount() : 0);
        shownWave = sp != null ? sp.WaveIndex : 0;
        SetWaveLine();
        if (sp != null)
        {
            OnEnemiesChanged(sp.EnemiesAlive, sp.PendingToSpawn);
            if (sp.InPrep)
            {
                if (lc.Level != null && lc.Level.waves.Count > 0) prepBonus = lc.Level.waves[0].earlyCallBonus;
                OnCountdown(sp.PrepRemaining, Mathf.Max(sp.PrepRemaining, 0.01f));
                if (summaryText != null && lc.Level != null && lc.Level.waves.Count > 0)
                    summaryText.text = "Se prepara: " + lc.Level.waves[0].count + " × " + EnemyNames.Of(lc.Level.waves[0].enemyType);
            }
            else SetCountdownIdle();
        }
        else SetCountdownIdle();
    }

    // ───────────────────────── eventos ─────────────────────────

    void OnPrepStarted(int index, int total, string summary)
    {
        totalWaves = total;
        shownWave = Mathf.Max(0, index - 1);
        SetWaveLine();
        if (summaryText != null) summaryText.text = "Se prepara: " + summary;
        prepBonus = 0;
        if (lc != null && lc.Level != null)
        {
            int gi = 0;
            for (int i = 0; i < lc.Level.waves.Count; i++)
            {
                if (i == 0 || !lc.Level.waves[i].joinPrevious) gi++;
                if (gi == index) { prepBonus = lc.Level.waves[i].earlyCallBonus; break; }
            }
        }
        shownBonus = -1;
    }

    void OnWaveStarted(int index, int total, string summary)
    {
        shownWave = index;
        totalWaves = total;
        inPrep = false;
        SetWaveLine();
        if (summaryText != null) summaryText.text = summary;
        SetCountdownIdle();
    }

    void OnCountdown(float remaining, float total)
    {
        if (finished) return;
        if (remaining <= 0f)
        {
            inPrep = false;
            SetCountdownIdle();
            return;
        }
        inPrep = true;
        float f = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        if (countdownFill != null) countdownFill.fillAmount = f;
        if (countdownText != null) countdownText.text = "Próxima oleada en " + remaining.ToString("0.0") + " s";

        int bonusNow = prepBonus > 0 ? Mathf.RoundToInt(prepBonus * f) : 0;
        if (bonusNow != shownBonus)
        {
            shownBonus = bonusNow;
            if (nextWaveLabel != null)
                nextWaveLabel.text = bonusNow > 0 ? "¡Siguiente oleada!  +" + bonusNow + " oro" : "¡Siguiente oleada!";
        }
        if (nextWaveButton != null)
        {
            if (!nextWaveButton.gameObject.activeSelf) nextWaveButton.gameObject.SetActive(true);
            nextWaveButton.interactable = true;
        }
    }

    void SetCountdownIdle()
    {
        if (countdownFill != null) countdownFill.fillAmount = 0f;
        if (countdownText != null) countdownText.text = finished ? string.Empty : "Oleada en curso";
        shownBonus = -1;
        if (nextWaveButton != null)
        {
            nextWaveButton.interactable = false;
            nextWaveButton.gameObject.SetActive(false);
        }
    }

    void SetWaveLine()
    {
        if (waveText == null) return;
        int t = Mathf.Max(totalWaves, shownWave);
        waveText.text = "Oleada " + shownWave + " / " + t;
    }

    void OnWaveCleared(int index, int bonus)
    {
        if (bonus > 0) Toast("Oleada limpia  +" + bonus + " oro", Gold);
    }

    void OnEnemiesChanged(int alive, int pending)
    {
        if (enemiesText == null) return;
        enemiesText.text = "Enemigos: vivos " + Mathf.Max(0, alive) + " · por venir " + Mathf.Max(0, pending);
    }

    void OnLevelFinished(LevelDefinition level, bool won)
    {
        finished = true;
        inPrep = false;
        if (waveText != null) waveText.text = won ? "¡Nivel superado!" : "Nivel perdido";
        if (summaryText != null) summaryText.text = string.Empty;
        SetCountdownIdle();
    }

    void OnPlacementRejected(Vector3 position)
    {
        int cost = placer != null ? placer.SelectedCost : 0;
        if (cost <= 0 && placer != null && placer.SelectedData != null) cost = placer.SelectedData.cost;
        int money = GameManager.I != null ? GameManager.I.Money : int.MaxValue;
        if (money < cost) Toast("Sin oro suficiente", Danger);
        else Toast("No se puede construir ahí", Danger);
        ProceduralAudio.Play(Sfx.Reject);
    }

    void OnNextWaveClicked()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        CallNextWave();
    }

    void CallNextWave()
    {
        if (lc == null || lc.Spawner == null || lc.IsPaused || finished) return;
        if (lc.Spawner.CallNextWaveEarly()) ProceduralAudio.Play(Sfx.Click);
    }

    // ───────────────────────── update ─────────────────────────

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        UpdateBanner(dt);
        UpdateToasts(dt);

        if (!finished && lc != null && !lc.IsPaused && Input.GetKeyDown(KeyCode.Space))
            CallNextWave();

        if (inPrep && lc != null && lc.Spawner != null && !lc.Spawner.InPrep && !finished)
        {
            inPrep = false;
            SetCountdownIdle();
        }
    }

    void UpdateBanner(float dt)
    {
        if (bannerDone || levelBanner == null) return;
        bannerTime += dt;
        float a = 1f;
        if (bannerTime > bannerHold) a = 1f - Mathf.Clamp01((bannerTime - bannerHold) / bannerFade);
        levelBanner.alpha = a;
        if (bannerTime >= bannerHold + bannerFade)
        {
            bannerDone = true;
            levelBanner.gameObject.SetActive(false);
        }
    }

    // ───────────────────────── toasts ─────────────────────────

    public void Toast(string message, Color color)
    {
        if (toastRoot == null || toastTemplate == null || string.IsNullOrEmpty(message)) return;

        var txt = Instantiate(toastTemplate, toastRoot);
        txt.gameObject.SetActive(true);
        txt.text = message;
        txt.color = color;
        var rect = txt.rectTransform;
        rect.anchoredPosition = Vector2.zero;

        var g = txt.GetComponent<CanvasGroup>();
        if (g == null) g = txt.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = false;
        g.interactable = false;

        var item = new ToastItem();
        item.rect = rect;
        item.group = g;
        item.t = 0f;
        toasts.Insert(0, item);

        while (toasts.Count > 5)
        {
            var old = toasts[toasts.Count - 1];
            toasts.RemoveAt(toasts.Count - 1);
            if (old.rect != null) Destroy(old.rect.gameObject);
        }
    }

    void UpdateToasts(float dt)
    {
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var it = toasts[i];
            if (it.rect == null) { toasts.RemoveAt(i); continue; }
            it.t += dt;

            float a;
            if (it.t < 0.15f) a = it.t / 0.15f;
            else if (it.t > toastLife - 0.5f) a = Mathf.Clamp01((toastLife - it.t) / 0.5f);
            else a = 1f;
            it.group.alpha = a;

            float targetY = i * (toastHeight + 6f);
            Vector2 p = it.rect.anchoredPosition;
            p.y = Mathf.Lerp(p.y, targetY, 1f - Mathf.Exp(-14f * dt));
            it.rect.anchoredPosition = p;

            if (it.t >= toastLife)
            {
                toasts.RemoveAt(i);
                Destroy(it.rect.gameObject);
            }
        }
    }
}
