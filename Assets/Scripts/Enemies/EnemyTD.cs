using UnityEngine;

public class EnemyTD : MonoBehaviour
{
    public EnemyData data;
    public int currentHealth;

    private static int _nextId = 1;
    public int uniqueId { get; private set; }

    /// <summary>Aéreo según su EnemyData (false si no hay data).</summary>
    public bool IsFlying => data != null && data.isFlying;

    public int MaxHealth => data != null ? Mathf.Max(1, data.maxHealth) : 1;
    public float HealthFraction => Mathf.Clamp01((float)currentHealth / MaxHealth);
    public bool IsDead { get; private set; }

    bool healthInitialized;

    void Awake()
    {
        uniqueId = _nextId++;
    }

    void Start()
    {
        if (!healthInitialized) InitHealth();
    }

    /// <summary>La fábrica lo llama después de asignar data (antes de Start) para que la vida sea la del tipo correcto.</summary>
    public void InitHealth()
    {
        currentHealth = data != null ? Mathf.Max(1, data.maxHealth) : 1;
        healthInitialized = true;
    }

    public void TakeDamage(int dmg)
    {
        if (IsDead) return;

        // Armadura: reducción porcentual, nunca baja de 1 de daño.
        float armor = data != null ? Mathf.Clamp(data.armor, 0f, 0.9f) : 0f;
        dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - armor)));

        currentHealth -= dmg;
        CombatEvents.RaiseDamageDealt(this, dmg, transform.position);
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        var prog = GetComponent<EnemyProgress>();
        EnemyPriorityABB.Instance?.Remove(prog);

        int bounty = data != null ? data.bounty : 0;
        int score = data != null ? Mathf.Max(1, data.scoreReward) : 1;

        if (bounty != 0)
            EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.AddMoney, bounty));

        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.AddScore, score));
        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.EnemyRemoved));

        CombatEvents.RaiseEnemyDied(this);
        Destroy(gameObject);
    }

    public void ReachEnd()
    {
        if (IsDead) return;
        IsDead = true;

        var prog = GetComponent<EnemyProgress>();
        EnemyPriorityABB.Instance?.Remove(prog);

        int dmgBase = (data != null && data.damageToBase > 0) ? data.damageToBase : 1;

        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.LifeLost, dmgBase));
        EventQueueManager.Enqueue(new GameplayEvent(GameplayEventType.EnemyRemoved));

        CombatEvents.RaiseEnemyReachedEnd(this);
        Destroy(gameObject);
    }
}
