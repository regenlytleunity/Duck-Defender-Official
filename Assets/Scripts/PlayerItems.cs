using System.Collections;
using UnityEngine;

public class PlayerItems : MonoBehaviour
{
    public readonly int[] Slots = new int[4];
    public int SelectedSlot { get; private set; }
    public float SelectionUntil { get; private set; }
    public float SpinUntil { get; private set; }
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
    bool _initialized, _loadoutsEnabled;
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
        if (!_initialized || !_player.Alive || Time.timeScale == 0) return;
        int slot = InputHelper.GetItemSelection(this);
        if (slot >= 0) Select(slot);
        if (InputHelper.GetUseItemDown(this)) UseSelected();
        if (CasinoActive && CasinoEffect == 3 && Time.time >= _nextRegen)
        { _nextRegen = Time.time + 5; _player.Health.Heal(1); }
    }
    public void Select(int slot)
    {
        if (!_initialized || slot < 0 || slot >= 4 || !_player.Alive || Time.timeScale == 0) return;
        SelectedSlot = slot; SelectionUntil = Time.time + 2;
    }
    public bool UseSelected()
    {
        if (!_initialized || !_loadoutsEnabled || !_player.Alive || !_player.Connected || _player.Health.IsStunned || Time.timeScale == 0 || Time.time < SpinUntil) return false;
        int id = Slots[SelectedSlot];
        if (!ItemCatalog.Valid(id)) return false;
        var data = SaveSystem.LoadData();
        if (data.ItemInventory[id] <= 0) { Slots[SelectedSlot] = 0; return false; }
        data.ItemInventory[id]--;
        var loadout = LocalCoopSession.Multiplayer ? data.CoopLoadouts[_player.Index] : data.SoloLoadout;
        loadout.Items[SelectedSlot] = 0;
        ShopManager.SanitizeItems(data, !LocalCoopSession.Multiplayer, LocalCoopSession.Multiplayer ? 1 : 4);
        SaveSystem.SaveData(data);
        Slots[SelectedSlot] = 0; SelectionUntil = Time.time + 2;
        switch ((ItemKind)id)
        {
            case ItemKind.Bomb: ItemArea.LobBomb(_player.Stats, _player.Weapon.AimDirection); break;
            case ItemKind.HealthPotion: _player.Health.Heal(_player.Health.MaxHealth); break;
            case ItemKind.Shield: ItemArea.Place(ItemKind.Shield, _player.Stats); break;
            case ItemKind.BattleBanner: ItemArea.Place(ItemKind.BattleBanner, _player.Stats); break;
            case ItemKind.PocketCasino: StartCoroutine(Spin()); break;
            case ItemKind.CloneProjector: ItemClone.Create(_player, -1); ItemClone.Create(_player, 1); break;
            case ItemKind.ExperiencePotion: LevelManager.Instance?.GrantItemLevel(); break;
            case ItemKind.Flamethrower: ItemArea.Place(ItemKind.Flamethrower, _player.Stats); break;
        }
        return true;
    }
    IEnumerator Spin()
    {
        SpinUntil = Time.time + 3;
        yield return new WaitForSeconds(3);
        int roll = Random.Range(0, 100);
        ApplyCasino(roll < 80 ? roll / 20 : 4 + (roll - 80) / 5);
    }
    public void ApplyCasino(int effect)
    {
        CasinoEffect = Mathf.Clamp(effect, 0, 7); CasinoExpiresWave = CompletedWaves + 2;
        _nextRegen = Time.time + 5;
        string[] names = { "+5 damage", "+25% speed", "+15% fire rate", "+1 health every 5 seconds", "Damage set to 1", "Speed set to 0", "Enemies drop no coins", "-25% experience" };
        ResultText = names[CasinoEffect]; ResultUntil = Time.time + 2;
    }
}
