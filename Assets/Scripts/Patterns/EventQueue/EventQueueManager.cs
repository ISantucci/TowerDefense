using UnityEngine;

public class EventQueueManager : MonoBehaviour
{
    public static EventQueueManager I { get; private set; }

    QueueTF<GameplayEvent> queue = new QueueTF<GameplayEvent>();

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        queue.InitializeFromArray(null); // arranca vacía
    }

    public static void Enqueue(GameplayEvent e)
    {
        if (I == null)
        {
            Debug.LogWarning("[EventQueueManager] No instance, evento ignorado.");
            return;
        }

        I.queue.Enqueue(e);
    }

    void Update()
    {
        while (!queue.IsEmpty())
        {
            var evt = queue.Dequeue();
            ProcessEvent(evt);
        }
    }

    void ProcessEvent(GameplayEvent evt)
    {
        switch (evt.type)
        {
            case GameplayEventType.EnemyDied:
                if (GameManager.I != null)
                {
                    if (evt.intParam1 != 0)
                        GameManager.I.AddMoney(evt.intParam1);
                    if (evt.intParam2 != 0)
                        GameManager.I.AddScore(evt.intParam2);
                }
                GameEvents.RaiseEnemyRemoved();
                break;

            case GameplayEventType.LifeLost:
                if (GameManager.I != null)
                    GameManager.I.LoseLife(evt.intParam1);
                break;

            case GameplayEventType.WaveStarted:
                // intParam1 = wave actual, intParam2 = total waves
                GameEvents.RaiseWaveChanged(evt.intParam1, evt.intParam2);
                break;

            case GameplayEventType.LevelWon:
                GameEvents.RaiseLevelWon();
                break;

            case GameplayEventType.LevelLost:
                GameEvents.RaiseLevelLost();
                break;

            case GameplayEventType.TowerBuilt:
            case GameplayEventType.WaveEnded:
            default:
                break;
        }
    }
}
