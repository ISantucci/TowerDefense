public enum GameplayEventType
{
    SetStats,
    AddMoney,
    AddScore,
    LifeLost,
    EnemyRemoved,
    WaveChanged,
    LevelWon,
    LevelLost,
    WaveStarted,
    TowerBuilt,
}

public struct GameplayEvent
{

    public static GameplayEvent AddMoney(int amount)
    {
        return new GameplayEvent(GameplayEventType.AddMoney, amount, 0, 0);
    }

    public static GameplayEvent AddScore(int amount)
    {
        return new GameplayEvent(GameplayEventType.AddScore, amount, 0, 0);
    }

    public static GameplayEvent LifeLost(int dmg)
    {
        return new GameplayEvent(GameplayEventType.LifeLost, dmg, 0, 0);
    }


    public GameplayEventType type;
    public int intParam1;
    public int intParam2;
    public int intParam3;

    public GameplayEvent(GameplayEventType type, int p1 = 0, int p2 = 0, int p3 = 0)
    {
        this.type = type;
        intParam1 = p1;
        intParam2 = p2;
        intParam3 = p3;
    }
}
