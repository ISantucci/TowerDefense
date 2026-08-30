using UnityEngine;

/// <summary>
/// Feedback de construcción y flujo del nivel:
///  - TowerPlaced → Sfx.Build + polvo (8 cubitos blanco/gris subiendo desde la base).
///  - TowerSold → Sfx.Sell + monedas (cubitos dorados saltando).
///  - TowerUpgraded → Sfx.Upgrade + pulso de anillo amarillo alrededor de la torre.
///  - WaveStarted → Sfx.WaveStart; WaveCleared → Sfx.WaveClear (sólo si hay bonus o no era la última).
///  - LevelFinished → Sfx.Win / Sfx.Lose (en un objeto que sobrevive al cambio de escena).
/// GameEvents.MoneyChanged no suena.
/// </summary>
public class BuildFeedback : MonoBehaviour
{
    static readonly Color DustA = new Color(0.95f, 0.95f, 0.92f);
    static readonly Color DustB = new Color(0.6f, 0.6f, 0.62f);
    static readonly Color CoinA = new Color(1f, 0.85f, 0.2f);
    static readonly Color CoinB = new Color(1f, 0.7f, 0.1f);
    static readonly Color UpgradeRing = new Color(1f, 0.9f, 0.25f, 0.8f);

    FeelParticles particles;
    FeelRings rings;
    int lastWaveTotal;

    void Awake()
    {
        particles = GetComponent<FeelParticles>();
        if (particles == null) particles = gameObject.AddComponent<FeelParticles>();
        rings = GetComponent<FeelRings>();
        if (rings == null) rings = gameObject.AddComponent<FeelRings>();
    }

    void OnEnable()
    {
        CombatEvents.TowerPlaced += OnTowerPlaced;
        CombatEvents.TowerSold += OnTowerSold;
        CombatEvents.TowerUpgraded += OnTowerUpgraded;
        LevelEvents.WavePrepStarted += OnWavePrepStarted;
        LevelEvents.WaveStarted += OnWaveStarted;
        LevelEvents.WaveCleared += OnWaveCleared;
        LevelEvents.LevelFinished += OnLevelFinished;
    }

    void OnDisable()
    {
        CombatEvents.TowerPlaced -= OnTowerPlaced;
        CombatEvents.TowerSold -= OnTowerSold;
        CombatEvents.TowerUpgraded -= OnTowerUpgraded;
        LevelEvents.WavePrepStarted -= OnWavePrepStarted;
        LevelEvents.WaveStarted -= OnWaveStarted;
        LevelEvents.WaveCleared -= OnWaveCleared;
        LevelEvents.LevelFinished -= OnLevelFinished;
    }

    static Vector3 BasePosition(Tower tower)
    {
        Vector3 p = tower.transform.position;
        return new Vector3(p.x, 0.06f, p.z);
    }

    void OnTowerPlaced(Tower tower)
    {
        ProceduralAudio.Play(Sfx.Build, 0.8f);
        if (tower == null || particles == null) return;
        Vector3 pos = BasePosition(tower);
        particles.Burst(pos + Vector3.up * 0.1f, DustA, DustB, 8, 0.12f, 0.22f, 0.6f, 1.6f, 1.2f, 2.6f, 0.75f, -3f);
    }

    void OnTowerSold(Tower tower)
    {
        ProceduralAudio.Play(Sfx.Sell, 0.8f);
        if (tower == null || particles == null) return;
        Vector3 pos = tower.transform.position + Vector3.up * 0.8f;
        particles.Burst(pos, CoinA, CoinB, 7, 0.12f, 0.2f, 0.8f, 2f, 3f, 5f, 0.8f, -9.8f);
    }

    void OnTowerUpgraded(Tower tower, UpgradeStat stat)
    {
        ProceduralAudio.Play(Sfx.Upgrade, 0.8f);
        if (tower == null || rings == null) return;
        rings.Spawn(BasePosition(tower), UpgradeRing, 0.4f, 1.7f, 0.35f);
    }

    void OnWavePrepStarted(int index, int total, string summary)
    {
        if (total > 0) lastWaveTotal = total;
    }

    void OnWaveStarted(int index, int total, string summary)
    {
        if (total > 0) lastWaveTotal = total;
        ProceduralAudio.Play(Sfx.WaveStart, 0.85f);
    }

    void OnWaveCleared(int index, int bonus)
    {
        int total = lastWaveTotal;
        if (total <= 0)
        {
            var rt = LevelController.Current;
            if (rt != null && rt.Level != null) total = rt.Level.WaveCount();
        }
        // La última oleada coincide con LevelFinished(won): ahí suena Win, no WaveClear.
        if (bonus > 0 || index < total)
            ProceduralAudio.Play(Sfx.WaveClear, 0.8f);
    }

    void OnLevelFinished(LevelDefinition level, bool won)
    {
        // La escena cambia enseguida: el sonido va en un objeto DontDestroyOnLoad que se autodestruye.
        ProceduralAudio.PlayDetached(won ? Sfx.Win : Sfx.Lose, 0.9f);
    }
}
