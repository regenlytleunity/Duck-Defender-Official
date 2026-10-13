#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class ShopUpdatePlayProbe : MonoBehaviour
{
    public static string Result = "Not started";
    public bool UIOnly;
    public Action SeedUI;
    public Func<string> CheckUI;
    public Func<GameObject, string> CheckScope;
    public static string ReportPath => Path.Combine(Path.GetTempPath(), "duck-shop-update-verification.txt");
    readonly List<string> _checks = new List<string>();
    readonly List<Gamepad> _pads = new List<Gamepad>(), _realPads = new List<Gamepad>();
    void Check(bool condition, string description)
    { if (!condition) throw new InvalidOperationException(description); _checks.Add(description); Result = "Running: " + description; }
    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(SaveSystem.VerificationSavePath)) throw new InvalidOperationException("Isolated save required");
        foreach (var pad in Gamepad.all.ToArray()) if (pad.enabled) { _realPads.Add(pad); InputSystem.DisableDevice(pad); }
        var tests = UIOnly ? RunUI() : Run();
        while (true)
        {
            object current;
            try { if (!tests.MoveNext()) break; current = tests.Current; }
            catch (Exception ex) { Result = "FAILED: " + ex; File.WriteAllText(ReportPath, Result + "\n" + string.Join("\n", _checks)); Debug.LogError(Result); Destroy(gameObject); yield break; }
            yield return current;
        }
        Result = "PASS: " + _checks.Count + (UIOnly ? " UI interaction checks" : " gameplay checks");
        File.WriteAllText(ReportPath, Result + "\n" + string.Join("\n", _checks)); Debug.Log(Result);
        Destroy(gameObject);
    }
    static void QuietWave()
    {
        if (EnemyTipUI.Instance != null) { EnemyTipUI.Instance.Dismiss(); EnemyTipUI.Instance.enabled = false; }
        WaveManager.Instance.StopAllCoroutines(); WaveManager.Instance.enabled = false;
        foreach (var enemy in EnemyBase.ActiveEnemies.ToArray()) if (enemy != null) Destroy(enemy.gameObject);
    }
    EnemyBase Enemy(Vector3 position, float health = 100)
    {
        var go = new GameObject("Item verification enemy"); go.transform.position = position;
        go.AddComponent<Rigidbody2D>().gravityScale = 0; go.AddComponent<BoxCollider2D>();
        var enemy = go.AddComponent<SwarmerEnemy>(); enemy.SpawnProtectionSeconds = 0; enemy.BaseHealth = health; enemy.XPValue = 0;
        enemy.BaseSpeed = 0; enemy.SeparationForce = 0; enemy.DamageOnHit = 0;
        enemy.Initialize(1); return enemy;
    }
    void Use(PlayerItems items, ItemKind kind)
    {
        items.Slots[0] = (int)kind; items.Select(0); int before = SaveSystem.LoadData().ItemInventory[(int)kind];
        Check(items.UseSelected(), "activate " + kind);
        Check(items.Slots[0] == 0 && SaveSystem.LoadData().ItemInventory[(int)kind] == before - 1, "consume exactly one " + kind);
        Check(!items.UseSelected(), "empty slot does not repeat " + kind);
    }
    IEnumerator Run()
    {
        LocalCoopSession.RequestedPlayers = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene"); yield return null; yield return null; yield return new WaitForSeconds(.15f);
        QuietWave();
        var player = LocalCoopSession.Instance.Players[0]; player.AssignController(null, false);
        var items = player.Stats.Items;
        Check(items != null && items.Slots[0] == 1, "solo loadout initialized after component Start");
        Check(player.Stats.PermanentDamage == .5f && player.Stats.PermanentLuck == .5f && player.Health.MaxHealth > 5, "permanent bonuses applied");
        Check(CardManager.Instance.GetRunPickups("mun_faster_firing", player.Stats) == 1, "starting card applied once");
        for (int i = 0; i < 20; i++) Check(CardManager.Instance.GetRandomCards(3, player.Stats).All(c => c.ID != "mun_faster_firing"), "starting card excluded from offers " + i);
        player.Health.TryTakeDamage(2); int injured = player.Health.CurrentHealth;
        Use(items, ItemKind.HealthPotion); Check(player.Health.CurrentHealth == player.Health.MaxHealth && injured < player.Health.MaxHealth, "health potion fully heals");
        float baseDamage = player.Weapon.FeatherDamage(); float baseSpeed = player.Movement.EffectiveMaxSpeed; float baseInterval = player.Weapon.EffectiveFireInterval;
        items.ApplyCasino(0); Check(player.Weapon.FeatherDamage() > baseDamage, "casino damage bonus");
        items.ApplyCasino(1); Check(player.Movement.EffectiveMaxSpeed > baseSpeed, "casino speed bonus");
        items.ApplyCasino(2); Check(player.Weapon.EffectiveFireInterval < baseInterval, "casino fire rate bonus");
        yield return new WaitForSeconds(player.Health.InvulnerabilityTime + .5f);
        Check(player.Health.TryTakeDamage(2), "regeneration test starts with injured player"); injured = player.Health.CurrentHealth; items.ApplyCasino(3);
        yield return new WaitForSeconds(5.1f); Check(player.Health.CurrentHealth == injured + 1, "casino regeneration heals one health every five seconds");
        items.ApplyCasino(4); Check(player.Weapon.FeatherDamage() == 1, "casino sets damage to one");
        items.ApplyCasino(5); Check(player.Movement.EffectiveMaxSpeed == 0, "casino immobilizes");
        items.ApplyCasino(6); Check(PlayerItems.SuppressEnemyCoins, "casino suppresses enemy coins");
        items.ApplyCasino(7); Check(PlayerItems.TeamXPFactor == .75f, "casino reduces shared XP");
        player.Stats.GlobalDamageBonus += .1f;
        LocalCoopSession.Instance.CompleteWave(); Check(items.CasinoActive, "casino survives first wave completion");
        LocalCoopSession.Instance.CompleteWave(); Check(!items.CasinoActive && player.Stats.GlobalDamageBonus == .1f && player.Movement.EffectiveMaxSpeed == baseSpeed, "casino expires without erasing gained stats");
        Use(items, ItemKind.PocketCasino); Check(items.SpinUntil > Time.time + 2.8f, "casino spin lasts three seconds");
        yield return new WaitForSeconds(3.1f); Check(items.CasinoActive && items.ResultUntil > Time.time, "casino resolves and displays result");
        LocalCoopSession.Instance.CompleteWave(); LocalCoopSession.Instance.CompleteWave();
        Use(items, ItemKind.BattleBanner); float withBanner = player.Weapon.EffectiveFireInterval;
        Check(ItemArea.BannerBonus(player.transform.position) == .15f && withBanner < baseInterval, "banner grants nearby fire rate and damage");
        for (int i = 0; i < 9; i++) LocalCoopSession.Instance.CompleteWave();
        Check(ItemArea.BannerBonus(player.transform.position) > 0, "banner survives nine completions");
        LocalCoopSession.Instance.CompleteWave(); yield return null;
        Check(ItemArea.BannerBonus(player.transform.position) == 0, "banner expires after ten completions");
        Use(items, ItemKind.Shield);
        var inside = Enemy(player.transform.position + Vector3.right);
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Check(Vector2.Distance(inside.transform.position, player.transform.position) >= 2.4f, "shield expels enemies");
        Check(ItemArea.BlocksSegment(player.transform.position + Vector3.left * 10, player.transform.position + Vector3.right * 10), "shield blocks swept projectile crossing");
        var bullet = new GameObject("Shield verification projectile").AddComponent<EnemyProjectile>(); bullet.transform.position = player.transform.position; yield return null;
        Check(!bullet.gameObject.activeSelf, "shield deactivates enemy projectile"); Destroy(bullet.gameObject);
        Destroy(inside.gameObject);
        Use(items, ItemKind.CloneProjector); yield return null; yield return null;
        var clones = FindObjectsByType<ItemClone>(FindObjectsSortMode.None);
        Check(clones.Length == 2 && LocalCoopSession.Instance.Players.Count == 1, "two clones do not become real players");
        Check(clones.All(c => c.GetComponent<PlayerHealth>().MaxHealth == player.Health.MaxHealth), "clones copy max health");
        Check(clones.All(c => Mathf.Abs(c.GetComponent<WeaponPlayer>().EffectiveFireInterval - player.Weapon.EffectiveFireInterval * 2) < .001f), "clones fire at half rate");
        var target = Enemy(player.transform.position + Vector3.right * 4); yield return new WaitForSeconds(.3f);
        Check(clones.Any(c => c.Shooting), "clone AI finds a target");
        Check(LocalCoopSession.NearestAlive(clones[0].transform.position) == player.Stats, "clone does not capture camera or pickups");
        clones[0].GetComponent<PlayerHealth>().TryTakeDamage(10000); yield return null;
        Check(!LocalCoopSession.Instance.GameOver && !player.Health.IsDead, "clone death does not end run");
        LocalCoopSession.Instance.CompleteWave(); LocalCoopSession.Instance.CompleteWave(); yield return null; yield return null;
        Check(FindObjectsByType<ItemClone>(FindObjectsSortMode.None).Length == 0, "clones expire after two waves");
        if (target != null) Destroy(target.gameObject);
        var far = Enemy(player.transform.position + Vector3.right * 35); far.SpawnProtectionSeconds = 3; far.Initialize(1);
        Check(far.IsSpawnProtected, "off-screen test enemy has normal spawn protection"); float beforeHealth = far.HealthRemaining;
        Use(items, ItemKind.Flamethrower); yield return new WaitForSeconds(1.1f);
        Check(far.HealthRemaining < beforeHealth && far.HealthRemaining >= beforeHealth - 7, "flames deal about five DPS off screen");
        Destroy(far.gameObject);
        var bombTarget = Enemy(player.transform.position + Vector3.up * 1.5f);
        ItemArea.LobBomb(player.Stats, Vector2.zero); yield return new WaitForSeconds(1.4f);
        Check(bombTarget == null || !bombTarget.IsAlive, "bomb instantly defeats enemies in blast");
        var data = SaveSystem.LoadData(); int collected = data.ItemInventory[1];
        var pickup = ItemPickup.Spawn(player.transform.position + Vector3.right * 10, 1);
        yield return new WaitForSeconds(.6f); Check(pickup != null, "item pickup ignores coin magnet");
        pickup.transform.position = player.transform.position; yield return null;
        Check(SaveSystem.LoadData().ItemInventory[1] == collected + 1, "item pickup grants persistent inventory");
        var mobile = MobileInputController.Instance; mobile.AutoDetectPlatform = false; mobile.IsMobileEnabled = true;
        yield return null; yield return null;
        var touch = GameObject.Find("Touch items"); Check(touch != null, "mobile item controls are visible");
        touch.transform.GetChild(2).GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Check(items.SelectedSlot == 2, "mobile slot button selects item");
        items.Slots[2] = (int)ItemKind.HealthPotion; int potionStock = SaveSystem.LoadData().ItemInventory[2];
        touch.transform.GetChild(4).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(items.Slots[2] == 0 && SaveSystem.LoadData().ItemInventory[2] == potionStock - 1, "mobile Use consumes selected item");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-shop-item-hud.png")); yield return null;
        mobile.IsMobileEnabled = false;
        int level = LevelManager.Instance.CurrentLevel;
        Use(items, ItemKind.ExperiencePotion); Check(LevelManager.Instance.CurrentLevel == level + 1, "XP potion grants exactly one level");
        Time.timeScale = 1;
        // A second scene validates shared host stock and per-player controller ownership.
        data = SaveSystem.LoadData(); data.ItemInventory[2] = 2; data.CoopLoadouts[0].Items[0] = data.CoopLoadouts[1].Items[0] = 2;
        data.CoopLoadouts[0].Cards[0] = "mun_faster_firing"; data.CoopLoadouts[1].Cards[0] = "mob_strong_legs"; SaveSystem.SaveData(data);
        for (int i = 0; i < 2; i++) { var pad = InputSystem.AddDevice<Gamepad>(); _pads.Add(pad); LocalCoopSession.PlayerControllers[i] = pad; }
        LocalCoopSession.RequestedPlayers = 2; LocalCoopSession.AdvancedLoadouts = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene"); yield return null; yield return null; yield return new WaitForSeconds(.15f); QuietWave();
        var a = LocalCoopSession.Instance.Players[0]; var b = LocalCoopSession.Instance.Players[1];
        Check(a.Stats.Items.Slots[0] == 2 && b.Stats.Items.Slots[0] == 2, "co-op equips two separately reserved copies");
        Check(CardManager.Instance.GetRunPickups("mun_faster_firing", a.Stats) == 1 && CardManager.Instance.GetRunPickups("mob_strong_legs", b.Stats) == 1, "per-player starting cards");
        InputSystem.QueueStateEvent(_pads[1], new GamepadState().WithButton(GamepadButton.DpadRight)); yield return null; yield return null;
        Check(a.Stats.Items.SelectedSlot == 0 && b.Stats.Items.SelectedSlot == 1 && b.Move == Vector2.zero, "P2 D-pad selects only P2 and does not move");
        InputSystem.QueueStateEvent(_pads[1], new GamepadState()); yield return null;
        b.Stats.Items.Select(0);
        InputSystem.QueueStateEvent(_pads[1], new GamepadState().WithButton(GamepadButton.RightShoulder)); yield return null; yield return null;
        Check(a.Stats.Items.Slots[0] == 2 && b.Stats.Items.Slots[0] == 0 && SaveSystem.LoadData().ItemInventory[2] == 1, "P2 RB consumes only P2 reserved copy");
        InputSystem.QueueStateEvent(_pads[1], new GamepadState()); yield return null;
        LocalCoopSession.AdvancedLoadouts = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene"); yield return null; yield return null; yield return new WaitForSeconds(.15f); QuietWave();
        Check(LocalCoopSession.Instance.Players.All(p => p.Stats.Items.Slots.All(i => i == 0)), "Simple mode disables item loadouts");
        Check(CardManager.Instance.GetRunPickups("mun_faster_firing", LocalCoopSession.Instance.Players[0].Stats) == 0 && SaveSystem.LoadData().CoopLoadouts[0].Cards[0] == "mun_faster_firing", "Simple mode disables starting cards and preserves selections");
        data = SaveSystem.LoadData(); int runs = data.CompletedRuns, unused = data.ItemInventory[2];
        foreach (var seat in LocalCoopSession.Instance.Players) seat.Health.TryTakeDamage(10000); yield return null;
        Check(LocalCoopSession.Instance.GameOver && SaveSystem.LoadData().CompletedRuns == runs + 1, "team defeat records one completed game");
        LocalCoopSession.Instance.CompleteRun();
        Check(SaveSystem.LoadData().CompletedRuns == runs + 1 && SaveSystem.LoadData().ItemInventory[2] == unused, "run completion is idempotent and retains unused inventory");
        Time.timeScale = 1;
        LocalCoopSession.RequestedPlayers = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
    }
    void OnDestroy()
    {
        foreach (var pad in _pads) if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        foreach (var pad in _realPads) if (pad != null && pad.added) InputSystem.EnableDevice(pad);
    }

    void Click(string label)
    {
        MainMenuUI.Instance.NavigationScope.GetComponentsInChildren<UnityEngine.UI.Button>()
            .First(b => b.GetComponentInChildren<TMPro.TMP_Text>()?.text == label).onClick.Invoke();
    }
    void Visible(string label)
    {
        string result = CheckUI(); Check(result.StartsWith("PASS"), label + ": " + result);
    }
    IEnumerator RunUI()
    {
        SeedUI(); var menu = MainMenuUI.Instance; var shop = ShopManager.Instance;
        var profile = shop.Progress;
        profile.StartingCardSlots = 3; profile.PackOpeningTier = 0;
        profile.PermanentUpgrades = new[] { 1, 2, 3, 4, 5 };
        foreach (var card in profile.CardCollection) card.Duplicates = 100;
        foreach (CardPackType pack in new[] { CardPackType.Mobility, CardPackType.Munitions, CardPackType.Survival, CardPackType.Gadget }) profile.AddEssence(pack, 2000);
        SaveSystem.SaveData(profile); shop.LoadEconomy();
        for (int page = 0; page < 3; page++)
        {
            menu.OpenShop(); menu.GetComponent<ShopUpdateUI>().ShowPage(page); yield return null; Visible("shop page " + page);
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-polish-shop-" + page + ".png")); yield return null;
        }
        shop.Progress.StartingCardSlots = 0; menu.OpenIndex(); yield return null;
        menu.IndexPanel.GetComponent<IndexLoadoutUI>().Refresh(); yield return null;
        Visible("collection without starting slots");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-polish-index-no-slots.png")); yield return null;
        shop.Progress.StartingCardSlots = 5; shop.Progress.PackOpeningTier = 2;
        menu.IndexPanel.GetComponent<IndexLoadoutUI>().Refresh(); yield return null;
        menu.OpenIndex(); yield return null; Visible("collection with starting slots");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-polish-index-cards.png")); yield return null;
        var collection = menu.IndexPanel.GetComponent<CardIndexUI>();
        var upgradeCard = collection.ContentArea.GetComponentsInChildren<CardDisplay>().First(c => c.UpgradeButton.IsInteractable());
        var upgradeRect = (RectTransform)upgradeCard.UpgradeButton.transform;
        var upgradePointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
            button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
            pointerPressRaycast = new UnityEngine.EventSystems.RaycastResult { gameObject = upgradeCard.UpgradeCostText.gameObject },
            pointerDrag = upgradeCard.gameObject
        };
        upgradeCard.GetComponent<LoadoutInteraction>().OnInitializePotentialDrag(upgradePointer);
        Check(upgradePointer.pointerDrag == null, "upgrade pointer gesture cannot start a parent card drag");
        int oldLevel = shop.GetCardData(upgradeCard.AssignedCard.ID).Level, oldCoins = shop.CurrentCoins;
        upgradeCard.UpgradeButton.OnPointerClick(upgradePointer); yield return null;
        Check(shop.GetCardData(upgradeCard.AssignedCard.ID).Level == oldLevel + 1 && shop.CurrentCoins == oldCoins - upgradeCard.AssignedCard.GetUpgradeCost(oldLevel), "pointer upgrade still advances card and charges exactly once");
        Check(menu.NavigationScope == menu.IndexPanel, "upgrading does not open an equip modal");
        var events = UnityEngine.EventSystems.EventSystem.current;
        events.SetSelectedGameObject(collection.LayoutButton.gameObject);
        UnityEngine.EventSystems.ExecuteEvents.Execute(collection.LayoutButton.gameObject, new UnityEngine.EventSystems.AxisEventData(events) { moveDir = UnityEngine.EventSystems.MoveDirection.Right, moveVector = Vector2.right }, UnityEngine.EventSystems.ExecuteEvents.moveHandler);
        Check(events.currentSelectedGameObject == collection.NextButton.gameObject, "controller can reach next-page arrow from Layout on first page");
        collection.NextButton.OnSubmit(new UnityEngine.EventSystems.BaseEventData(events)); yield return null;
        Check(collection.CurrentPage == 1, "controller submit turns index page"); Visible("index middle page");
        while (collection.NextButton.IsInteractable()) { collection.NextPage(); yield return null; }
        Visible("index last page");
        while (collection.PreviousButton.IsInteractable()) { collection.PreviousPage(); yield return null; }
        for (int layout = 0; layout < 3; layout++) { collection.CycleLayout(); yield return null; Visible("index layout " + layout); }
        var index = menu.IndexPanel.GetComponent<IndexLoadoutUI>(); index.ShowItems(); yield return null; Visible("item inventory");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-polish-index-items.png")); yield return null;
        menu.IndexPanel.GetComponentsInChildren<LoadoutInteraction>().First(h => !h.IsCard && h.Slot == -1).ContextMenu();
        yield return null; Visible("item context menu"); Click("Equip to slot 2"); yield return null;
        Check(shop.Loadout().Items[1] == 1, "context menu equips one bomb into slot 2");
        var hooks = menu.IndexPanel.GetComponentsInChildren<LoadoutInteraction>();
        var source = hooks.First(h => !h.IsCard && h.Slot == 1); var target = hooks.First(h => !h.IsCard && h.Slot == 2);
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        source.OnBeginDrag(pointer); target.OnDrop(pointer); source.OnEndDrag(pointer); yield return null;
        Check(shop.Loadout().Items[2] == 1 && shop.Loadout().Items[1] == 3, "drag swaps equipped slots without losing inventory");
        source = menu.IndexPanel.GetComponentsInChildren<LoadoutInteraction>().First(h => !h.IsCard && h.Slot == 2);
        source.OnBeginDrag(pointer); source.OnEndDrag(pointer); yield return null;
        Check(shop.Loadout().Items[2] == 0, "drag outside unequips");
        index.ShowCards(); yield return null;
        var display = menu.IndexPanel.GetComponentsInChildren<CardDisplay>().First(d => d.AssignedCard != null);
        string id = display.AssignedCard.ID; display.ClickButton.onClick.Invoke(); yield return null;
        Visible("starting card context menu"); Click("Equip to slot 2"); yield return null;
        Check(shop.Loadout().Cards[1] == id, "card context menu equips a starting card");
        menu.IndexPanel.GetComponentsInChildren<LoadoutInteraction>().First(h => h.IsCard && h.Slot == 1).ContextMenu();
        yield return null; Click("Unequip"); yield return null; Check(string.IsNullOrEmpty(shop.Loadout().Cards[1]), "starting card unequips");
        foreach (int count in new[] { 1, 3, 6, 1 })
        {
            menu.OpenShop(); menu.GetComponent<ShopUpdateUI>().ShowPage(0); yield return null;
            menu.PackContainer.GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke(); yield return null; Visible("pack purchase confirmation");
            if (count == 1) { ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-polish-open-pack.png")); yield return null; }
            int before = shop.CurrentCoins;
            if (count == 1) menu.YesButton.onClick.Invoke(); else if (count == 3) menu.BuyThreeButton.onClick.Invoke(); else menu.BuySixButton.onClick.Invoke();
            yield return null;
            Check(shop.CurrentCoins == before - shop.AvailablePacks[0].Cost * count, "purchase charges for " + count + " packs");
            var packs = new List<UnityEngine.UI.Image> { menu.PackImage }; packs.AddRange(menu.AdditionalPackImages.Where(p => p.gameObject.activeSelf));
            Check(packs.Count == count, "visible pack count " + count);
            if (count == 6)
            {
                Check(packs.Select(p => Mathf.RoundToInt(p.transform.position.y)).Distinct().Count() == 2, "six packs use two rows");
                ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-shop-six-packs.png")); yield return null;
            }
            menu.SendMessage("CompleteSlice"); float deadline = Time.unscaledTime + 12;
            while (!menu.ContinueButton.gameObject.activeSelf && Time.unscaledTime < deadline) yield return null;
            Check(menu.ContinueButton.gameObject.activeSelf, "reveal animation completes for " + count);
            Check(menu.CardRevealCenter.GetComponentsInChildren<CardDisplay>().Length == count * 3, "all cards revealed for " + count);
            foreach (var card in menu.CardRevealCenter.GetComponentsInChildren<CardDisplay>())
            {
                var rect = (RectTransform)card.transform; var slot = (RectTransform)rect.parent;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Check(corners.All(c => { Vector3 p = slot.InverseTransformPoint(c); return p.x >= slot.rect.xMin - 1 && p.x <= slot.rect.xMax + 1 && p.y >= slot.rect.yMin - 1 && p.y <= slot.rect.yMax + 1; }), "revealed card fits its grid cell");
            }
            Check(menu.RevealGrid.spacing.y >= 0, "reveal rows have no negative spacing");
            Visible("revealed cards " + count);
            if (count == 6) { ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-shop-six-reveal.png")); yield return null; }
            menu.ContinueButton.onClick.Invoke(); yield return null; Check(menu.ShopPanel.activeSelf && !menu.OpeningOverlay.activeSelf, "continue returns to shop");
        }
        for (int i = 0; i < 4; i++) _pads.Add(InputSystem.AddDevice<Gamepad>());
        LocalCoopSession.RequestedPlayers = 4; LocalCoopSession.AdvancedLoadouts = true; LocalCoopSession.KeyboardTest = false;
        var host = menu.GetComponent<HostMenuUI>(); host.Open(); host.Hide();
        var lobby = menu.GetComponent<CoopLobbyUI>(); if (lobby == null) lobby = menu.gameObject.AddComponent<CoopLobbyUI>(); lobby.Open(host);
        for (int i = 0; i < 4; i++) LocalCoopSession.PlayerControllers[i] = _pads[i];
        yield return new WaitForSecondsRealtime(.4f);
        InputSystem.QueueStateEvent(_pads[1], new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
        InputSystem.QueueStateEvent(_pads[1], new GamepadState()); yield return null;
        InputSystem.QueueStateEvent(_pads[1], new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
        InputSystem.QueueStateEvent(_pads[1], new GamepadState()); yield return null;
        InputSystem.QueueStateEvent(_pads[1], new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
        InputSystem.QueueStateEvent(_pads[1], new GamepadState()); yield return null;
        var panels = (LoadoutPanel[])typeof(CoopLobbyUI).GetField("_loadouts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(lobby);
        Check(panels[1].Root.gameObject.activeSelf && !panels[0].Root.gameObject.activeSelf, "P2 controller opens only its own lobby equipment");
        panels[1].ShowCards(true); yield return null;
        Check(panels[1].Root.GetComponentsInChildren<LoadoutInteraction>().Count(h => h.Slot == -1 && h.IsCard) == 6, "lobby card page contains six choices in three columns");
        Check(CheckScope(panels[1].Root.gameObject).StartsWith("PASS"), "four-player card pane text, pointer targets and controller graph");
        var p1Focus = panels[0].Focused;
        InputSystem.QueueStateEvent(_pads[1], new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
        InputSystem.QueueStateEvent(_pads[1], new GamepadState()); yield return null;
        Check(panels[0].Focused == p1Focus, "P2 navigation does not move P1 focus");
        panels[1].ShowCards(false); yield return null;
        Check(CheckScope(panels[1].Root.gameObject).StartsWith("PASS"), "four-player item pane text, pointer targets and controller graph");
        for (int i = 0; i < 4; i++) { panels[i].Root.gameObject.SetActive(true); panels[i].ShowCards(i % 2 == 0); }
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-shop-four-player-lobby.png")); yield return null;
        panels[1].ControllerInput(Vector2.zero, false, true); yield return null;
        Check(!panels[1].Root.gameObject.activeSelf, "controller Back closes equipment");
        lobby.Close(); LocalCoopSession.RequestedPlayers = 2; lobby.Open(host);
        panels = (LoadoutPanel[])typeof(CoopLobbyUI).GetField("_loadouts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(lobby);
        for (int i = 0; i < 2; i++) { panels[i].Root.gameObject.SetActive(true); panels[i].ShowCards(i == 0); }
        yield return null;
        for (int i = 0; i < 2; i++) Check(CheckScope(panels[i].Root.gameObject).StartsWith("PASS"), "two-player pane " + i + " text, pointer targets and controller graph");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-polish-two-player-lobby.png")); yield return null;
        lobby.Close(); host.Open(); LocalCoopSession.RequestedPlayers = 1;
    }
}
#endif
