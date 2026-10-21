using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public class ItemClone : MonoBehaviour
{
    static readonly List<ItemClone> _clones = new List<ItemClone>();
    public PlayerStats Source { get; private set; }
    public Vector2 Move { get; private set; }
    public Vector2 Aim { get; private set; } = Vector2.right;
    public bool Shooting { get; private set; }
    public bool Jump { get; private set; }
    public bool JumpHeld => Time.time < _jumpHoldUntil;
    public float SpawnMoveSpeed { get; private set; }
    public float SpawnAcceleration { get; private set; }
    PlayerStats _stats;
    PlayerController _movement;
    PlayerHealth _health;
    EnemyBase _target;
    int _side, _expiresWave;
    float _retarget, _jumpAfter, _jumpHoldUntil;
    float _moveDirection;
    Rigidbody2D _body, _sourceBody;
    public static ItemClone Create(LocalPlayer player, int side)
    {
        var holder = new GameObject("Clone initialization"); holder.SetActive(false);
        var body = Instantiate(player.gameObject, holder.transform);
        body.name = side < 0 ? "Left clone" : "Right clone"; body.tag = "Untagged";
        var clone = body.AddComponent<ItemClone>(); clone.Source = player.Stats; clone._side = side;
        clone.SpawnMoveSpeed = player.Movement.EffectiveMaxSpeed;
        clone.SpawnAcceleration = player.Movement.EffectiveAcceleration;
        clone._body = body.GetComponent<Rigidbody2D>(); clone._sourceBody = player.GetComponent<Rigidbody2D>();
        clone._stats = body.GetComponent<PlayerStats>(); clone._movement = body.GetComponent<PlayerController>(); clone._health = body.GetComponent<PlayerHealth>();
        clone._expiresWave = PlayerItems.CompletedWaves + 5;
        // These are real player-only components. The AI supplies input through InputHelper.
        foreach (var component in body.GetComponents<LocalPlayer>()) { component.enabled = false; Destroy(component); }
        foreach (var component in body.GetComponents<PlayerItems>()) { component.enabled = false; Destroy(component); }
        foreach (var component in body.GetComponents<ItemWheelUI>()) { component.enabled = false; Destroy(component); }
        var arrow = body.transform.Find("Aim direction"); if (arrow != null) Destroy(arrow.gameObject);
        var trajectory = body.transform.Find("Item throw path"); if (trajectory != null) Destroy(trajectory.gameObject);
        clone._stats.CopyCloneRuntime(player.Stats);
        body.transform.position = player.transform.position + Vector3.right * side * 1.5f;
        body.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        body.GetComponent<SpriteRenderer>().color = new Color(.55f, .85f, 1, .65f);
        // IgnoreCollision only persists for active colliders. Configure after
        // leaving the inactive initialization holder so clones cannot block each other.
        body.transform.SetParent(null, true); body.SetActive(true); Destroy(holder);
        foreach (var own in body.GetComponentsInChildren<Collider2D>())
        {
            if (LocalCoopSession.Instance != null) foreach (var other in LocalCoopSession.Instance.Players)
                foreach (var collider in other.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(own, collider);
            foreach (var other in _clones) if (other != null && other != clone) foreach (var collider in other.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(own, collider);
        }
        var template = TurretManager.Instance;
        if (template != null)
        {
            var manager = new GameObject("Clone companions").AddComponent<TurretManager>(); manager.transform.SetParent(body.transform);
            manager.MarksmanPrefab = template.MarksmanPrefab; manager.MedicPrefab = template.MedicPrefab;
            manager.ProtectorPrefab = template.ProtectorPrefab; manager.ElementalPrefab = template.ElementalPrefab; manager.SecondWindAngelPrefab = template.SecondWindAngelPrefab;
            manager.AutoFindPlayer = false; manager.PlayerTransform = body.transform;
        }
        return clone;
    }
    void OnEnable() { _clones.Add(this); }
    void OnDisable() { _clones.Remove(this); }
    public static PlayerStats Nearest(Vector2 position, PlayerStats current)
    {
        float distance = current != null ? ((Vector2)current.transform.position - position).sqrMagnitude : float.PositiveInfinity;
        foreach (var clone in _clones)
        {
            if (clone == null || clone._health == null || clone._health.IsDead) continue;
            float candidate = ((Vector2)clone.transform.position - position).sqrMagnitude;
            if (candidate < distance) { distance = candidate; current = clone._stats; }
        }
        return current;
    }
    void Update()
    {
        Jump = false;
        if (Time.timeScale == 0) return;
        if (Source == null || PlayerItems.CompletedWaves >= _expiresWave) { Destroy(gameObject); return; }
        if (Time.time >= _retarget)
        {
            _retarget = Time.time + .2f; float best = float.PositiveInfinity; _target = null;
            foreach (var enemy in EnemyBase.ActiveEnemies)
            {
                if (enemy == null || !enemy.CanTakeDamage) continue;
                float score = (enemy.transform.position - transform.position).sqrMagnitude;
                if ((enemy.transform.position.x - Source.transform.position.x) * _side < 0) score += 1000;
                if (score < best) { best = score; _target = enemy; }
            }
        }
        Shooting = _target != null;
        if (_target != null) Aim = (_target.transform.position - transform.position).normalized;
        float sourceVelocity = _sourceBody != null ? _sourceBody.linearVelocity.x : 0;
        float desiredX = Source.transform.position.x + _side * 3 + sourceVelocity * .2f;
        float delta = desiredX - transform.position.x;
        // Full-strength movement with a stopping band avoids rapid direction
        // changes that keep the shared player controller from accelerating.
        if (Mathf.Abs(delta) > 1.1f) _moveDirection = Mathf.Sign(delta);
        else if (Mathf.Abs(delta) < .35f || _moveDirection * delta < 0) _moveDirection = 0;
        float x = _moveDirection;
        if (_target != null && Vector2.Distance(_target.transform.position, transform.position) < 2 && Mathf.Abs(delta) < 4)
            x = Mathf.Sign(transform.position.x - _target.transform.position.x);
        // Keep the AI in its owner's neighborhood and out of the owner's body.
        if (Mathf.Abs(transform.position.x - Source.transform.position.x) > 5.5f) x = Mathf.Sign(delta);
        Move = new Vector2(x, 0);
        var hit = Physics2D.Raycast(transform.position, Vector2.right * x, 1.2f, _movement.GroundLayer);
        if (_movement.IsGrounded && Time.time >= _jumpAfter && (hit.collider != null || Source.transform.position.y > transform.position.y + 1.5f))
        { Jump = true; _jumpHoldUntil = Time.time + .25f; _jumpAfter = Time.time + .65f; }
        if (Vector2.Distance(transform.position, Source.transform.position) > 6)
        { transform.position = Source.transform.position + Vector3.right * _side * 1.5f; _body.linearVelocity = Vector2.zero; }
    }
    public Vector3 AimScreenPosition()
    {
        var weapon = GetComponent<WeaponPlayer>(); var origin = weapon.FirePoint != null ? weapon.FirePoint.position : transform.position;
        return Camera.main != null ? Camera.main.WorldToScreenPoint(origin + (Vector3)Aim * 10) : Vector3.zero;
    }
}
