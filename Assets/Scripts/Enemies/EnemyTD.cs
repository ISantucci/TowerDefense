using UnityEngine;

public class EnemyTD : MonoBehaviour
{
    [Header("Datos compartidos (Flyweight)")]
    public EnemyData data;

    [Header("Estado de runtime (extrínseco)")]
    public int currentHealth;
    public bool testAutoKill = false;

    public static int nextId = 0;
    public int uniqueId;

    void Awake()
    {
        uniqueId = nextId++;
    }

    void Start()
    {
        if (data == null)
        {
            Debug.LogError("[EnemyTD] No hay EnemyData asignado en " + name, this);
            currentHealth = 1;
        }
        else
        {
            currentHealth = data.maxHealth;
        }

        if (testAutoKill)
            Invoke(nameof(_TestKill), 0.8f);
    }

    void _TestKill() => TakeDamage(999);

    public void TakeDamage(int dmg)
    {
        currentHealth -= dmg;
        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        var prog = GetComponent<EnemyProgress>();
        EnemyPriorityABB.Instance?.Remove(prog);

        int bounty = 1;
        int score = 1;

        if (data != null)
        {
            bounty = data.bounty;
            score = data.scoreReward;
        }

        // 👉 Toda la consecuencia de la muerte viaja por la EventQueue
        EventQueueManager.Enqueue(
            new GameplayEvent(GameplayEventType.EnemyDied, bounty, score)
        );

        Destroy(gameObject);
    }

    public void ReachEnd()
    {
        var prog = GetComponent<EnemyProgress>();
        EnemyPriorityABB.Instance?.Remove(prog);

        int dmgBase = data != null ? data.damageToBase : 1;

        // 👉 Perder vida via EventQueue
        EventQueueManager.Enqueue(
            new GameplayEvent(GameplayEventType.LifeLost, dmgBase)
        );

        // 👉 También cuenta como “enemigo removido” para el spawner,
        // pero sin recompensa de dinero/score.
        EventQueueManager.Enqueue(
            new GameplayEvent(GameplayEventType.EnemyDied, 0, 0)
        );

        Destroy(gameObject);
    }
}
