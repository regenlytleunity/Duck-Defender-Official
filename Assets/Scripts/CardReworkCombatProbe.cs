#if UNITY_EDITOR
using UnityEngine;

// A transient target for isolated combat checks. Never saved into a prefab or scene.
[AddComponentMenu("")]
public class CardReworkCombatProbe : EnemyBase
{
    public bool DisableOnDamage;
    public void SetHealth(float health) { CurrentHealth = health; }
    protected override void Move() { }
    public override void TakeDamage(int damage)
    {
        CurrentHealth -= damage;
        if (DisableOnDamage)
        {
            ActiveEnemies.Remove(this);
            gameObject.SetActive(false);
        }
    }
}
#endif
