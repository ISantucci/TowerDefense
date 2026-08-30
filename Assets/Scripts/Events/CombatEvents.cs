using System;
using UnityEngine;

/// <summary>
/// Eventos de combate para feedback (audio, números de daño, rayos, partículas).
/// Los sistemas de juego los emiten; el game feel se suscribe. Nadie del gameplay depende de ellos.
/// </summary>
public static class CombatEvents
{
    public static event Action<EnemyTD, int, Vector3> DamageDealt;
    public static event Action<EnemyTD> EnemyDied;
    public static event Action<EnemyTD> EnemyReachedEnd;
    public static event Action<EnemyTD> EnemySpawned;
    public static event Action<Tower, EnemyTD> TowerFired;
    public static event Action<Tower, EnemyTD, float> BeamTick;
    public static event Action<Vector3, Vector3> ChainJump;
    public static event Action<Tower, EnemyTD> PushHit;
    public static event Action<Tower> TowerPlaced;
    public static event Action<Tower> TowerSold;
    public static event Action<Tower, UpgradeStat> TowerUpgraded;

    public static void RaiseDamageDealt(EnemyTD e, int amount, Vector3 pos) { var h = DamageDealt; if (h != null) h(e, amount, pos); }
    public static void RaiseEnemyDied(EnemyTD e) { var h = EnemyDied; if (h != null) h(e); }
    public static void RaiseEnemyReachedEnd(EnemyTD e) { var h = EnemyReachedEnd; if (h != null) h(e); }
    public static void RaiseEnemySpawned(EnemyTD e) { var h = EnemySpawned; if (h != null) h(e); }
    public static void RaiseTowerFired(Tower t, EnemyTD e) { var h = TowerFired; if (h != null) h(t, e); }
    public static void RaiseBeamTick(Tower t, EnemyTD e, float ramp01) { var h = BeamTick; if (h != null) h(t, e, ramp01); }
    public static void RaiseChainJump(Vector3 from, Vector3 to) { var h = ChainJump; if (h != null) h(from, to); }
    public static void RaisePushHit(Tower t, EnemyTD e) { var h = PushHit; if (h != null) h(t, e); }
    public static void RaiseTowerPlaced(Tower t) { var h = TowerPlaced; if (h != null) h(t); }
    public static void RaiseTowerSold(Tower t) { var h = TowerSold; if (h != null) h(t); }
    public static void RaiseTowerUpgraded(Tower t, UpgradeStat s) { var h = TowerUpgraded; if (h != null) h(t, s); }
}

/// <summary>Eventos del flujo del nivel (oleadas, preparación, cierre). Complementan GameEvents sin tocarlo.</summary>
public static class LevelEvents
{
    /// <summary>remaining, total — se emite cada frame durante la preparación de una oleada.</summary>
    public static event Action<float, float> WaveCountdown;
    /// <summary>index (1-based) de la oleada que se prepara, total, resumen ("8 × Goblin, 2 × Globo").</summary>
    public static event Action<int, int, string> WavePrepStarted;
    /// <summary>index (1-based), total, resumen de enemigos.</summary>
    public static event Action<int, int, string> WaveStarted;
    public static event Action<int, int> WaveCleared;   // index, bonus
    public static event Action<int, int> EnemiesChanged; // vivos, por spawnear
    public static event Action<LevelDefinition> LevelStarted;
    public static event Action<LevelDefinition, bool> LevelFinished; // level, won

    public static void RaiseWaveCountdown(float remaining, float total) { var h = WaveCountdown; if (h != null) h(remaining, total); }
    public static void RaiseWavePrepStarted(int index, int total, string summary) { var h = WavePrepStarted; if (h != null) h(index, total, summary); }
    public static void RaiseWaveStarted(int index, int total, string enemyName) { var h = WaveStarted; if (h != null) h(index, total, enemyName); }
    public static void RaiseWaveCleared(int index, int bonus) { var h = WaveCleared; if (h != null) h(index, bonus); }
    public static void RaiseEnemiesChanged(int alive, int pending) { var h = EnemiesChanged; if (h != null) h(alive, pending); }
    public static void RaiseLevelStarted(LevelDefinition l) { var h = LevelStarted; if (h != null) h(l); }
    public static void RaiseLevelFinished(LevelDefinition l, bool won) { var h = LevelFinished; if (h != null) h(l, won); }
}
