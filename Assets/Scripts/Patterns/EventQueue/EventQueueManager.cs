using UnityEngine;

public class EventQueueManager : MonoBehaviour
{
    public static EventQueueManager I { get; private set; }

    QueueTF<GameplayEvent> queue = new QueueTF<GameplayEvent>();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        queue.InitializeFromArray(null);
    }

    public static void Enqueue(GameplayEvent e)
    {
        if (I == null) return;
        I.queue.Enqueue(e);
    }

    void Update()
    {
        while (!queue.IsEmpty())
            ProcessEvent(queue.Dequeue());
    }

    void ProcessEvent(GameplayEvent evt)
    {
        if (GameManager.I == null) return;

        switch (evt.type)
        {
            case GameplayEventType.SetStats:
                GameManager.I.SetMoneyLivesScore(evt.intParam1, evt.intParam2, evt.intParam3);
                GameEvents.RaiseMoneyChanged(GameManager.I.Money);
                GameEvents.RaiseLivesChanged(GameManager.I.Lives);
                GameEvents.RaiseScoreChanged(GameManager.I.Score);
                break;

            case GameplayEventType.AddMoney:
                GameManager.I.AddMoney(evt.intParam1);
                GameEvents.RaiseMoneyChanged(GameManager.I.Money);
                break;

            case GameplayEventType.AddScore:
                GameManager.I.AddScore(evt.intParam1);
                GameEvents.RaiseScoreChanged(GameManager.I.Score);
                break;

            case GameplayEventType.LifeLost:
                {
                    int dmg = evt.intParam1 <= 0 ? 1 : evt.intParam1;
                    bool lost = GameManager.I.LoseLife(dmg);
                    GameEvents.RaiseLivesChanged(GameManager.I.Lives);
                    if (lost) Enqueue(new GameplayEvent(GameplayEventType.LevelLost));
                }
                break;

            case GameplayEventType.EnemyRemoved:
                GameEvents.RaiseEnemyRemoved();
                break;

            case GameplayEventType.WaveChanged:
                GameEvents.RaiseWaveChanged(evt.intParam1, evt.intParam2);
                break;

            case GameplayEventType.LevelWon:
                GameEvents.RaiseLevelWon();
                break;

            case GameplayEventType.LevelLost:
                GameEvents.RaiseLevelLost();
                break;
        }
    }
}
