#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Run only through the existing isolated verification save. Never touches the real profile.
public class ItemUpdatePlayProbe : MonoBehaviour
{
    public static string Result;
    public static string ReportPath => Path.GetFullPath("tmp/item-update/verification.txt");
    readonly List<string> _checks = new List<string>();
    readonly List<Gamepad> _pads = new List<Gamepad>(), _realPads = new List<Gamepad>();
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks.Add(message); Result = "Running: " + message;
    }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(SaveSystem.VerificationSavePath)) throw new InvalidOperationException("Start isolated Play verification first.");
        DontDestroyOnLoad(gameObject); Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        foreach (var pad in Gamepad.all.ToArray()) if (pad.enabled) { _realPads.Add(pad); InputSystem.DisableDevice(pad); }
        for (int i = 0; i < 2; i++) _pads.Add(InputSystem.AddDevice<Gamepad>());
        var routine = Run();
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception ex) { Result = "FAILED: " + ex; Finish(); yield break; }
            yield return next;
        }
        Result = "PASS: " + _checks.Count + " update 6 checks"; Finish();
    }
    void Finish()
    {
        File.WriteAllText(ReportPath, Result + "\n" + string.Join("\n", _checks));
        Debug.Log(Result); Destroy(gameObject);
    }
    void OnDestroy()
    {
        foreach (var pad in _pads) if (pad.added) InputSystem.RemoveDevice(pad);
        foreach (var pad in _realPads) if (pad.added) InputSystem.EnableDevice(pad);
        Time.timeScale = 1;
    }
    IEnumerator Pad(GamepadState state, float seconds = .06f)
    {
        InputSystem.QueueStateEvent(_pads[0], state); yield return new WaitForSecondsRealtime(seconds);
        InputSystem.QueueStateEvent(_pads[0], new GamepadState()); yield return new WaitForSecondsRealtime(.06f);
    }
    EnemyBase Enemy(Vector2 position, float health = 100)
    {
        var go = new GameObject("Update 6 test enemy"); go.transform.position = position;
        go.AddComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        go.AddComponent<BoxCollider2D>();
        var enemy = go.AddComponent<SwarmerEnemy>(); enemy.BaseHealth = health; enemy.BaseSpeed = 0;
        enemy.SpawnProtectionSeconds = 0; enemy.XPValue = 0; enemy.CoinDropRange = Vector2Int.zero;
        enemy.SeparationForce = 0; enemy.DamageOnHit = 0; enemy.Initialize(1);
        return enemy;
    }
    void ClearEnemies()
    {
        foreach (var enemy in EnemyBase.ActiveEnemies.ToArray()) if (enemy != null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
    }
    void Use(PlayerItems items, ItemKind kind)
    {
        items.Slots[0] = (int)kind; items.Select(0);
        int before = SaveSystem.LoadData().ItemInventory[(int)kind];
        Check(items.UseSelected(), "activate " + kind);
        Check(SaveSystem.LoadData().ItemInventory[(int)kind] == before - 1 && items.Slots[0] == 0, "consume exactly one " + kind);
    }
    IEnumerator Run()
    {
        int frame = Time.frameCount; yield return null; yield return null;
        Check(Time.frameCount > frame, "Play Mode advances frames");
        var old = new PlayerData { ItemInventory = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, TotalCoins = 5432 };
        old.NormalizeShopData();
        Check(old.ItemInventory.Length == 13 && old.ItemInventory[8] == 8 && old.ItemInventory[12] == 0 && old.TotalCoins == 5432, "old inventories expand without losing items or coins");
        Check(ItemCatalog.Prices[(int)ItemKind.Flamethrower] == 500 && ItemCatalog.Prices[(int)ItemKind.Sawblade] == 250 && ItemCatalog.Prices[(int)ItemKind.Nuke] == 1000, "updated item prices");
        ControllerBindings.Reset(); InputManager.Instance.ResetToDefaults();
        Check(ControllerBindings.TrySet("UseItem", "buttonWest", out _) && ControllerBindings.TrySet("Shoot", "rightShoulder", out _), "controller item and gameplay controls can trade formerly reserved buttons");
        Check(ControllerBindings.TrySet("Item1", "start", out _) && ControllerBindings.Get("Item1") == "start", "Start can be assigned to an item slot");
        Check(!ControllerBindings.TrySet("Item2", "start", out _), "duplicate gameplay bindings are rejected");
        ControllerBindings.Reset();
        var menu = MainMenuUI.Instance; var shop = ShopManager.Instance;
        var data = shop.Progress;
        data.TotalCoins = 500000; data.StartingCardSlots = 5; data.PackOpeningTier = 2;
        for (int i = 1; i <= ItemCatalog.Count; i++) data.ItemInventory[i] = 20;
        foreach (var loadout in data.CoopLoadouts) Array.Clear(loadout.Items, 0, 4);
        SaveSystem.SaveData(data); shop.LoadEconomy();
        LocalCoopSession.RequestedPlayers = 2; LocalCoopSession.AdvancedLoadouts = true; LocalCoopSession.KeyboardTest = false;
        var host = menu.GetComponent<HostMenuUI>(); host.Open(); host.Hide();
        var lobby = menu.GetComponent<CoopLobbyUI>() ?? menu.gameObject.AddComponent<CoopLobbyUI>(); lobby.Open(host);
        for (int i = 0; i < 2; i++) LocalCoopSession.PlayerControllers[i] = _pads[i];
        var panels = Field<LoadoutPanel[]>(lobby, "_loadouts");
        for (int i = 0; i < 2; i++) { panels[i].Root.gameObject.SetActive(true); panels[i].ShowCards(false); }
        panels[1].OpenItem(1); var modal = panels[1].NavigationScope;
        Check(shop.EquipItem(1, 0, 0), "P1 equips shared item");
        Check(panels.Take(2).All(p => p.Root.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text.Contains("Bomb\nOwned: 20\nAvailable: 19"))), "both lobby availability counters refresh immediately");
        Check(panels[1].NavigationScope == modal && panels[1].HasModal, "another player's open equip menu survives inventory refresh");
        Check(shop.EquipItem(0, 0, 0), "P1 unequips shared item");
        Check(panels.Take(2).All(p => p.Root.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text.Contains("Bomb\nOwned: 20\nAvailable: 20"))), "both lobby counters refresh on unequip");
        lobby.Close(); LocalCoopSession.RequestedPlayers = 1;
        menu.ShowPanel(menu.SettingsPanel); var settings = menu.SettingsPanel.GetComponent<SettingsMenuUI>();
        settings.OpenKeybinds(); yield return null;
        Check(settings.KeybindLabels.Length == 15 && settings.KeybindLabels.All(t => t != null), "settings includes all keyboard item controls");
        Check(settings.TryAssignBinding(10, KeyCode.Mouse2) && settings.TryAssignBinding(5, KeyCode.Mouse1), "keyboard item use and shooting can trade former reserved key");
        settings.RestoreKeybinds();
        var controllerUI = Field<ControllerBindingsUI>(settings, "_controllerBindings"); controllerUI.Show(true); yield return null;
        Check(settings.KeybindPanel.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Item slot 4"), "controller settings includes every item slot");
        foreach (var label in settings.KeybindPanel.GetComponentsInChildren<TMPro.TMP_Text>())
        { label.ForceMeshUpdate(); Check(label.font == menu.UIFont && label.color == Color.white && !label.isTextOverflowing, "controller text fits with original white font: " + label.text); }
        ScreenCapture.CaptureScreenshot("tmp/item-update/controller-settings.png"); yield return null;
        settings.CloseKeybinds(); yield return null;
        var slider = settings.MasterSlider; slider.value = .5f;
        EventSystem.current.SetSelectedGameObject(slider.gameObject);
        yield return Pad(new GamepadState { leftStick = Vector2.right }, .2f);
        Check(Mathf.Approximately(slider.value, .5f), "browsing past a slider does not change its value");
        EventSystem.current.SetSelectedGameObject(slider.gameObject);
        yield return Pad(new GamepadState().WithButton(GamepadButton.East));
        yield return Pad(new GamepadState { leftStick = Vector2.right }, .3f);
        Check(slider.value > .55f && EventSystem.current.currentSelectedGameObject == slider.gameObject, "Interact locks slider focus and thumbstick adjusts smoothly");
        yield return Pad(new GamepadState().WithButton(GamepadButton.East));
        float value = slider.value;
        yield return Pad(new GamepadState { leftStick = Vector2.right }, .2f);
        Check(Mathf.Approximately(slider.value, value), "second Interact exits slider editing");
        menu.OpenShop(); menu.GetComponent<ShopUpdateUI>().ShowPage(0); yield return null;
        float tripleHeight = 0;
        foreach (int count in new[] { 3, 6 })
        {
            menu.PackContainer.GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
            (count == 3 ? menu.BuyThreeButton : menu.BuySixButton).onClick.Invoke(); yield return null;
            if (count == 3) Check(menu.PackImage.rectTransform.rect.height >= 342, "three packs use their own larger pack size");
            menu.SendMessage("CompleteSlice");
            float deadline = Time.unscaledTime + 15;
            while (!menu.ContinueButton.gameObject.activeSelf && Time.unscaledTime < deadline) yield return null;
            Check(menu.CardRevealCenter.GetComponentsInChildren<CardDisplay>().Length == count * 3, "all cards revealed for " + count + " packs");
            if (count == 3) { tripleHeight = menu.RevealGrid.cellSize.y; Check(menu.RevealGrid.constraintCount == 5, "three-pack reveal uses five columns and two rows"); }
            else Check(tripleHeight > menu.RevealGrid.cellSize.y * 1.3f, "three-pack cards are substantially larger than six-pack cards");
            ScreenCapture.CaptureScreenshot("tmp/item-update/reveal-" + count + ".png"); yield return null;
            menu.ContinueButton.onClick.Invoke(); yield return null;
        }
        menu.OpenIndex(); yield return null;
        var cardView = menu.IndexPanel.GetComponentsInChildren<CardDisplay>().First(c => c.AssignedCard != null);
        var saved = shop.GetCardData(cardView.AssignedCard.ID); saved.IsAscended = true; saved.Level = 6;
        cardView.Setup(cardView.AssignedCard); yield return null;
        Color beforeColor = cardView.BackgroundImage.color; yield return new WaitForSecondsRealtime(.3f);
        Check(cardView.BackgroundImage.color != beforeColor && cardView.NameText.color == Color.white, "ascended background changes chromatically while text remains white");
        data = SaveSystem.LoadData(); Array.Clear(data.PermanentUpgrades, 0, 5); Array.Clear(data.SoloLoadout.Cards, 0, 5);
        data.ShowTips = false; SaveSystem.SaveData(data);
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene"); yield return null; yield return null; yield return new WaitForSeconds(.3f);
        var wave = WaveManager.Instance; wave.StopAllCoroutines(); wave.enabled = false;
        if (EnemyTipUI.Instance != null) { EnemyTipUI.Instance.Dismiss(); EnemyTipUI.Instance.enabled = false; }
        ClearEnemies();
        var plan = new List<WaveManager.SpawnRequest>();
        for (int kind = 0; kind < 4; kind++) foreach (bool elite in new[] { false, true })
        {
            int number = (int)(elite ? wave.EliteUnlockWaves[kind] : wave.NormalUnlockWaves[kind]);
            for (int roll = 0; roll < 6; roll++)
            {
                Check(wave.TryBuildWave(number, null, plan, out _), "build introduction wave " + number);
                var intro = plan.Single(p => p.Introduction);
                Check(plan.Count(p => p.Prefab == intro.Prefab && p.Elite == intro.Elite) == 1, "exactly one introduced variant on wave " + number);
            }
        }
        var player = LocalCoopSession.Instance.Players[0]; player.AssignController(_pads[0], true);
        float baseShotDelay = player.Weapon.FireRate;
        var items = player.Stats.Items;
        InputSystem.QueueStateEvent(_pads[0], new GamepadState { rightStick = Vector2.right }); yield return null;
        Check(ItemArea.ThrowPosition(Vector2.zero, new Vector2(1, -4), .5f) == ItemArea.ThrowPosition(Vector2.zero, new Vector2(1, 4), .5f), "bomb arc depends only on left/right aim");
        var bombTarget = Enemy(ItemArea.ThrowPosition(player.transform.position, Vector2.right, .25f));
        Use(items, ItemKind.Bomb); yield return new WaitForSeconds(.55f);
        Check(bombTarget == null || !bombTarget.IsAlive, "bomb detonates on enemy contact before landing"); ClearEnemies();
        var a = Enemy((Vector2)player.transform.position + new Vector2(5, 2), 100);
        var b = Enemy((Vector2)player.transform.position + new Vector2(8, 2), 200);
        Use(items, ItemKind.Shock);
        Check(a.IsFrozen && b.IsFrozen && a.HealthRemaining == a.MaxHealth * .5f && b.HealthRemaining == b.MaxHealth * .5f, "Shock stuns and removes exactly half maximum health");
        yield return new WaitForSeconds(9.5f); Check(a.IsFrozen, "Shock remains active before ten seconds");
        yield return new WaitForSeconds(.7f); Check(!a.IsFrozen, "Shock ends after ten seconds"); ClearEnemies();
        Use(items, ItemKind.Shield); var shield = FindObjectsByType<ItemArea>(FindObjectsSortMode.None).Single(x => x.name == "Shield");
        Set(shield, "_expires", Time.time - 1); yield return null;
        Check(shield != null, "shield ignores the former seconds timer");
        LocalCoopSession.Instance.CompleteWave(); yield return null; Check(shield != null, "shield survives one wave");
        LocalCoopSession.Instance.CompleteWave(); yield return null; yield return null; Check(shield == null, "shield expires after two waves");
        items.ApplyCasino(0); int expiry = items.CasinoExpiresWave;
        Check(expiry == PlayerItems.CompletedWaves + 10, "positive casino effect lasts ten waves");
        items.ApplyCasino(4); Check(items.CasinoExpiresWave == PlayerItems.CompletedWaves + 2, "negative casino effect lasts two waves");
        LocalCoopSession.Instance.CompleteWave(); LocalCoopSession.Instance.CompleteWave();
        Use(items, ItemKind.CloneProjector); yield return null; yield return null;
        var clone = FindObjectsByType<ItemClone>(FindObjectsSortMode.None).Single();
        Check(Mathf.Approximately(clone.GetComponent<WeaponPlayer>().EffectiveFireInterval, player.Weapon.EffectiveFireInterval) && Mathf.Approximately(clone.GetComponent<WeaponPlayer>().FeatherDamage(), player.Weapon.FeatherDamage()), "one clone copies full damage and fire rate");
        float interval = clone.GetComponent<WeaponPlayer>().EffectiveFireInterval; player.Weapon.ApplyFireRateReduction(.1f);
        Check(Mathf.Approximately(clone.GetComponent<WeaponPlayer>().EffectiveFireInterval, interval), "later player upgrades do not alter clone stats");
        for (int i = 0; i < 4; i++) LocalCoopSession.Instance.CompleteWave(); yield return null; Check(clone != null, "clone survives four waves");
        LocalCoopSession.Instance.CompleteWave(); yield return null; yield return null; Check(clone == null, "clone expires after five waves");
        a = Enemy((Vector2)player.transform.position + new Vector2(30, 2), 100); b = Enemy((Vector2)player.transform.position + new Vector2(35, 2), 200);
        Use(items, ItemKind.Flamethrower); yield return new WaitForSeconds(1.05f);
        Check(a.HealthRemaining / a.MaxHealth < .92f && a.HealthRemaining / a.MaxHealth > .86f && Mathf.Abs(a.HealthRemaining / a.MaxHealth - b.HealthRemaining / b.MaxHealth) < .001f, "flamethrower applies ten percent maximum health per second to different health pools");
        var fire = FindObjectsByType<ItemArea>(FindObjectsSortMode.None).Single(x => x.name == "Flamethrower");
        for (int i = 0; i < 2; i++) LocalCoopSession.Instance.CompleteWave(); yield return null; Check(fire != null, "flames survive two waves");
        LocalCoopSession.Instance.CompleteWave(); yield return null; yield return null; Check(fire == null, "flames expire after three waves"); ClearEnemies();
        var stats = player.Stats; stats.GlobalDamageBonus = .75f; stats.HasMiniGun = true; stats.HasSlowingAura = true; stats.SlowingAuraSlowPercent = .3f;
        stats.ActivateAscension(CardAscension.RecoveryPlus); player.Movement.MaxRunSpeed += 5; player.Health.AddMaxHealth(20);
        int originalMax = player.Health.MaxHealth; float speed = player.Movement.MaxRunSpeed;
        stats.CoinsCollectedRun = 77;
        Use(items, ItemKind.DivineIntervention);
        Check(stats.CardsSuppressed && !stats.HasMiniGun && !stats.HasSlowingAura && !stats.HasAscension(CardAscension.RecoveryPlus) && player.Movement.MaxRunSpeed < speed, "Divine suspends card stats and ascensions");
        Check(player.Movement.IsFlying && Mathf.Approximately(player.Weapon.EffectiveFireInterval, baseShotDelay * .1f) && player.Health.MaxHealth < originalMax, "Divine grants flight and reduced base shot delay with card health suspended");
        var basic = CardManager.Instance.AllCards.First(c => c.IsBasic && c.Modifiers.Any(m => m.StatType == StatType.Damage));
        int damage = player.Weapon.CurrentStats.Damage, picked = CardManager.Instance.GetRunPickups(basic.ID, stats);
        CardManager.Instance.ApplyCardEffect(basic, stats);
        Check(player.Weapon.CurrentStats.Damage == damage && CardManager.Instance.GetRunPickups(basic.ID, stats) == picked + 1, "cards selected during Divine are recorded once and deferred");
        Set(player.Health, "_currentHealth", 1); yield return new WaitForSeconds(.22f);
        Check(player.Health.CurrentHealth >= 3, "Divine regenerates ten health per second");
        stats.ReportCoinsGained(3);
        var chain = new List<EnemyBase>();
        Vector2 origin = player.Weapon.FirePoint.position;
        for (int i = 0; i < 6; i++) chain.Add(Enemy(origin + Vector2.right * (3 + i * 2), 100));
        items.FireDivineLightning();
        for (int i = 0; i < 6; i++) Check(Mathf.Abs(chain[i].HealthRemaining - chain[i].MaxHealth * (1 - .5f * Mathf.Pow(.9f, i))) < .01f, "Divine lightning chain damage hop " + i);
        LocalCoopSession.Instance.CompleteWave(); yield return null; Check(items.DivineActive, "Divine survives one completed wave");
        LocalCoopSession.Instance.CompleteWave(); yield return null;
        Check(!items.DivineActive && !stats.CardsSuppressed && stats.GlobalDamageBonus == .75f && stats.HasMiniGun && stats.HasAscension(CardAscension.RecoveryPlus) && player.Movement.MaxRunSpeed == speed && player.Health.MaxHealth == originalMax, "Divine restores original card stats at two waves");
        Check(player.Weapon.CurrentStats.Damage > damage && CardManager.Instance.GetRunPickups(basic.ID, stats) == picked + 1 && stats.CoinsCollectedRun == 80, "deferred card applies exactly once and run counters survive restoration");
        stats.HasSlowingAura = false; ClearEnemies();
        a = Enemy((Vector2)player.transform.position + Vector2.right * 40); Use(items, ItemKind.Nuke);
        yield return new WaitForSeconds(2);
        Check(a == null || !a.IsAlive, "falling nuke defeats off-screen enemies");
        var radiation = FindObjectsByType<ItemArea>(FindObjectsSortMode.None).Single(x => x.name == "Nuke");
        Check(radiation.GetComponentsInChildren<ParticleSystem>().Any(p => p.main.startColor.color.g > p.main.startColor.color.r), "radiation reuses fire particles tinted green");
        a = Enemy((Vector2)player.transform.position + Vector2.right * 30); yield return new WaitForSeconds(.3f);
        float factor = (float)typeof(EnemyBase).GetProperty("MovementSpeedFactor", Private).GetValue(a);
        Check(Mathf.Abs(factor - .5f) < .001f, "radiation slows newly spawned enemies by fifty percent");
        for (int i = 0; i < 9; i++) LocalCoopSession.Instance.CompleteWave(); yield return null; Check(radiation != null, "radiation survives nine waves");
        LocalCoopSession.Instance.CompleteWave(); yield return null; yield return null; Check(radiation == null, "radiation expires after ten waves"); ClearEnemies();
        Vector2 cursor = Camera.main.ScreenToWorldPoint(player.AimScreenPosition());
        a = Enemy(cursor); a.GetComponent<BoxCollider2D>().size = Vector2.one * 5;
        for (int i = 0; i < 4; i++) Use(items, ItemKind.Sawblade);
        yield return null; yield return null; float sawHealth = a.HealthRemaining;
        Check(items.SawbladeCount == 4 && sawHealth == a.MaxHealth - 4, "four sawblades each deal one contact damage");
        yield return new WaitForSeconds(.3f); Check(a.HealthRemaining == sawHealth, "each sawblade hits an enemy only once during an orbit");
        yield return new WaitForSeconds(1.4f); Check(a.HealthRemaining <= a.MaxHealth - 8 && a.HealthRemaining >= a.MaxHealth - 12, "sawblades hit again after a full orbit");
        var saws = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(s => s.name == "Item sawblade").ToArray();
        Check(saws.Length == 4 && Vector2.Distance((saws[0].transform.position + saws[2].transform.position) * .5f, cursor) < .15f, "four sawblades remain evenly spaced around the cursor");
        ScreenCapture.CaptureScreenshot("tmp/item-update/sawblades.png"); yield return null;
        stats.HasSecondWind = false; player.Health.TryTakeDamage(10000); yield return null;
        Check(items.SawbladeCount == 0, "sawblades are removed on player death");
    }
}
#endif
