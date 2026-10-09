using System.Collections;
using UnityEngine;

public class BuzzerEnemy : EnemyBase
{
    [Header("Hover Settings")]
    public float HoverHeight = 3;
    public float HorizontalOffset = 4;
    public float MoveSmoothing = 1;
    [Header("Combat")]
    public float BaseShootRate = 1;
    [HideInInspector] public float MinShootRate = .5f;
    [HideInInspector] public float ShootRateScaling = .05f;
    public float ShootRange = 8;
    public float ShotWindup = .35f;
    [HideInInspector] public float InaccuracyAngle;
    [Header("Legacy Retreat Settings (unused)")]
    public float RetreatInterval = 6;
    public float RetreatDuration = 2;
    public float RetreatHeightBonus = 3;
    public float RetreatDistanceBonus = 4;
    [Header("Swarm Separation")]
    public float SeparationRadius = 1.5f;
    public float SeparationForce = 5;
    [Header("Elite")]
    public float SlowDuration = 2;
    public float CrashDuration = 3;
    public float CrashDrift = 1.5f;
    public float CrashExplosionRadius = 1.5f;
    public LayerMask GroundLayer;
    public GameObject CrashExplosionPrefab;

    float _nextShootTime, _windupElapsed;
    bool _windingUp;
    Vector2 _aimPosition;
    FlyingEnemyAnimator _animator;

    public override void Initialize(float wave)
    {
        base.Initialize(wave);
        if (Rb != null) Rb.gravityScale = 0;
        _animator = GetComponent<FlyingEnemyAnimator>();
        _nextShootTime = Time.time + .5f;
    }

    protected override void Move()
    {
        if (PlayerTarget == null || Rb == null) return;
        Rb.linearVelocity = Vector2.zero;
        if (_windingUp)
        {
            _windupElapsed += Time.fixedDeltaTime;
            if (_windupElapsed >= ShotWindup)
            {
                Fire();
                _windingUp = false;
                _nextShootTime = Time.time + Mathf.Max(.1f, BaseShootRate);
            }
            return;
        }
        float side = transform.position.x >= PlayerTarget.position.x ? 1 : -1;
        Vector2 target = (Vector2)PlayerTarget.position + new Vector2(side * HorizontalOffset, HoverHeight);
        Vector2 offset = target - (Vector2)transform.position;
        float speed = CurrentSpeed * PlayerStats.ProjectileSpeedFactor(transform.position);
        Rb.linearVelocity = offset.normalized * Mathf.Min(speed, offset.magnitude / Mathf.Max(.1f, MoveSmoothing));
        if (Time.time >= _nextShootTime && Vector2.Distance(transform.position, PlayerTarget.position) <= ShootRange)
        {
            _windingUp = true;
            _windupElapsed = 0;
            _aimPosition = TargetCollider != null ? TargetCollider.bounds.center : PlayerTarget.position;
            _animator?.TriggerShootAnimation(Mathf.Max(.1f, ShotWindup * 2));
        }
    }

    void Fire()
    {
        if (ObjectPooler.Instance == null) return;
        var bullet = ObjectPooler.Instance.GetEnemyBullet();
        if (bullet == null) return;
        var projectile = bullet.GetComponent<EnemyProjectile>();
        if (projectile == null) return;
        Vector2 direction = _aimPosition - (Vector2)transform.position;
        bullet.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        projectile.Configure(IsElite, SlowDuration);
        bullet.SetActive(true);
        AudioManager.Instance?.PlaySFX("Flying_Enemy_Shoot");
    }

    protected override void Die()
    {
        if (!IsElite) { base.Die(); return; }
        if (!BeginDeath()) return;
        _animator?.SetFlying(false);
        if (Rb != null) Rb.simulated = false;
        foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        StartCoroutine(CrashRoutine());
    }

    IEnumerator CrashRoutine()
    {
        Vector3 start = transform.position;
        float drift = Random.Range(-CrashDrift, CrashDrift);
        Vector2 rayOrigin = new Vector2(start.x + drift, start.y);
        var hit = Physics2D.Raycast(rayOrigin, Vector2.down, 100, GroundLayer);
        Vector3 end = hit.collider != null ? new Vector3(hit.point.x, hit.point.y + .15f, start.z) : start + new Vector3(drift, -10, 0);
        float elapsed = 0;
        while (elapsed < Mathf.Max(.1f, CrashDuration))
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, CrashDuration));
            transform.position = new Vector3(Mathf.Lerp(start.x, end.x, t), Mathf.Lerp(start.y, end.y, t * t), start.z);
            transform.Rotate(0, 0, drift * 60 * Time.deltaTime);
            yield return null;
        }
        if (LocalCoopSession.Instance != null)
        {
            foreach (var player in LocalCoopSession.Instance.Players)
            {
                var body = player.GetComponent<Collider2D>();
                if (player.Alive && body != null && Vector2.Distance(body.ClosestPoint(transform.position), transform.position) <= CrashExplosionRadius)
                    player.Health.TryTakeDamage(1);
            }
        }
        else if (TargetHealth != null && TargetCollider != null &&
            Vector2.Distance(TargetCollider.ClosestPoint(transform.position), transform.position) <= CrashExplosionRadius)
            TargetHealth.TryTakeDamage(1);
        if (CrashExplosionPrefab != null) ObjectPooler.SpawnEffect(CrashExplosionPrefab, transform.position, Quaternion.identity);
        CompleteDeath();
    }
}
