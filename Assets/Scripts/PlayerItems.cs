using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerItems : MonoBehaviour
{
    public readonly int[] Slots = new int[4];
    public int SelectedSlot { get; private set; }
    public float SelectionUntil { get; private set; }
    public float SpinUntil { get; private set; }
    public float SpinEndAngle { get; private set; }
    public string ResultText { get; private set; }
    public float ResultUntil { get; private set; }
    public int CasinoEffect { get; private set; } = -1;
    public int CasinoExpiresWave { get; private set; }
    public bool CasinoActive => CasinoEffect >= 0 && CompletedWaves < CasinoExpiresWave;
    public bool DamageLocked => CasinoActive && CasinoEffect == 4;
    public bool SpeedLocked => CasinoActive && CasinoEffect == 5;
    public float FlatDamage => CasinoActive && CasinoEffect == 0 ? 5 : 0;
    public float SpeedBonus => CasinoActive && CasinoEffect == 1 ? .25f : 0;
    public float FireRateBonus => CasinoActive && CasinoEffect == 2 ? .15f : 0;
    public static int CompletedWaves => LocalCoopSession.Instance != null ? LocalCoopSession.Instance.CompletedWaves : 0;
    LocalPlayer _player;
    float _nextRegen;
    string _baseStats, _baseMovement, _savedStats, _savedMovement;
    int _baseMaxHealth, _savedMaxHealth, _savedRegen, _savedThorns;
    float _baseShotDelay, _divineRegen;
    readonly List<System.Action> _deferredCards = new List<System.Action>();
    readonly List<EnemyBase> _targets = new List<EnemyBase>();
    readonly List<EnemyBase> _lightningHits = new List<EnemyBase>(6);
    readonly List<Saw> _saws = new List<Saw>(4);
    float _orbit;
    int _lastPrunedOrbit;
    readonly List<int> _staleSawHits = new List<int>();
    public int SawbladeCount => _saws.Count;
    public int DivineExpiresWave { get; private set; }
    public bool DivineActive => _savedStats != null;
    public float DivineShotDelay => Mathf.Max(.005f, _baseShotDelay * .1f);
    sealed class Saw
    {
        public SpriteRenderer Visual;
        public readonly Dictionary<int, float> NextHit = new Dictionary<int, float>();
    }
    bool _initialized, _loadoutsEnabled;
    int _aimingSlot = -1;
    bool _touchHeld;
    LineRenderer _trajectory;
    static Material _trajectoryMaterial;
    public bool IsAimingThrowable => _aimingSlot >= 0;
    bool CanUse => _initialized && _loadoutsEnabled && _player != null && _player.Alive && _player.Connected &&
        !_player.Health.IsStunned && Time.timeScale > 0 && Time.time >= SpinUntil;
    public static bool SuppressEnemyCoins => TeamHasEffect(6);
    public static float TeamXPFactor => TeamHasEffect(7) ? .75f : 1;
    static bool TeamHasEffect(int effect)
    {
        if (LocalCoopSession.Instance == null) return false;
        foreach (var player in LocalCoopSession.Instance.Players)
            if (player.Stats.Items != null && player.Stats.Items.CasinoActive && player.Stats.Items.CasinoEffect == effect) return true;
        return false;
    }
    public void Initialize(LocalPlayer player, PlayerData data, bool loadoutsEnabled)
    {
        _player = player; _loadoutsEnabled = loadoutsEnabled; _initialized = true;
        var stats = player.Stats; stats.Items = this;
        stats.PermanentDamage = data.PermanentUpgrades[0] * .01f;
        stats.PermanentSpeed = data.PermanentUpgrades[2] * .01f;
        stats.PermanentFireRate = data.PermanentUpgrades[3] * .01f;
        stats.PermanentLuck = data.PermanentUpgrades[4] * .01f;
        player.Health.AddMaxHealthPercent(data.PermanentUpgrades[1] * .01f);
        // Capture the authored player plus permanent upgrades before starting cards apply.
        _baseStats = JsonUtility.ToJson(stats); _baseMovement = JsonUtility.ToJson(player.Movement);
        _baseMaxHealth = player.Health.MaxHealth; _baseShotDelay = player.Weapon.FireRate;
        var loadout = LocalCoopSession.Multiplayer ? data.CoopLoadouts[player.Index] : data.SoloLoadout;
        if (loadoutsEnabled)
        {
            System.Array.Copy(loadout.Items, Slots, 4);
            var manager = CardManager.Instance;
            if (manager != null) for (int i = 0; i < data.StartingCardSlots; i++)
            {
                string id = loadout.Cards[i];
                if (string.IsNullOrEmpty(id) || LocalCoopSession.Multiplayer && LocalCoopSession.DisabledCards.Contains(id)) continue;
                var card = manager.AllCards.Find(c => c != null && !c.IsBasic && c.ID == id);
                if (card != null && data.CardCollection.Exists(c => c.CardID == id && c.IsUnlocked)) manager.ApplyCardEffect(card, stats);
            }
        }
        gameObject.AddComponent<ItemWheelUI>().Initialize(this, player);
    }
    void Update()
    {
        if (!_initialized) return;
        if (!_player.Alive) { CancelUse(); ClearSawblades(); EndDivine(); return; }
        if (DivineActive && CompletedWaves >= DivineExpiresWave) EndDivine();
        if (Time.timeScale == 0) { CancelUse(); return; }
        UpdateSawblades();
        if (DivineActive)
        {
            _divineRegen += Time.deltaTime * 10;
            int heal = Mathf.FloorToInt(_divineRegen);
            if (heal > 0) { _divineRegen -= heal; _player.Health.Heal(heal); }
        }
        int slot = InputHelper.GetItemSelection(this);
        if (slot >= 0) Select(slot);
        if (!CanUse) CancelUse();
        else if (InputHelper.GetUseItemDown(this)) BeginUse();
        if (IsAimingThrowable)
        {
            if (!_touchHeld && !InputHelper.GetUseItemHeld(this)) ReleaseUse();
            else UpdateTrajectory();
        }
        if (CasinoActive && CasinoEffect == 3 && Time.time >= _nextRegen)
        { _nextRegen = Time.time + 5; _player.Health.Heal(1); }
    }
    public void Select(int slot)
    {
        if (!_initialized || slot < 0 || slot >= 4 || !_player.Alive || Time.timeScale == 0) return;
        if (SelectedSlot != slot) CancelUse();
        SelectedSlot = slot; SelectionUntil = Time.time + 2;
    }
    public void BeginUse(bool touch = false)
    {
        if (!CanUse || !ItemCatalog.Valid(Slots[SelectedSlot])) return;
        if (!ItemCatalog.IsThrowable(Slots[SelectedSlot])) { UseSelected(); return; }
        _touchHeld = touch; _aimingSlot = SelectedSlot;
        UpdateTrajectory();
    }
    public void ReleaseUse()
    {
        bool throwItem = IsAimingThrowable && _aimingSlot == SelectedSlot && CanUse;
        CancelUse();
        if (throwItem) UseSelected();
    }
    public void CancelUse()
    {
        _aimingSlot = -1; _touchHeld = false;
        if (_trajectory != null) _trajectory.enabled = false;
    }
    void OnDisable() { CancelUse(); ClearSawblades(); EndDivine(); }
    void UpdateTrajectory()
    {
        if (_trajectory == null)
        {
            _trajectory = new GameObject("Item throw path").AddComponent<LineRenderer>();
            _trajectory.transform.SetParent(transform, false);
            if (_trajectoryMaterial == null) _trajectoryMaterial = new Material(Shader.Find("Sprites/Default"));
            _trajectory.sharedMaterial = _trajectoryMaterial; _trajectory.useWorldSpace = true;
            _trajectory.startWidth = _trajectory.endWidth = .045f;
            _trajectory.startColor = Color.white; _trajectory.endColor = new Color(1, 1, 1, .4f);
            _trajectory.sortingOrder = 110;
        }
        _trajectory.enabled = true; _trajectory.positionCount = 33;
        Vector2 origin = transform.position, previous = origin;
        _trajectory.SetPosition(0, origin);
        for (int i = 1; i <= 32; i++)
        {
            Vector2 next = ItemArea.ThrowPosition(origin, _player.Weapon.AimDirection, ItemArea.BombFlightTime * i / 32f);
            bool hit = ItemArea.TraceBomb(previous, next, _player.Movement.GroundLayer, out var impact);
            _trajectory.SetPosition(i, impact);
            if (hit) { _trajectory.positionCount = i + 1; break; }
            previous = next;
        }
    }
    public bool UseSelected()
    {
        if (!CanUse) return false;
        int id = Slots[SelectedSlot];
        if (!ItemCatalog.Valid(id)) return false;
        if (id == (int)ItemKind.Sawblade && SawbladeCount >= 4) return false;
        var data = SaveSystem.LoadData();
        if (data.ItemInventory[id] <= 0) { Slots[SelectedSlot] = 0; return false; }
        data.ItemInventory[id]--;
        var loadout = LocalCoopSession.Multiplayer ? data.CoopLoadouts[_player.Index] : data.SoloLoadout;
        loadout.Items[SelectedSlot] = 0;
        ShopManager.SanitizeItems(data, !LocalCoopSession.Multiplayer, LocalCoopSession.Multiplayer ? 1 : 4);
        SaveSystem.SaveData(data);
        Slots[SelectedSlot] = 0; SelectionUntil = Time.time + 2;
        CancelUse();
        switch ((ItemKind)id)
        {
            case ItemKind.Bomb: ItemArea.LobBomb(_player.Stats, _player.Weapon.AimDirection); break;
            case ItemKind.HealthPotion: _player.Health.Heal(_player.Health.MaxHealth); break;
            case ItemKind.Shield: ItemArea.Place(ItemKind.Shield, _player.Stats); break;
            case ItemKind.BattleBanner: ItemArea.Place(ItemKind.BattleBanner, _player.Stats); break;
            case ItemKind.PocketCasino: StartCoroutine(Spin()); break;
            case ItemKind.CloneProjector: ItemClone.Create(_player, _player.Weapon.AimDirection.x < 0 ? -1 : 1); break;
            case ItemKind.ExperiencePotion: LevelManager.Instance?.GrantItemLevel(); break;
            case ItemKind.Flamethrower: ItemArea.Place(ItemKind.Flamethrower, _player.Stats); break;
            case ItemKind.Sawblade: AddSawblade(); break;
            case ItemKind.Shock: Shock(); break;
            case ItemKind.DivineIntervention: BeginDivine(); break;
            case ItemKind.Nuke: ItemArea.DropNuke(_player.Stats); break;
        }
        return true;
    }
    IEnumerator Spin()
    {
        int roll = Random.Range(0, 100);
        int effect = roll < 80 ? roll / 20 : 4 + (roll - 80) / 5;
        SpinEndAngle = effect < 4 ? 36 + effect * 72 : 297 + (effect - 4) * 18;
        SpinUntil = Time.time + 3;
        yield return new WaitForSeconds(3);
        ApplyCasino(effect);
    }
    public void ApplyCasino(int effect)
    {
        CasinoEffect = Mathf.Clamp(effect, 0, 7); CasinoExpiresWave = CompletedWaves + (CasinoEffect < 4 ? 10 : 2);
        _nextRegen = Time.time + 5;
        string[] names = { "+5 damage", "+25% speed", "+15% fire rate", "+1 health every 5 seconds", "Damage set to 1", "Speed set to 0", "Enemies drop no coins", "-25% experience" };
        ResultText = names[CasinoEffect]; ResultUntil = Time.time + 2;
    }

    public void AddSawblade()
    {
        if (_saws.Count >= 4) return;
        var visual = new GameObject("Item sawblade").AddComponent<SpriteRenderer>();
        visual.sprite = ItemCatalog.Icon((int)ItemKind.Sawblade); visual.sortingOrder = 100;
        visual.transform.localScale = Vector3.one * .6f;
        _saws.Add(new Saw { Visual = visual });
    }
    void ClearSawblades()
    {
        foreach (var saw in _saws) if (saw.Visual != null) Destroy(saw.Visual.gameObject);
        _saws.Clear(); _orbit = 0;
    }
    void UpdateSawblades()
    {
        if (_saws.Count == 0 || Camera.main == null) return;
        _orbit += Time.deltaTime / 1.5f;
        Vector3 center = Camera.main.ScreenToWorldPoint(InputHelper.GetMousePosition(this)); center.z = 0;
        EnemyBase.CopyActiveEnemies(_targets);
        if (Mathf.FloorToInt(_orbit) != _lastPrunedOrbit)
        {
            _lastPrunedOrbit = Mathf.FloorToInt(_orbit);
            foreach (var saw in _saws)
            {
                _staleSawHits.Clear();
                foreach (var hit in saw.NextHit) if (hit.Value <= _orbit) _staleSawHits.Add(hit.Key);
                foreach (int id in _staleSawHits) saw.NextHit.Remove(id);
            }
        }
        for (int i = 0; i < _saws.Count; i++)
        {
            var saw = _saws[i];
            float angle = (_orbit + i / (float)_saws.Count) * Mathf.PI * 2;
            Vector2 position = (Vector2)center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.5f;
            saw.Visual.transform.position = position; saw.Visual.transform.rotation = Quaternion.Euler(0, 0, -_orbit * 720);
            foreach (var enemy in _targets)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                int id = enemy.GetInstanceID();
                if (saw.NextHit.TryGetValue(id, out float next) && _orbit < next) continue;
                Vector2 point = enemy.BodyCollider != null ? enemy.BodyCollider.ClosestPoint(position) : (Vector2)enemy.transform.position;
                if ((point - position).sqrMagnitude > .3f * .3f) continue;
                saw.NextHit[id] = _orbit + 1;
                enemy.TakeItemDamage(1, _player.Stats);
            }
        }
    }
    public void Shock()
    {
        EnemyBase.CopyActiveEnemies(_targets);
        foreach (var enemy in _targets) if (enemy != null && enemy.IsAlive)
        {
            enemy.ApplyItemStun(10);
            _player.Weapon.ShowElectricChain(enemy.transform.position + Vector3.up * 12, enemy.transform.position);
            enemy.TakeItemDamage(enemy.MaxHealth * .5f, _player.Stats);
        }
    }
    public void BeginDivine()
    {
        if (_player == null || _baseStats == null) return;
        DivineExpiresWave = CompletedWaves + 2;
        if (DivineActive) return;
        var stats = _player.Stats;
        _savedStats = JsonUtility.ToJson(stats); _savedMovement = JsonUtility.ToJson(_player.Movement);
        _savedMaxHealth = _player.Health.MaxHealth; _savedRegen = _player.Health.RegenPerWave; _savedThorns = _player.Health.ThornsDamage;
        int coins = stats.CoinsCollectedRun;
        JsonUtility.FromJsonOverwrite(_baseStats, stats); stats.CoinsCollectedRun = coins;
        JsonUtility.FromJsonOverwrite(_baseMovement, _player.Movement);
        stats.CardsSuppressed = true;
        _player.Health.SetTemporaryMaxHealth(_baseMaxHealth);
        _player.Health.RegenPerWave = _player.Health.ThornsDamage = 0;
        _player.Movement.CancelCardMovement();
        _player.Weapon.StopCardAttacks();
        stats.GetComponent<AscensionEffects>()?.SuspendWalls(true);
        stats.NotifyTurretsChanged(); stats.NotifySecondWindChanged();
        _divineRegen = 0;
    }
    public void DeferCard(System.Action apply) { _deferredCards.Add(apply); }
    public void EndDivine()
    {
        if (!DivineActive || _player == null) return;
        var stats = _player.Stats;
        int coins = stats.CoinsCollectedRun;
        JsonUtility.FromJsonOverwrite(_savedStats, stats); stats.CoinsCollectedRun = coins;
        JsonUtility.FromJsonOverwrite(_savedMovement, _player.Movement);
        _savedStats = _savedMovement = null; stats.CardsSuppressed = false;
        _player.Weapon.StopCardAttacks();
        _player.Health.SetTemporaryMaxHealth(_savedMaxHealth);
        _player.Health.RegenPerWave = _savedRegen; _player.Health.ThornsDamage = _savedThorns;
        stats.GetComponent<AscensionEffects>()?.SuspendWalls(false);
        foreach (var apply in _deferredCards) apply();
        _deferredCards.Clear();
        stats.NotifyTurretsChanged(); stats.NotifySecondWindChanged();
    }
    public void FireDivineLightning()
    {
        if (!DivineActive) return;
        _lightningHits.Clear();
        Vector2 origin = _player.Weapon.FirePoint != null ? (Vector2)_player.Weapon.FirePoint.position : (Vector2)transform.position;
        Vector2 direction = _player.Weapon.AimDirection;
        EnemyBase first = null; float nearest = 40;
        foreach (var enemy in EnemyBase.ActiveEnemies)
        {
            if (enemy == null || !enemy.CanTakeDamage) continue;
            Vector2 delta = (Vector2)enemy.transform.position - origin;
            float along = Vector2.Dot(delta, direction);
            float width = enemy.BodyCollider != null ? Mathf.Max(enemy.BodyCollider.bounds.extents.x, enemy.BodyCollider.bounds.extents.y) : .5f;
            if (along < 0 || along > nearest || Mathf.Abs(delta.x * direction.y - delta.y * direction.x) > width + .4f) continue;
            nearest = along; first = enemy;
        }
        if (first == null) { _player.Weapon.ShowElectricChain(origin, origin + direction * 15); return; }
        EnemyBase target = first;
        Vector3 previous = first.transform.position + Vector3.up * 15;
        for (int hop = 0; hop <= 5 && target != null; hop++)
        {
            _lightningHits.Add(target);
            Vector3 position = target.transform.position;
            _player.Weapon.ShowElectricChain(previous, position);
            target.TakeItemDamage(target.MaxHealth * .5f * Mathf.Pow(.9f, hop), _player.Stats);
            previous = position; target = null; float best = 6 * 6;
            foreach (var enemy in EnemyBase.ActiveEnemies)
            {
                if (enemy == null || !enemy.CanTakeDamage || _lightningHits.Contains(enemy)) continue;
                float distance = (enemy.transform.position - position).sqrMagnitude;
                if (distance < best) { best = distance; target = enemy; }
            }
        }
    }
}
