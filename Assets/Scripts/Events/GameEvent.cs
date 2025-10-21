using System;

public static class GameEvents
{
    
    public static event Action EnemySpawned;
    public static event Action EnemyRemoved;
    public static void RaiseEnemySpawned() => EnemySpawned?.Invoke();
    public static void RaiseEnemyRemoved() => EnemyRemoved?.Invoke();

    
    public static event Action<int> LivesChanged;
    public static event Action<int> MoneyChanged;
    public static event Action<int> ScoreChanged;
    public static event Action<int, int> WaveChanged; 
    public static event Action LevelWon;
    public static event Action LevelLost;

    public static void RaiseLivesChanged(int v) => LivesChanged?.Invoke(v);
    public static void RaiseMoneyChanged(int v) => MoneyChanged?.Invoke(v);
    public static void RaiseScoreChanged(int v) => ScoreChanged?.Invoke(v);
    public static void RaiseWaveChanged(int idx, int total) => WaveChanged?.Invoke(idx, total);
    public static void RaiseLevelWon() => LevelWon?.Invoke();
    public static void RaiseLevelLost() => LevelLost?.Invoke();
}
