using UnityEngine;

public class EnemyTD : MonoBehaviour
{
    [Header("Datos compartidos (Flyweight)")]
    public EnemyData data;   // 👉 referencia al ScriptableObject

    [Header("Estado de runtime (extrínseco)")]
    public int currentHealth;   // vida actual de ESTA instancia
    public bool testAutoKill = false;

    void Start()
    {
        if (data == null)
        {
            Debug.LogError("[EnemyTD] No hay EnemyData asignado en " + name, this);
            currentHealth = 1; // valor de seguridad
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
        // --- ABB: remover de prioridad ---
        var prog = GetComponent<EnemyProgress>();
        EnemyPriorityABB.Instance?.Remove(prog);

        // Recompensas desde el Flyweight
        if (data != null)
        {
            GameManager.I.AddMoney(data.bounty);
            GameManager.I.AddScore(data.scoreReward);
        }
        else
        {
            GameManager.I.AddMoney(1);
            GameManager.I.AddScore(1);
        }

        GameEvents.RaiseEnemyRemoved();   // <<< Observer
        Destroy(gameObject);
    }

    public void ReachEnd()
    {
        // --- ABB: remover de prioridad ---
        var prog = GetComponent<EnemyProgress>();
        EnemyPriorityABB.Instance?.Remove(prog);

        int dmgBase = data != null ? data.damageToBase : 1;
        GameManager.I.LoseLife(dmgBase);

        GameEvents.RaiseEnemyRemoved();   // <<< Observer
        Destroy(gameObject);
    }
}
