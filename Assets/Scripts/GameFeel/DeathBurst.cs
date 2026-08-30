using UnityEngine;

/// <summary>
/// Muerte de enemigo: ráfaga de cubitos con el tinte del enemigo (6–10; 14 si es grande, vida ≥ 300)
/// + sonido. Fuga por la base: anillo rojo que se expande en el suelo + Sfx.EnemyLeak.
/// </summary>
public class DeathBurst : MonoBehaviour
{
    public const int BigEnemyHealth = 300;

    FeelParticles particles;
    FeelRings rings;

    void Awake()
    {
        particles = GetComponent<FeelParticles>();
        if (particles == null) particles = gameObject.AddComponent<FeelParticles>();
        rings = GetComponent<FeelRings>();
        if (rings == null) rings = gameObject.AddComponent<FeelRings>();
    }

    void OnEnable()
    {
        CombatEvents.EnemyDied += OnEnemyDied;
        CombatEvents.EnemyReachedEnd += OnEnemyReachedEnd;
    }

    void OnDisable()
    {
        CombatEvents.EnemyDied -= OnEnemyDied;
        CombatEvents.EnemyReachedEnd -= OnEnemyReachedEnd;
    }

    void OnEnemyDied(EnemyTD enemy)
    {
        if (enemy == null) return;

        Vector3 pos = enemy.transform.position;
        var data = enemy.data;
        Color tint = data != null ? data.tint : Color.white;
        bool big = data != null && data.maxHealth >= BigEnemyHealth;
        int bounty = data != null ? data.bounty : 0;

        if (particles != null)
        {
            if (big)
                particles.Burst(pos, tint, 14, 0.2f, 0.4f, 2f, 4.5f, 2.5f, 6f, 0.7f, -9.8f);
            else
                particles.Burst(pos, tint, Random.Range(6, 11), 0.15f, 0.3f, 1.5f, 3.5f, 2f, 5f, 0.6f, -9.8f);
        }

        ProceduralAudio.Play(big ? Sfx.EnemyDeathBig : Sfx.EnemyDeath, big ? 0.8f : 0.55f);
        if (bounty > 0) ProceduralAudio.Play(Sfx.Coin, 0.3f);
    }

    void OnEnemyReachedEnd(EnemyTD enemy)
    {
        if (enemy == null) return;

        Vector3 pos = enemy.transform.position;
        if (rings != null)
            rings.Spawn(new Vector3(pos.x, 0.06f, pos.z), new Color(1f, 0.2f, 0.15f, 0.85f), 0.3f, 1.9f, 0.4f);

        ProceduralAudio.Play(Sfx.EnemyLeak, 0.8f);
    }
}
