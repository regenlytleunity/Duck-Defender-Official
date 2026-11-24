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
    public bool ControllerOnly;
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
        var tests = ControllerOnly ? RunController() : UIOnly ? RunUI() : Run();
        while (true)
        {
            object current;
            try { if (!tests.MoveNext()) break; current = tests.Current; }
            catch (Exception ex) { Result = "FAILED: " + ex; File.WriteAllText(ReportPath, Result + "\n" + string.Join("\n", _checks)); Debug.LogError(Result); Destroy(gameObject); yield break; }
            yield return current;
        }
        Result = "PASS: " + _checks.Count + (ControllerOnly ? " controller regression checks" : UIOnly ? " UI interaction checks" : " gameplay checks");
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
        Check(CardManager.Instance.GetRunPickups("mun_faster_firing") == 1, "solo compatibility API shares the starting-card history");
        for (int i = 0; i < 20; i++) Check(CardManager.Instance.GetRandomCards(3).All(c => c.ID != "mun_faster_firing"), "solo level-up API excludes starting card " + i);
        LevelUpUI.Instance.ShowLevelUpOptions();
        var offered = LevelUpUI.Instance.CardContainer.GetComponentsInChildren<CardDisplay>();
        Check(offered.Length == 3 && offered.All(c => c.AssignedCard.ID != "mun_faster_firing"), "actual solo level-up UI excludes starting card");
        LevelUpUI.Instance.SelectCard(offered[0].AssignedCard); yield return null;
        float fireInterval = player.Weapon.FireRate;
        CardManager.Instance.ApplyCardEffect(CardManager.Instance.AllCards.Find(c => c.ID == "mun_faster_firing"));
        Check(Mathf.Approximately(fireInterval, player.Weapon.FireRate), "solo repeat cannot reapply starting-card effect");

        var world = WorldCamera.Instance; var playerBody = player.GetComponent<Rigidbody2D>();
        var groundPrefab = WaveManager.Instance.EnemyPrefabs.First(p => p.GetComponent<EnemyBase>().EnemyKind == "ground");
        foreach (float edge in new[] { world.Left + 2, world.Right - 2 })
        {
            playerBody.position = new Vector2(edge, world.Floor + 1); playerBody.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(.3f);
            var spawned = new List<EnemyBase>();
            for (int i = 0; i < 8; i++)
            {
                Check(world.TrySpawnPosition(groundPrefab, out var position, i % 2 == 0), "edge spawn has available terrain " + edge + "/" + i);
                var enemy = Instantiate(groundPrefab, position, Quaternion.identity).GetComponent<EnemyBase>();
                enemy.IsElite = i % 2 == 0; enemy.Initialize(1);
                enemy.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
                Physics2D.SyncTransforms();
                Check(spawned.All(e => !e.BodyCollider.bounds.Intersects(enemy.BodyCollider.bounds)), "edge spawn avoids body overlap " + i);
                Check(!enemy.IsInView(false), "edge spawn stays outside viewport " + i);
                spawned.Add(enemy);
            }
            foreach (var enemy in spawned) Destroy(enemy.gameObject);
            float side = edge < 0 ? -1 : 1;
            var walker = Instantiate(groundPrefab, new Vector3(edge + side * 4, world.Floor + 1, 0), Quaternion.identity).GetComponent<SwarmerEnemy>();
            walker.DamageOnHit = 0; walker.CoinPrefab = null; walker.XPValue = 0; walker.Initialize(1);
            float highest = walker.transform.position.y;
            float end = Time.time + 1.6f;
            while (Time.time < end) { highest = Mathf.Max(highest, walker.transform.position.y); yield return new WaitForFixedUpdate(); }
            Check(highest < world.Floor + 1.3f, "ground enemy crosses player boundary without jumping " + edge);
            Destroy(walker.gameObject);
        }
        playerBody.position = new Vector2(0, world.Floor + 1); playerBody.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(.3f);
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
        Check(clones.Length == 1 && LocalCoopSession.Instance.Players.Count == 1, "one clone does not become a real player");
        Check(clones.All(c => c.GetComponent<PlayerHealth>().MaxHealth == player.Health.MaxHealth), "clones copy max health");
        Check(clones.All(c => Mathf.Abs(c.GetComponent<WeaponPlayer>().EffectiveFireInterval - player.Weapon.EffectiveFireInterval) < .001f), "clones copy the full fire rate");
        Check(clones.All(c => Mathf.Approximately(c.GetComponent<PlayerController>().EffectiveMaxSpeed, player.Movement.EffectiveMaxSpeed)), "clones snapshot upgraded player movement speed");
        Check(clones.All(c => Physics2D.GetIgnoreCollision(c.GetComponent<Collider2D>(), player.GetComponent<Collider2D>())), "active clones ignore the owner collider");
        float clonedSpeed = clones[0].SpawnMoveSpeed; player.Stats.RunSpeedBonus += .5f;
        Check(Mathf.Approximately(clones[0].GetComponent<PlayerController>().EffectiveMaxSpeed, clonedSpeed), "later player speed changes do not alter clone snapshot");
        player.Stats.RunSpeedBonus -= .5f;
        var movingClone = clones[0];
        movingClone.GetComponent<Rigidbody2D>().position = playerBody.position + Vector2.left * 4;
        yield return new WaitForSeconds(.2f);
        Check(movingClone.GetComponent<Rigidbody2D>().linearVelocity.x > clonedSpeed * .75f, "clone accelerates to player-like running speed");
        var target = Enemy(player.transform.position + Vector3.right * 4); yield return new WaitForSeconds(.3f);
        Check(clones.Any(c => c.Shooting), "clone AI finds a target");
        Check(LocalCoopSession.NearestAlive(clones[0].transform.position) == player.Stats, "clone does not capture camera or pickups");
        clones[0].GetComponent<PlayerHealth>().TryTakeDamage(10000); yield return null;
        Check(!LocalCoopSession.Instance.GameOver && !player.Health.IsDead, "clone death does not end run");
        Use(items, ItemKind.CloneProjector); yield return null;
        for (int i = 0; i < 4; i++) LocalCoopSession.Instance.CompleteWave(); yield return null;
        Check(FindObjectsByType<ItemClone>(FindObjectsSortMode.None).Length == 1, "clone survives four waves");
        LocalCoopSession.Instance.CompleteWave(); yield return null; yield return null;
        Check(FindObjectsByType<ItemClone>(FindObjectsSortMode.None).Length == 0, "clone expires after five waves");
        if (target != null) Destroy(target.gameObject);
        var far = Enemy(player.transform.position + Vector3.right * 35); far.SpawnProtectionSeconds = 3; far.Initialize(1);
        Check(far.IsSpawnProtected, "off-screen test enemy has normal spawn protection"); float beforeHealth = far.HealthRemaining;
        Use(items, ItemKind.Flamethrower); yield return new WaitForSeconds(1.1f);
        Check(far.HealthRemaining < beforeHealth - far.MaxHealth * .08f && far.HealthRemaining >= beforeHealth - far.MaxHealth * .15f, "flames deal ten percent maximum HP per second off screen");
        var mapFire = FindObjectsByType<ItemArea>(FindObjectsSortMode.None).First(a => a.name == "Flamethrower");
        var patches = mapFire.GetComponentsInChildren<FireTrailPatch>();
        Check(patches.Length > 30 && patches.All(p => !p.enabled && Mathf.Abs(p.transform.position.y - world.Floor) < .2f), "map flames reuse fire-trail prefab on the ground");
        Check(patches.All(p => p.GetComponentsInChildren<ParticleSystem>().All(ps => ps.main.loop)), "map fire continues for the item lifetime");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-balance-ground-flames.png")); yield return null;
        Destroy(far.gameObject);
        var bombTarget = Enemy(ItemArea.ThrowPosition(player.transform.position, Vector2.right, .25f));
        ItemArea.LobBomb(player.Stats, Vector2.zero); yield return new WaitForSeconds(1.4f);
        Check(bombTarget == null || !bombTarget.IsAlive, "bomb instantly defeats enemies in blast");
        var data = SaveSystem.LoadData(); int collected = data.ItemInventory[1];
        var pickup = ItemPickup.Spawn(player.transform.position + Vector3.right * 10 + Vector3.up * 5, 1);
        float dropY = pickup.transform.position.y;
        yield return new WaitForSeconds(.6f); Check(pickup != null, "item pickup ignores coin magnet");
        Check(pickup.transform.position.y < dropY - 1, "flying-enemy item drops fall under gravity");
        yield return new WaitForSeconds(1);
        Check(pickup.GetComponent<Collider2D>().bounds.min.y >= world.Floor - .1f, "item drop settles above ground");
        pickup.transform.position = player.transform.position; yield return null;
        Check(SaveSystem.LoadData().ItemInventory[1] == collected + 1, "item pickup grants persistent inventory");
        var popup = GameObject.Find("Item HUD 1").GetComponentsInChildren<TMPro.TMP_Text>().FirstOrDefault(t => t.text == "+1 Bomb");
        Check(popup != null && popup.font.name == "DuckDefenderTestFontv2" && popup.color.r == 1 && popup.color.g == 1 && popup.color.b == 1, "pickup shows item name with original font and white infill");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-balance-item-popup.png")); yield return null;

        var throwPad = InputSystem.AddDevice<Gamepad>(); _pads.Add(throwPad); player.AssignController(throwPad, true);
        items.Slots[0] = (int)ItemKind.Bomb; items.Select(0);
        int bombStock = SaveSystem.LoadData().ItemInventory[1];
        InputSystem.QueueStateEvent(throwPad, new GamepadState { rightStick = new Vector2(1, .6f) }.WithButton(GamepadButton.RightShoulder));
        yield return null; yield return null; yield return new WaitForSeconds(.2f);
        var line = player.transform.Find("Item throw path").GetComponent<LineRenderer>();
        Check(items.IsAimingThrowable && line.enabled && line.positionCount > 3 && SaveSystem.LoadData().ItemInventory[1] == bombStock, "holding RB previews without consuming bomb");
        Vector2 predicted = ItemArea.ThrowPosition(player.transform.position, player.Weapon.AimDirection, ItemArea.BombFlightTime / 32);
        Check(Vector2.Distance(line.GetPosition(1), predicted) < .02f, "trajectory preview matches launch formula");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-balance-throw-preview.png")); yield return null;
        InputSystem.QueueStateEvent(throwPad, new GamepadState { rightStick = new Vector2(1, .6f) }); yield return null; yield return null;
        Check(!items.IsAimingThrowable && !line.enabled && items.Slots[0] == 0 && SaveSystem.LoadData().ItemInventory[1] == bombStock - 1, "releasing RB throws exactly one bomb");
        player.AssignController(null, false);
        items.Slots[0] = (int)ItemKind.Bomb; items.Select(0); items.BeginUse(true); items.Select(1);
        Check(!items.IsAimingThrowable && items.Slots[0] == 1, "switching item cancels a pending throw without consumption");
        items.Select(0); items.BeginUse(true); Time.timeScale = 0; yield return null;
        Check(!items.IsAimingThrowable && items.Slots[0] == 1, "pausing cancels a pending throw"); Time.timeScale = 1;
        items.BeginUse(true); items.ReleaseUse();
        Check(items.Slots[0] == 0, "touch hold/release shares throwable behavior");
        Use(items, ItemKind.PocketCasino);
        yield return new WaitForSeconds(1);
        var indicator = GameObject.Find("Casino indicator");
        var tick = indicator.transform.Find("Landing tick");
        Check(tick != null && tick.localEulerAngles == Vector3.zero, "casino landing tick stays fixed while wheel rotates");
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "duck-balance-casino.png")); yield return null;
        yield return new WaitForSeconds(2.1f);
        Check((items.CasinoEffect < 4) == (items.SpinEndAngle < 288), "casino lands in the color matching its applied outcome");
        Check(Mathf.Abs(Mathf.DeltaAngle(indicator.transform.Find("Casino wheel").localEulerAngles.z, items.SpinEndAngle)) < .1f, "casino visibly stops at its rolled outcome");
        var mobile = MobileInputController.Instance; mobile.AutoDetectPlatform = false; mobile.IsMobileEnabled = true;
        yield return null; yield return null;
        var touch = GameObject.Find("Touch items"); Check(touch != null, "mobile item controls are visible");
        touch.transform.GetChild(2).GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Check(items.SelectedSlot == 2, "mobile slot button selects item");
        items.Slots[2] = (int)ItemKind.HealthPotion; int potionStock = SaveSystem.LoadData().ItemInventory[2];
        UnityEngine.EventSystems.ExecuteEvents.Execute(touch.transform.GetChild(4).gameObject, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
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
    IEnumerator Press(Gamepad pad, GamepadButton button)
    {
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button)); yield return null; yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null; yield return null;
    }
    IEnumerator RunController()
    {
        SeedUI(); ControllerBindings.Reset();
        var pad = InputSystem.AddDevice<Gamepad>(); _pads.Add(pad);
        var menu = MainMenuUI.Instance; var shop = ShopManager.Instance;
        var events = UnityEngine.EventSystems.EventSystem.current;
        foreach (var data in shop.Progress.CardCollection) data.Duplicates = 100;
        foreach (CardPackType category in new[] { CardPackType.Mobility, CardPackType.Munitions, CardPackType.Survival, CardPackType.Gadget }) shop.Progress.AddEssence(category, 2000);
        shop.Progress.PermanentUpgrades = new int[5];
        SaveSystem.SaveData(shop.Progress); shop.LoadEconomy();
        menu.ShowPanel(menu.MenuPanel);
        var homeIndex = menu.MenuPanel.transform.Find("IndexButton").gameObject;
        events.SetSelectedGameObject(homeIndex); yield return Press(pad, GamepadButton.East);
        Check(menu.IndexPanel.activeSelf, "controller opens Index from home");
        yield return Press(pad, GamepadButton.South);
        Check(menu.MenuPanel.activeSelf && events.currentSelectedGameObject == homeIndex, "leaving Index restores the home Index button");
        menu.OpenIndex(); yield return null;
        var index = menu.IndexPanel.GetComponent<IndexLoadoutUI>();
        var card = menu.IndexPanel.GetComponentsInChildren<CardDisplay>()[1];
        string id = card.AssignedCard.ID;
        for (int level = 1; level < card.AssignedCard.MaxLevel; level++)
        {
            events.SetSelectedGameObject(card.UpgradeButton.gameObject);
            yield return Press(pad, GamepadButton.East);
            Check(shop.GetCardData(id).Level == level + 1, "B upgrades the selected card exactly once to level " + (level + 1));
            var expected = level + 1 < card.AssignedCard.MaxLevel ? card.UpgradeButton : card.AscendButton.IsActive() && card.AscendButton.IsInteractable() ? card.AscendButton : card.ClickButton;
            Check(events.currentSelectedGameObject == expected.gameObject, "focus stays on the card's available control at level " + (level + 1));
        }
        if (card.AscendButton.IsActive() && card.AscendButton.IsInteractable())
        {
            yield return Press(pad, GamepadButton.East);
            Check(shop.GetCardData(id).IsAscended && events.currentSelectedGameObject == card.ClickButton.gameObject, "ascension returns focus to the same card");
        }
        events.SetSelectedGameObject(card.ClickButton.gameObject);
        yield return Press(pad, GamepadButton.East);
        var equip = menu.NavigationScope.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.GetComponentInChildren<TMPro.TMP_Text>()?.text == "Equip to slot 2");
        events.SetSelectedGameObject(equip.gameObject); yield return Press(pad, GamepadButton.East);
        Check(shop.Loadout().Cards[1] == id && events.currentSelectedGameObject == card.ClickButton.gameObject, "equipping a starting card returns to its source card");
        var slot = menu.IndexPanel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Starting slot 2");
        events.SetSelectedGameObject(slot.gameObject); yield return Press(pad, GamepadButton.East);
        yield return Press(pad, GamepadButton.East);
        Check(string.IsNullOrEmpty(shop.Loadout().Cards[1]) && events.currentSelectedGameObject?.name == "Starting slot 2", "unequipping restores the rebuilt starting slot");

        index.ShowItems(); yield return null;
        var item = menu.IndexPanel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Item 6");
        events.SetSelectedGameObject(item.gameObject); yield return Press(pad, GamepadButton.East);
        equip = menu.NavigationScope.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.GetComponentInChildren<TMPro.TMP_Text>()?.text == "Equip to slot 2");
        events.SetSelectedGameObject(equip.gameObject); yield return Press(pad, GamepadButton.East);
        Check(shop.Loadout().Items[1] == 6 && events.currentSelectedGameObject?.name == "Item 6", "item equip restores its inventory button");
        slot = menu.IndexPanel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Item slot 2");
        events.SetSelectedGameObject(slot.gameObject); yield return Press(pad, GamepadButton.East);
        yield return Press(pad, GamepadButton.East);
        Check(shop.Loadout().Items[1] == 0 && events.currentSelectedGameObject?.name == "Item slot 2", "item unequip restores its slot button");
        yield return Press(pad, GamepadButton.DpadDown);
        Check(events.currentSelectedGameObject != null && events.currentSelectedGameObject.name != "BackButton", "navigation continues from restored item focus");

        menu.OpenShop(); menu.GetComponent<ShopUpdateUI>().ShowPage(2); yield return null;
        var permanent = menu.ShopPanel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Permanent upgrade 4");
        events.SetSelectedGameObject(permanent.gameObject); yield return Press(pad, GamepadButton.East);
        Check(shop.Progress.PermanentUpgrades[2] == 1 && events.currentSelectedGameObject == permanent.gameObject, "permanent upgrade retains its button");
        shop.Progress.PermanentUpgrades[2] = 49; menu.GetComponent<ShopUpdateUI>().Refresh();
        yield return Press(pad, GamepadButton.East);
        Check(shop.Progress.PermanentUpgrades[2] == 50 && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(permanent.transform.parent), "maxed permanent upgrade moves focus to a nearby upgrade");

        foreach (int count in new[] { 1, 3, 6 })
        {
            menu.OpenShop(); menu.GetComponent<ShopUpdateUI>().ShowPage(0); yield return null;
            int packIndex = count == 1 ? 0 : count == 3 ? 1 : 3;
            var pack = menu.PackContainer.GetChild(packIndex).GetComponent<UnityEngine.UI.Button>();
            events.SetSelectedGameObject(pack.gameObject); yield return Press(pad, GamepadButton.East);
            var buy = count == 1 ? menu.YesButton : count == 3 ? menu.BuyThreeButton : menu.BuySixButton;
            events.SetSelectedGameObject(buy.gameObject);
            int coins = shop.CurrentCoins;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
            yield return null; yield return null; yield return new WaitForSecondsRealtime(.15f);
            Check(menu.SliceZone.activeSelf && !menu.ContinueButton.gameObject.activeSelf, "held purchase press does not open " + count + " packs");
            var hint = menu.SliceHelpText.GetComponent<TMPro.TMP_Text>(); hint.ForceMeshUpdate();
            Check(hint.text.Contains(ControllerBindings.Label("Confirm")) && !hint.isTextOverflowing, "pack swipe hint shows the binding and fits");
            Check(shop.CurrentCoins == coins - shop.AvailablePacks[packIndex].Cost * count, "controller purchase charges once for " + count + " packs");
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null; yield return null;
            if (count == 3)
            {
                Check(ControllerBindings.TrySet("Confirm", "buttonWest", out _), "Interact can be remapped for the swipe screen");
                yield return Press(pad, GamepadButton.East);
                Check(menu.SliceZone.activeSelf, "old B binding cannot open a pack after remapping");
            }
            var interact = count == 3 ? GamepadButton.West : GamepadButton.East;
            yield return Press(pad, interact);
            Check(!menu.SliceZone.activeSelf, "Interact opens the " + count + " pack swipe screen");
            yield return Press(pad, interact);
            float deadline = Time.unscaledTime + 12;
            while (!menu.ContinueButton.gameObject.activeSelf && Time.unscaledTime < deadline) yield return null;
            yield return null;
            Check(menu.ContinueButton.gameObject.activeSelf && menu.CardRevealCenter.GetComponentsInChildren<CardDisplay>().Length == count * 3, "repeated Interact reveals exactly " + count * 3 + " cards");
            Check(shop.CurrentCoins == coins - shop.AvailablePacks[packIndex].Cost * count && events.currentSelectedGameObject == menu.ContinueButton.gameObject, "reveal preserves coins and focuses Continue");
            yield return Press(pad, interact);
            Check(menu.ShopPanel.activeSelf && !menu.OpeningOverlay.activeSelf, "controller Continue returns to shop");
            Check(events.currentSelectedGameObject == pack.gameObject, "pack opening restores the specific purchased pack");
            ControllerBindings.Reset();
        }

        menu.PackContainer.GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke(); menu.BuyPack(); yield return null;
        yield return Press(pad, GamepadButton.South);
        Check(menu.MenuPanel.activeSelf && !menu.OpeningOverlay.activeSelf, "Back still leaves the pack swipe screen");

        // Per-seat equipment uses independent focus instead of EventSystem.
        _pads.Add(InputSystem.AddDevice<Gamepad>());
        LocalCoopSession.RequestedPlayers = 2; LocalCoopSession.AdvancedLoadouts = true; LocalCoopSession.KeyboardTest = false;
        var host = menu.GetComponent<HostMenuUI>(); host.Open(); host.Hide();
        var lobby = menu.GetComponent<CoopLobbyUI>(); if (lobby == null) lobby = menu.gameObject.AddComponent<CoopLobbyUI>();
        lobby.Open(host); for (int i = 0; i < 2; i++) LocalCoopSession.PlayerControllers[i] = _pads[i];
        var panels = (LoadoutPanel[])typeof(CoopLobbyUI).GetField("_loadouts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(lobby);
        panels[1].Root.gameObject.SetActive(true); panels[1].ShowCards(false);
        yield return new WaitForSecondsRealtime(.4f);
        var paneControls = (UnityEngine.UI.Button[])typeof(LoadoutPanel).GetField("_controls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(panels[1]);
        typeof(LoadoutPanel).GetField("_focus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(panels[1], Array.FindIndex(paneControls, b => b.name == "Item 6"));
        panels[1].ControllerInput(Vector2.zero, false, false);
        var origin = panels[1].Focused.name; var p1Focus = panels[0].Focused;
        yield return Press(_pads[1], GamepadButton.East);
        Check(panels[1].HasModal, "P2 opens its own item actions");
        yield return Press(_pads[1], GamepadButton.East);
        Check(!panels[1].HasModal && panels[1].Focused.name == origin, "P2 item equip restores its own button");
        Check(panels[0].Focused == p1Focus, "P2 equipment change does not move P1 focus");
        lobby.Close(); LocalCoopSession.RequestedPlayers = 1; menu.OpenMenu();
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
                var rect = (RectTransform)card.transform;
                // Three-pack reveals center the four-card second row between grid columns.
                var slot = count == 3 ? (RectTransform)menu.CardRevealCenter : (RectTransform)rect.parent;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Check(corners.All(c => { Vector3 p = slot.InverseTransformPoint(c); return p.x >= slot.rect.xMin - 1 && p.x <= slot.rect.xMax + 1 && p.y >= slot.rect.yMin - 1 && p.y <= slot.rect.yMax + 1; }), "revealed card fits its reveal area");
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
