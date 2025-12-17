using UnityEngine;

public class EnemyTD : MonoBehaviour
{
    public EnemyData data;
    public int currentHealth;

    private static int _nextId = 1;
    public int uniqueId { get; private set; }

    void Awake()
    {
        uniqueId = _nextId++;
    }


    void Start()
    {
        currentHealth = data != null ? data.maxHealth : 1;
    }

    public void TakeDamage(int dmg)
    {
        currentHealth -= dmg;
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        var prog = GetComponent<EnemyProgress>();
        EnemyPriorityABB.Instance?.Remove(prog);

        int bounty = data != null ? data.bounty : 0;

        if (bounty != 0)
            EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.AddMoney, bounty));

        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.AddScore, 1));
        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.EnemyRemoved));

        Destroy(gameObject);
    }

    public void ReachEnd()
    {
        var prog = GetComponent<EnemyProgress>();
        EnemyPriorityABB.Instance?.Remove(prog);

        int dmgBase = (data != null && data.damageToBase > 0) ? data.damageToBase : 1;

        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.LifeLost, dmgBase));
        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.EnemyRemoved));

        Destroy(gameObject);
    }
}
