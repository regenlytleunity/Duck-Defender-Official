using System.Collections.Generic;
using UnityEngine;

// Short-lived world effects. Damage uses EnemyBase's existing registry/reward path.
public class ItemArea : MonoBehaviour
{
    static readonly List<ItemArea> _areas = new List<ItemArea>();
    readonly List<EnemyBase> _targets = new List<EnemyBase>();
    ItemKind _kind;
    PlayerStats _owner;
    float _expires, _radius, _tick;
    int _waveExpires;
    Vector2 _throwOrigin;
    bool _radiation;
    int _level = 1;
    bool _mini, _bounced;
    Vector2 _velocity;
    readonly HashSet<PlayerStats> _visitors = new HashSet<PlayerStats>();
    static readonly RaycastHit2D[] _bombHits = new RaycastHit2D[64];
    public const float BombFlightTime = 2.5f;
    public static Vector2 ThrowPosition(Vector2 origin, Vector2 aim, float seconds) =>
        origin + (aim.normalized * 10 + Vector2.up * 6) * seconds + Vector2.down * (9 * seconds * seconds);
    public static bool TraceBomb(Vector2 from, Vector2 to, int groundMask, out Vector2 impact)
    {
        Vector2 delta = to - from;
        int count = Physics2D.CircleCast(from, .18f, delta.normalized, new ContactFilter2D { useTriggers = true }, _bombHits, delta.magnitude);
        float nearest = float.PositiveInfinity;
        impact = to;
        for (int i = 0; i < count; i++)
        {
            var hit = _bombHits[i];
            var enemy = hit.collider.GetComponentInParent<EnemyBase>();
            bool ground = !hit.collider.isTrigger && (groundMask & (1 << hit.collider.gameObject.layer)) != 0;
            if ((!ground && (enemy == null || !enemy.IsAlive)) || hit.distance >= nearest) continue;
            nearest = hit.distance; impact = hit.centroid;
        }
        return nearest < float.PositiveInfinity;
    }
    SpriteRenderer _visual;
    static Sprite _circle;
    public static Sprite Circle
    {
        get
        {
            if (_circle != null) return _circle;
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var colors = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                colors[y * 64 + x] = (new Vector2(x, y) - new Vector2(31.5f, 31.5f)).sqrMagnitude < 31 * 31 ? Color.white : Color.clear;
            texture.SetPixels(colors); texture.Apply();
            return _circle = Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * .5f, 64);
        }
    }
    public static ItemArea Place(ItemKind kind, PlayerStats owner, int level = 1)
    {
        var area = new GameObject(ItemCatalog.Names[(int)kind]).AddComponent<ItemArea>();
        area._kind = kind; area._owner = owner; area._level = level; area.transform.position = owner.transform.position;
        area._radius = kind == ItemKind.Shield ? (level >= 9 ? 3.5f : 2.5f) : (level >= 3 ? 4 : 3);
        area._expires = Time.time + (kind == ItemKind.Shield ? (level >= 9 ? 10 : 7) : 30);
        area._waveExpires = PlayerItems.CompletedWaves + (kind == ItemKind.Shield ? 2 : kind == ItemKind.Flamethrower ? 3 : 10);
        area._visual = area.gameObject.AddComponent<SpriteRenderer>(); area._visual.sprite = Circle;
        area._visual.sortingOrder = 8;
        area._visual.color = kind == ItemKind.Shield ? new Color(.025f, .12f, .8f, .38f) : kind == ItemKind.BattleBanner ? new Color(1, .72f, .05f, .3f) : new Color(1, .28f, .025f, .3f);
        area.transform.localScale = Vector3.one * area._radius * 2;
        if (kind == ItemKind.Flamethrower) area.BuildMapFlames();
        if (kind == ItemKind.Shield) area.PushEnemiesOutside();
        if (kind == ItemKind.BattleBanner)
        {
            var banner = new GameObject("Banner placeholder").AddComponent<SpriteRenderer>(); banner.transform.SetParent(area.transform, false);
            banner.sprite = ItemCatalog.Icon((int)kind); banner.sortingOrder = 15; banner.transform.localScale = Vector3.one * .15f;
        }
        return area;
    }
    public static void LobBomb(PlayerStats owner, Vector2 aim, int level = 1)
    {
        var bomb = Place(ItemKind.Bomb, owner, level); bomb._expires = Time.time + 8; bomb._radius = 2 + (level >= 2 ? 1 : 0) + (level >= 6 ? 1 : 0);
        bomb._velocity = aim.normalized * 10 + Vector2.up * 6;
        bomb._visual.sprite = ItemCatalog.Icon(1); bomb._visual.color = Color.white; bomb._visual.sortingOrder = 20;
        bomb.transform.localScale = Vector3.one * .8f;
    }
    public static ItemArea DropNuke(PlayerStats owner)
    {
        var nuke = Place(ItemKind.Nuke, owner);
        var camera = Camera.main;
        nuke.transform.position = new Vector3(owner.transform.position.x, camera != null ? camera.transform.position.y + camera.orthographicSize + 3 : owner.transform.position.y + 15, 0);
        nuke._throwOrigin = owner.transform.position;
        nuke._visual.sprite = ItemCatalog.Icon((int)ItemKind.Nuke); nuke._visual.color = Color.white;
        nuke._visual.sortingOrder = 100; nuke.transform.localScale = Vector3.one * 1.5f;
        return nuke;
    }
    void OnEnable() { _areas.Add(this); }
    void OnDisable() { _areas.Remove(this); }
    bool Influences(PlayerStats stats)
    {
        if (stats == null || Time.time >= _expires) return false;
        if (((Vector2)transform.position - (Vector2)stats.transform.position).sqrMagnitude <= _radius * _radius)
        { _visitors.Add(stats); return true; }
        return _level >= 10 && _visitors.Contains(stats);
    }
    public static float BannerBonus(PlayerStats stats)
    {
        float bonus = 0;
        foreach (var area in _areas)
            if (area != null && area._kind == ItemKind.BattleBanner && area.Influences(stats))
                bonus = Mathf.Max(bonus, area._level >= 9 ? .25f : area._level >= 6 ? .2f : area._level >= 2 ? .15f : .1f);
        return bonus;
    }
    public static float BannerReduction(PlayerStats stats)
    {
        float reduction = 0;
        foreach (var area in _areas)
            if (area != null && area._kind == ItemKind.BattleBanner && area.Influences(stats))
                reduction = Mathf.Max(reduction, area._level >= 7 ? .5f : area._level >= 5 ? .3f : 0);
        return reduction;
    }
    public static void PotionShockwave(PlayerStats owner)
    {
        foreach (var enemy in EnemyBase.ActiveEnemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            Vector2 delta = enemy.transform.position - owner.transform.position;
            if (delta.sqrMagnitude > 25) continue;
            enemy.ApplyKnockback(new Vector2(delta.x < 0 ? -12 : 12, 3));
        }
    }
    public static bool BlocksSegment(Vector2 from, Vector2 to)
    {
        foreach (var area in _areas)
        {
            if (area == null || area._kind != ItemKind.Shield || Time.time >= area._expires) continue;
            Vector2 delta = to - from;
            float t = delta.sqrMagnitude < .00001f ? 0 : Mathf.Clamp01(Vector2.Dot((Vector2)area.transform.position - from, delta) / delta.sqrMagnitude);
            if ((from + delta * t - (Vector2)area.transform.position).sqrMagnitude <= area._radius * area._radius) return true;
        }
        return false;
    }
    void FixedUpdate()
    {
        if (_kind != ItemKind.Shield || Time.time >= _expires) return;
        if (_owner != null) transform.position = _owner.transform.position;
        PushEnemiesOutside();
    }
    void PushEnemiesOutside()
    {
        // Resolve penetration at the body edge, including newly spawned enemies.
        foreach (var enemy in EnemyBase.ActiveEnemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            var body = enemy.GetComponent<Rigidbody2D>(); var collider = enemy.GetComponent<Collider2D>();
            float extent = collider != null ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) : .5f;
            Vector2 delta = (Vector2)enemy.transform.position - (Vector2)transform.position;
            float distance = _radius + extent;
            if (delta.sqrMagnitude >= distance * distance) continue;
            Vector2 direction = delta.sqrMagnitude > .0001f ? delta.normalized : Vector2.right;
            // Ground enemies below the player's pivot must leave sideways, never be pushed underground.
            if (direction.y < 0) direction = delta.x < 0 ? Vector2.left : Vector2.right;
            Vector2 outside = (Vector2)transform.position + direction * distance;
            if (body != null) { body.position = outside; float inward = Vector2.Dot(body.linearVelocity, direction); if (inward < 0) body.linearVelocity -= direction * inward; }
            else enemy.transform.position = outside;
        }
    }
    void Update()
    {
        if (Time.timeScale == 0) return;
        if (_kind == ItemKind.Bomb)
        {
            Vector2 old = transform.position;
            Vector2 next = old + _velocity * Time.deltaTime + Vector2.down * 9 * Time.deltaTime * Time.deltaTime;
            _velocity += Vector2.down * 18 * Time.deltaTime;
            var movement = _owner != null ? _owner.GetComponent<PlayerController>() : null;
            bool hit = TraceBomb(old, next, movement != null ? movement.GroundLayer.value : 0, out var impact);
            transform.position = impact;
            if (hit || Time.time >= _expires)
            {
                bool bounce = hit && !_mini && _level >= 10 && !_bounced;
                Detonate();
                if (bounce)
                {
                    _bounced = true;
                    _velocity = new Vector2(_velocity.x * .65f, 7);
                    transform.position += Vector3.up * .45f;
                    _expires = Time.time + 3;
                }
                else Destroy(gameObject);
            }
            return;
        }
        if (_kind == ItemKind.Nuke && !_radiation)
        {
            transform.position = Vector3.MoveTowards(transform.position, _throwOrigin, 22 * Time.deltaTime);
            if (Vector2.Distance(transform.position, _throwOrigin) > .05f) return;
            EnemyBase.CopyActiveEnemies(_targets);
            foreach (var enemy in _targets) if (enemy != null && enemy.IsAlive) enemy.TakeItemDamage(0, _owner, true);
            _radiation = true; _waveExpires = PlayerItems.CompletedWaves + 10;
            BuildMapFlames();
        }
        if (_kind == ItemKind.BattleBanner && LocalCoopSession.Instance != null)
            foreach (var player in LocalCoopSession.Instance.Players) if (player.Alive) Influences(player.Stats);
        if (_kind == ItemKind.None && _radiation)
        {
            while (_tick <= Mathf.Min(Time.time, _expires))
            {
                _tick += 1;
                EnemyBase.CopyActiveEnemies(_targets);
                foreach (var enemy in _targets)
                    if (enemy != null && ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude <= _radius * _radius)
                        enemy.TakeItemDamage(enemy.MaxHealth * .05f, _owner);
            }
            if (Time.time >= _expires) Destroy(gameObject);
            return;
        }
        if (_kind == ItemKind.None || _kind == ItemKind.Shield || _kind == ItemKind.BattleBanner ? Time.time >= _expires : PlayerItems.CompletedWaves >= _waveExpires) { Destroy(gameObject); return; }
        if (_kind != ItemKind.Flamethrower && !_radiation) return;
        _tick += Time.deltaTime;
        if (_tick < .2f) return;
        float seconds = _tick; _tick = 0;
        EnemyBase.CopyActiveEnemies(_targets);
        foreach (var enemy in _targets) if (enemy != null && enemy.IsAlive)
        {
            if (_radiation) enemy.ApplyZoneSlow(.5f, .3f);
            else enemy.TakeItemDamage(enemy.MaxHealth * .1f * seconds, _owner);
        }
    }
    void Detonate()
    {
        EnemyBase.CopyActiveEnemies(_targets);
        foreach (var enemy in _targets)
            if (enemy != null && ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude <= _radius * _radius)
                enemy.TakeItemDamage(0, _owner, true);
        var flash = Place(ItemKind.None, _owner);
        flash.transform.position = transform.position;
        flash._radius = _radius; flash.transform.localScale = Vector3.one * _radius * 2;
        flash._expires = Time.time + .3f;
        if (!_mini && _level >= 7)
        {
            flash._radiation = true; flash._tick = Time.time + 1;
            flash._expires = Time.time + 3;
            flash._visual.color = new Color(.3f, .8f, .15f, .35f);
        }
        if (_mini || _level < 5) return;
        for (int direction = -1; direction <= 1; direction += 2)
        {
            var mini = Place(ItemKind.Bomb, _owner, 1);
            mini._mini = true; mini._radius = 1.5f;
            mini.transform.position = transform.position + new Vector3(direction * .3f, .4f);
            mini.transform.localScale = Vector3.one * .4f;
            mini._visual.sprite = ItemCatalog.Icon(1); mini._visual.color = Color.white;
            mini._velocity = new Vector2(direction * 6, 6); mini._expires = Time.time + 4;
        }
    }
    void BuildMapFlames()
    {
        _visual.enabled = false; transform.localScale = Vector3.one;
        var movement = _owner != null ? _owner.GetComponent<PlayerController>() : null;
        var prefab = movement != null ? movement.FireTrailPatchPrefab : null;
        if (prefab == null) { Debug.LogWarning("Flamethrower needs PlayerController.FireTrailPatchPrefab."); return; }
        var world = WorldCamera.Instance;
        float left = world != null ? world.Left - world.SpawnPadding : transform.position.x - 20;
        float right = world != null ? world.Right + world.SpawnPadding : transform.position.x + 20;
        float floor = world != null ? world.Floor : transform.position.y - 1;
        var hits = new RaycastHit2D[8];
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = movement.GroundLayer, useTriggers = false };
        int count = Mathf.Clamp(Mathf.CeilToInt((right - left) / 2), 1, 128);
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(left, right, (i + .5f) / count);
            int found = Physics2D.Raycast(new Vector2(x, floor + 2), Vector2.down, filter, hits, 8);
            for (int j = 0; j < found; j++)
            {
                if (hits[j].normal.y < .5f || world != null && world.IsPlayerWall(hits[j].collider)) continue;
                var flame = Instantiate(prefab, hits[j].point, Quaternion.identity, transform);
                // ItemArea owns damage and wave lifetime; these patches supply only the authored visual.
                var patch = flame.GetComponent<FireTrailPatch>();
                if (patch != null) { patch.Initialize(0, 1); patch.enabled = false; }
                foreach (var collider in flame.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                foreach (var particles in flame.GetComponentsInChildren<ParticleSystem>())
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particles.main; main.loop = true; main.stopAction = ParticleSystemStopAction.None;
                    if (_radiation)
                    {
                        main.startColor = new Color(.25f, 1, .15f, .65f);
                        var color = particles.colorOverLifetime; color.enabled = false;
                    }
                    particles.Play();
                }
                break;
            }
        }
    }
}
