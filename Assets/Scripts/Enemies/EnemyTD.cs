using UnityEngine;

public class EnemyTD : MonoBehaviour
{
    public int maxHealth = 12;
    public int bounty = 5;
    public int damageToBase = 1;
    public bool testAutoKill = false;

    int hp;

    void Start()
    {
        hp = maxHealth;
        if (testAutoKill) Invoke(nameof(_TestKill), 0.8f);
    }
    void _TestKill() => TakeDamage(999);

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (hp <= 0) Die();
    }

    void Die()
    {
        GameManager.I.AddMoney(bounty);
        GameManager.I.AddScore(1);
        GameEvents.RaiseEnemyRemoved();   // <<< Observer
        Destroy(gameObject);
    }

    public void ReachEnd()
    {
        GameManager.I.LoseLife(damageToBase);
        GameEvents.RaiseEnemyRemoved();   // <<< Observer
        Destroy(gameObject);
    }
}
