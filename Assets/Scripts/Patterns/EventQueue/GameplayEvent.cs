public enum GameplayEventType
{
    EnemyDied,
    TowerBuilt,
    WaveStarted,
    WaveEnded,
    LifeLost,
    LevelWon,
    LevelLost
}

public struct GameplayEvent
{
    public GameplayEventType type;
    public int intParam1;
    public int intParam2;
    public float floatParam1;

    public GameplayEvent(GameplayEventType type, int intParam1 = 0, int intParam2 = 0, float floatParam1 = 0f)
    {
        this.type = type;
        this.intParam1 = intParam1;
        this.intParam2 = intParam2;
        this.floatParam1 = floatParam1;
    }
}
