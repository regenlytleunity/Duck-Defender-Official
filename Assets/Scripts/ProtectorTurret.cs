using UnityEngine;

/// <summary>
/// Protector turret: shockwave around player that knocks enemies back. No damage.
/// 1.4.11: No functional changes.
/// </summary>
public class ProtectorTurret : TurretBase
{
    public override TurretSlotType TurretType => TurretSlotType.Protector;

    [Header("Protector Settings")]
    public GameObject ShockwaveEffectPrefab;
    public string ShockwaveSoundName = "Shockwave_Ground_Impact";
    public bool CenterOnPlayer = true;

    protected override float GetCurrentInterval()
    {
        if (PlayerStats.Instance == null) return 99f;
        return Mathf.Max(0.5f, PlayerStats.Instance.ProtectorInterval);
    }

    protected override void OnTick()
    {
        if (PlayerStats.Instance == null) return;

        Vector3 center = CenterOnPlayer && PlayerTransform != null
            ? PlayerTransform.position
            : transform.position;

        float radius = Mathf.Max(0.1f, PlayerStats.Instance.ProtectorRadius);
        float knockbackForce = Mathf.Max(0.1f, PlayerStats.Instance.ProtectorKnockback);

        if (ShockwaveEffectPrefab != null)
        {
            GameObject fx = Instantiate(ShockwaveEffectPrefab, center, Quaternion.identity);
            fx.transform.localScale = Vector3.one * (radius / 3.0f);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null) continue;

            Vector2 awayDir = ((Vector2)enemy.transform.position - (Vector2)center).normalized;
            if (awayDir.sqrMagnitude < 0.001f)
            {
                awayDir = new Vector2(Random.value > 0.5f ? 1f : -1f, 0.2f).normalized;
            }

            enemy.ApplyKnockback(awayDir * knockbackForce);
        }

        if (AudioManager.Instance != null && !string.IsNullOrEmpty(ShockwaveSoundName))
        {
            AudioManager.Instance.PlaySFX(ShockwaveSoundName);
        }
    }
}