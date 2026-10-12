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
    Vector2 _velocity;
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
    public static ItemArea Place(ItemKind kind, PlayerStats owner)
    {
        var area = new GameObject(ItemCatalog.Names[(int)kind]).AddComponent<ItemArea>();
        area._kind = kind; area._owner = owner; area.transform.position = owner.transform.position;
        area._radius = kind == ItemKind.Shield ? 2.5f : 5;
        area._expires = Time.time + 30; area._waveExpires = PlayerItems.CompletedWaves + 10;
        area._visual = area.gameObject.AddComponent<SpriteRenderer>(); area._visual.sprite = Circle;
        area._visual.sortingOrder = 8;
        area._visual.color = kind == ItemKind.Shield ? new Color(.025f, .12f, .8f, .38f) : kind == ItemKind.BattleBanner ? new Color(1, .72f, .05f, .3f) : new Color(1, .28f, .025f, .3f);
        area.transform.localScale = Vector3.one * area._radius * 2;
        if (kind == ItemKind.Flamethrower) area.BuildMapFlames();
        if (kind == ItemKind.BattleBanner)
        {
            var banner = new GameObject("Banner placeholder").AddComponent<SpriteRenderer>(); banner.transform.SetParent(area.transform, false);
            banner.sprite = ItemCatalog.Icon((int)kind); banner.sortingOrder = 15; banner.transform.localScale = Vector3.one * .15f;
        }
        return area;
    }
    public static void LobBomb(PlayerStats owner, Vector2 aim)
    {
        var bomb = Place(ItemKind.Bomb, owner); bomb._expires = Time.time + 1.25f; bomb._radius = 4;
        bomb._velocity = aim.normalized * 12 + Vector2.up * 4;
        bomb._visual.sprite = ItemCatalog.Icon(1); bomb._visual.color = Color.white; bomb._visual.sortingOrder = 20;
        bomb.transform.localScale = Vector3.one * .8f;
    }
    void OnEnable() { _areas.Add(this); }
    void OnDisable() { _areas.Remove(this); }
    public static float BannerBonus(Vector2 position)
    {
        foreach (var area in _areas)
            if (area != null && area._kind == ItemKind.BattleBanner && PlayerItems.CompletedWaves < area._waveExpires && ((Vector2)area.transform.position - position).sqrMagnitude <= area._radius * area._radius) return .15f;
        return 0;
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
            Vector2 old = transform.position; _velocity += Physics2D.gravity * Time.deltaTime;
            Vector2 next = old + _velocity * Time.deltaTime;
            var movement = _owner != null ? _owner.GetComponent<PlayerController>() : null;
            var hit = Physics2D.Linecast(old, next, movement != null ? movement.GroundLayer.value : 0);
            transform.position = hit.collider != null ? hit.point : next;
            if (hit.collider != null || Time.time >= _expires) Explode();
            return;
        }
        if (_kind == ItemKind.BattleBanner ? PlayerItems.CompletedWaves >= _waveExpires : Time.time >= _expires) { Destroy(gameObject); return; }
        if (_kind != ItemKind.Flamethrower) return;
        _tick += Time.deltaTime;
        if (_tick < .2f) return;
        float damage = 5 * _tick; _tick = 0;
        EnemyBase.CopyActiveEnemies(_targets);
        foreach (var enemy in _targets) if (enemy != null && enemy.IsAlive) enemy.TakeItemDamage(damage, _owner);
    }
    void Explode()
    {
        EnemyBase.CopyActiveEnemies(_targets);
        foreach (var enemy in _targets) if (enemy != null && ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude <= _radius * _radius) enemy.TakeItemDamage(0, _owner, true);
        _kind = ItemKind.None; _expires = Time.time + .3f;
        _visual.sprite = Circle; _visual.color = new Color(1, .55f, .05f, .6f); transform.localScale = Vector3.one * _radius * 2;
    }
    void BuildMapFlames()
    {
        _visual.enabled = false; transform.localScale = Vector3.one;
        Bounds bounds = new Bounds(transform.position, new Vector3(40, 20, 0));
        var maps = FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None);
        foreach (var map in maps) if (map.GetComponent<Collider2D>() != null) bounds.Encapsulate(map.GetComponent<Collider2D>().bounds);
        if (WaveManager.Instance != null && WaveManager.Instance.SpawnPoints != null) foreach (var point in WaveManager.Instance.SpawnPoints) if (point != null) bounds.Encapsulate(point.position);
        // Bounded placeholder flames span world bounds, independent of the camera.
        for (int i = 0; i < 48; i++)
        {
            var flame = new GameObject("Flame placeholder").AddComponent<SpriteRenderer>(); flame.transform.SetParent(transform, false);
            flame.transform.position = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, i / 47f), bounds.center.y, 0);
            flame.sprite = ItemCatalog.Icon(8); flame.color = new Color(1, .5f, .15f, .4f); flame.sortingOrder = 6;
            flame.transform.localScale = new Vector3(Mathf.Max(1, bounds.size.x / 24), Mathf.Max(8, bounds.size.y), 1);
        }
    }
}
