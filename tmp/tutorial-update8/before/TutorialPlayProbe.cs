#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Opt-in probe. TutorialVerification.StartPlay isolates save and preferences.
public class TutorialPlayProbe : MonoBehaviour
{
    public const string SessionKey = "DuckDefender.TutorialPlayVerification";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartIsolatedProbe()
    {
        if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
        UnityEditor.SessionState.EraseBool(SessionKey);
        new GameObject("Tutorial Play Verification").AddComponent<TutorialPlayProbe>();
    }
    public static string Result;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<string> _checks = new List<string>();
    readonly List<Gamepad> _realPads = new List<Gamepad>();
    Gamepad _pad;
    static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);
    static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Private).Invoke(value, args);
    string Step => Field<object>(TutorialDirector.Instance, "_step").ToString();
    LocalPlayer Player => LocalCoopSession.Instance.Players[0];
    void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks.Add(message); Result = "Running: " + message;
    }
    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(SaveSystem.VerificationSavePath)) throw new InvalidOperationException("Start isolated Play verification first.");
        DontDestroyOnLoad(gameObject);
        while (!TutorialDirector.Practice || LocalCoopSession.Instance == null || LocalCoopSession.Instance.Players.Count == 0) yield return null;
        foreach (var pad in Gamepad.all.ToArray()) if (pad.enabled) { _realPads.Add(pad); InputSystem.DisableDevice(pad); }
        _pad = InputSystem.AddDevice<Gamepad>(); ControllerBindings.Reset(); InputManager.Instance.ResetToDefaults();
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            object current;
            try
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                current = top.Current;
                if (current is IEnumerator child) { stack.Push(child); continue; }
            }
            catch (Exception e) { Finish("FAILED: " + e); yield break; }
            yield return current;
        }
        Finish("PASS: " + _checks.Count + " tutorial Play Mode checks.");
    }
    void Finish(string result)
    {
        Result = result; Directory.CreateDirectory("tmp/tutorial");
        File.WriteAllText("tmp/tutorial/play-verification.txt", result + "\n" + string.Join("\n", _checks));
        Debug.Log(result); Destroy(gameObject);
    }
    void OnDestroy()
    {
        if (_pad != null && _pad.added) InputSystem.RemoveDevice(_pad);
        foreach (var pad in _realPads) if (pad.added) InputSystem.EnableDevice(pad);
    }
    void Input(Vector2 move = default, Vector2 aim = default, bool shoot = false)
    { InputSystem.QueueStateEvent(_pad, new GamepadState { leftStick = move, rightStick = aim, rightTrigger = shoot ? 1 : 0 }); }
    IEnumerator Until(Func<bool> predicate, string message, float timeout = 12)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
        Check(predicate(), message);
    }
    IEnumerator Continue()
    {
        Input(); yield return new WaitForSecondsRealtime(.5f);
        var button = Field<UnityEngine.UI.Button>(TutorialDirector.Instance, "_continue");
        Check(button.gameObject.activeInHierarchy && button.IsInteractable(), "Continue is reachable for " + Step);
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.East));
        yield return new WaitForSecondsRealtime(.08f); Input(); yield return new WaitForSecondsRealtime(.2f);
    }
    IEnumerator Submit(UnityEngine.UI.Button button)
    {
        Input(); yield return new WaitForSecondsRealtime(.25f);
        Check(button != null && button.IsActive() && button.IsInteractable(), "Reachable menu control: " + (button != null ? button.name : "missing"));
        if (MainMenuUI.Instance != null) Check(button.transform.IsChildOf(MainMenuUI.Instance.NavigationScope.transform), "Control belongs to current navigation scope: " + button.name);
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.East));
        yield return new WaitForSecondsRealtime(.08f); Input(); yield return new WaitForSecondsRealtime(.3f);
    }
    UnityEngine.UI.Button MenuButton(GameObject panel, string name) => panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == name);
    IEnumerator MoveTo(float x)
    {
        float end = Time.realtimeSinceStartup + 6;
        while (Mathf.Abs(Player.transform.position.x - x) > .15f && Time.realtimeSinceStartup < end)
        { Input(new Vector2(Mathf.Clamp((x - Player.transform.position.x) * .6f, -.65f, .65f), 0)); yield return null; }
        Input(); yield return new WaitForSecondsRealtime(.2f);
        Check(Mathf.Abs(Player.transform.position.x - x) < .5f, "Controller movement reaches practice goal.");
    }
    IEnumerator Run()
    {
        yield return Until(() => TutorialDirector.Practice && Step == "Welcome", "Fresh save automatically enters stage 1.");
        int frame = Time.frameCount; yield return null; yield return null;
        Check(Time.frameCount > frame, "Game frames advance in Play Mode.");
        Check(WaveManager.Instance.GetCurrentWave() == 0 && EnemyBase.ActiveEnemies.Count == 0, "Practice suppresses normal waves.");
        Check(!TutorialDirector.AllowMovement && !TutorialDirector.AllowShooting, "Welcome locks gameplay controls.");
        ScreenCapture.CaptureScreenshot("tmp/tutorial/welcome.png");
        yield return Continue();
        var objects = Field<List<GameObject>>(TutorialDirector.Instance, "_practiceObjects");
        yield return MoveTo(objects[0].transform.position.x);
        Check(!Field<bool>(TutorialDirector.Instance, "_ready"), "One movement goal cannot skip the other.");
        Call(TutorialDirector.Instance, "RestartPractice"); yield return null;
        Check(!Field<bool>(TutorialDirector.Instance, "_ready") && objects.Count == 2, "Restart step rebuilds both goals and clears partial progress.");
        Player.transform.position += Vector3.down * 30; yield return null; yield return null;
        Check(Player.transform.position.y > Field<Vector3>(TutorialDirector.Instance, "_origin").y - 2, "Falling out of practice returns the player to the lesson.");
        yield return MoveTo(objects[0].transform.position.x);
        yield return MoveTo(objects[1].transform.position.x);
        yield return Continue();
        var box = Field<BoxCollider2D>(TutorialDirector.Instance, "_box");
        yield return new WaitForSecondsRealtime(.6f);
        float start = Time.realtimeSinceStartup;
        while (!Field<bool>(TutorialDirector.Instance, "_ready") && Time.realtimeSinceStartup - start < 5)
        {
            float x = Mathf.Clamp((box.bounds.center.x - Player.transform.position.x) * .6f, -.6f, .6f);
            Input(new Vector2(x, Time.realtimeSinceStartup - start < .65f ? 1 : 0)); yield return null;
        }
        Input(); Check(Field<bool>(TutorialDirector.Instance, "_ready"), "Actual controller jump lands on the box.");
        yield return Continue();
        var targets = Field<List<TutorialTarget>>(TutorialDirector.Instance, "_targets");
        foreach (var target in targets) { Input(aim: (target.transform.position - Player.Weapon.FirePoint.position).normalized); yield return new WaitForSecondsRealtime(.15f); }
        yield return Continue();
        Check(Step == "Shoot" && Field<List<TutorialTarget>>(TutorialDirector.Instance, "_targets").Count == 5, "All five aim goals unlock shooting targets.");
        targets = Field<List<TutorialTarget>>(TutorialDirector.Instance, "_targets");
        foreach (var target in targets)
        {
            Input(aim: (target.transform.position - Player.Weapon.FirePoint.position).normalized, shoot: true);
            yield return Until(() => !target.gameObject.activeSelf, "A real feather destroys target " + target.Index, 5);
        }
        Input(); yield return Continue();
        yield return Until(() => Step == "Combat", "Practice enemies arrive and freeze.");
        Check(EnemyBase.ActiveEnemies.Count == 2 && EnemyBase.ActiveEnemies.All(e => e.HealthRemaining == 2), "Exactly two practice enemies have 2 HP.");
        var enemies = EnemyBase.ActiveEnemies.ToArray();
        enemies[0].TakeDamage(2, Player.Stats); yield return null;
        Check(LevelManager.Instance.CurrentLevel == 1 && LevelManager.Instance.CurrentXP == LevelManager.Instance.TargetXP / 2, "First enemy awards exactly half the XP bar.");
        enemies[1].TakeDamage(2, Player.Stats); yield return null;
        Check(LevelUpUI.Instance.IsOffering && LevelManager.Instance.CurrentLevel == 2, "Second enemy opens the level-up offer.");
        var options = LevelUpUI.Instance.CardContainer.GetComponentsInChildren<CardDisplay>();
        Check(options.Length == 3 && options.Count(c => c.ClickButton.IsInteractable()) == 1, "Only Basic Health is selectable.");
        LevelUpUI.Instance.SelectCard(options.First(c => c.AssignedCard.ID == "bas_damage").AssignedCard);
        Check(LevelUpUI.Instance.IsOffering, "Direct selection cannot bypass the required health card.");
        LevelUpUI.Instance.SelectCard(options.First(c => c.AssignedCard.ID == "bas_health").AssignedCard);
        yield return null; Check(GameUI.Instance.PlayerHealthBar.gameObject.activeSelf && Time.timeScale == 1, "Health lesson reveals the numeric bar and releases card pause.");
        ScreenCapture.CaptureScreenshot("tmp/tutorial/health.png");
        Check(LevelManager.Instance.TotalCoins == 0, "Practice grants no coins.");
        yield return Continue(); yield return Continue();
        yield return Until(() => MainMenuUI.Instance != null, "Stage 1 returns to menu."); yield return new WaitForSecondsRealtime(.5f);
        Check(!MainMenuUI.Instance.MenuPanel.transform.Find("ShopButton").GetComponent<UnityEngine.UI.Button>().IsInteractable(), "Menu limits navigation to Play.");
        yield return Submit(MenuButton(MainMenuUI.Instance.MenuPanel, "PlayButton"));
        yield return Until(() => LocalCoopSession.Instance != null && TutorialDirector.Progress.Stage == TutorialStage.Waves, "Stage 2 opens."); yield return new WaitForSecondsRealtime(.5f);
        yield return Continue();
        Call(TutorialDirector.Instance, "RestartWaves"); yield return new WaitForSecondsRealtime(.7f);
        Check(TutorialDirector.Progress.Stage == TutorialStage.Waves && WaveManager.Instance.GetCurrentWave() == 0, "Restart stage returns to its intro without an extra reward.");
        SceneManager.LoadScene("MainMenu"); yield return new WaitForSecondsRealtime(.7f);
        Check(TutorialDirector.Progress.Stage == TutorialStage.Play, "Interrupted waves resume from the Play checkpoint.");
        yield return Submit(MenuButton(MainMenuUI.Instance.MenuPanel, "PlayButton"));
        yield return new WaitForSecondsRealtime(.5f); yield return Continue();
        Player.Health.TryTakeDamage(10000); Check(Player.Health.CurrentHealth == 1 && !Player.Health.IsDead, "Lethal tutorial damage stops at 1 health.");
        Check(!LocalCoopSession.Instance.ItemDropsEnabled && !TutorialDirector.AllowItems, "Stage 2 blocks item drops and use.");
        WaveManager.Instance.TimeBetweenWaves = .02f; WaveManager.Instance.BaseSpawnInterval = .04f; WaveManager.Instance.MinSpawnInterval = .04f;
        float end = Time.realtimeSinceStartup + 35;
        while (TutorialDirector.Progress.Stage == TutorialStage.Waves && Time.realtimeSinceStartup < end)
        {
            if (EnemyTipUI.Instance.IsShowing) EnemyTipUI.Instance.Dismiss();
            if (LevelUpUI.Instance.IsOffering)
            {
                var option = LevelUpUI.Instance.CardContainer.GetComponentsInChildren<CardDisplay>().FirstOrDefault();
                if (option != null) LevelUpUI.Instance.SelectCard(option.AssignedCard);
            }
            foreach (var enemy in EnemyBase.ActiveEnemies.ToArray()) enemy.TakeItemDamage(10000, Player.Stats, true);
            yield return null;
        }
        Check(WaveManager.Instance.GetCurrentWave() == 5 && TutorialDirector.Progress.Stage == TutorialStage.Shop && Time.timeScale == 0, "Stage 2 ends at wave 5, saves reward and pauses.");
        if (LevelUpUI.Instance.IsOffering) LevelUpUI.Instance.SelectCard(LevelUpUI.Instance.CardContainer.GetComponentsInChildren<CardDisplay>()[0].AssignedCard);
        yield return Continue(); yield return Until(() => MainMenuUI.Instance != null, "Reward returns to shop lesson."); yield return new WaitForSecondsRealtime(.4f);
        var menu = MainMenuUI.Instance;
        yield return Submit(MenuButton(menu.MenuPanel, "ShopButton"));
        yield return Submit(menu.PackContainer.GetComponentsInChildren<UnityEngine.UI.Button>()[0]);
        yield return Submit(menu.YesButton);
        int coins = ShopManager.Instance.CurrentCoins;
        menu.BackToMainMenu(); Check(menu.OpeningOverlay.activeSelf, "Back cannot escape an unopened tutorial pack.");
        SceneManager.LoadScene("MainMenu"); yield return new WaitForSecondsRealtime(1);
        menu = MainMenuUI.Instance;
        Check(menu.OpeningOverlay.activeSelf && ShopManager.Instance.CurrentCoins == coins, "Reload restores purchased pack without charging again.");
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.East));
        yield return new WaitForSecondsRealtime(.08f); Input();
        yield return Until(() => menu.ContinueButton.gameObject.activeInHierarchy, "Pack reveal completes.");
        yield return Submit(menu.ContinueButton);
        yield return Submit(MenuButton(menu.ShopPanel, "BackButton"));
        yield return Submit(MenuButton(menu.MenuPanel, "IndexButton"));
        var index = menu.IndexPanel.GetComponent<CardIndexUI>();
        int page = index.PageForCard(TutorialDirector.Progress.UpgradeCardID);
        while (index.CurrentPage < page) yield return Submit(index.NextButton);
        while (index.CurrentPage > page) yield return Submit(index.PreviousButton);
        yield return Continue();
        var upgradeID = TutorialDirector.Progress.UpgradeCardID;
        yield return Submit(index.ContentArea.GetComponentsInChildren<CardDisplay>().First(c => c.AssignedCard.ID == upgradeID).ClickButton);
        yield return Submit(menu.IndexPanel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name.StartsWith("Upgrade - ")));
        Check(ShopManager.Instance.GetCardData(upgradeID).Level == 2, "Guided index upgrade succeeds.");
        yield return Submit(MenuButton(menu.IndexPanel, "BackButton"));
        Check(!TutorialDirector.CoreActive, "Core tutorial completes and unlocks normal menus.");
        yield return Continue();
        Check(SceneManager.GetActiveScene().name == "MainMenu" && !TutorialDirector.MenuLocked, "Completion confirm dismisses the banner without also pressing Play.");
        var shop = ShopManager.Instance;
        shop.Progress.TotalCoins = 10000; SaveSystem.SaveData(shop.Progress); shop.LoadEconomy();
        yield return Submit(MenuButton(menu.MenuPanel, "ShopButton")); shop.RefreshItemShop();
        Check(shop.BuyItem(0, shop.Progress.ItemShopStock[0], shop.Progress.ItemShopRefreshUtc), "First purchased item starts item lesson.");
        yield return Submit(MenuButton(menu.ShopPanel, "BackButton"));
        yield return Submit(MenuButton(menu.MenuPanel, "IndexButton"));
        yield return Submit(MenuButton(menu.IndexPanel, "ItemsTab"));
        var equipment = menu.IndexPanel.GetComponent<IndexLoadoutUI>();
        int owned = Array.FindIndex(shop.Progress.ItemInventory, n => n > 0);
        Check(!shop.SellItem(owned), "Item lesson prevents selling.");
        while (!menu.IndexPanel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Item " + owned))
            yield return Submit(MenuButton(menu.IndexPanel, ">"));
        yield return Submit(MenuButton(menu.IndexPanel, "Item " + owned));
        yield return Submit(MenuButton(menu.IndexPanel, "Equip to slot 1"));
        Check(shop.Progress.SoloLoadout.Items[0] == owned, "Item lesson accepts equipping through its menu.");
        yield return Continue();
        Check(shop.Progress.Tutorial.Items == ItemTutorialStage.Complete && TutorialDirector.MenuLocked, "Item completion banner holds menu focus.");
        yield return Continue();
        Check(!TutorialDirector.MenuLocked, "Dismissing item completion restores navigation.");
        yield return Submit(MenuButton(menu.IndexPanel, "BackButton"));
        yield return Submit(MenuButton(menu.MenuPanel, "PlayButton"));
        yield return new WaitForSecondsRealtime(.7f);
        var items = Player.GetComponent<PlayerItems>();
        Check(!TutorialDirector.ProtectedRun && items.HadEquippedItems, "Normal runs restore item loadouts and remove tutorial protection.");
        if (EnemyTipUI.Instance.IsShowing) EnemyTipUI.Instance.Dismiss();
        items.Select(0); yield return null; yield return null;
        Check(Field<RectTransform>(Player.GetComponent<ItemWheelUI>(), "_wheel").gameObject.activeSelf, "Equipping before a run enables its item wheel.");
        Player.Health.TryTakeDamage(10000);
        Check(Player.Health.IsDead, "Normal run lethal damage is no longer clamped.");
        Time.timeScale = 1; SceneManager.LoadScene("MainMenu"); yield return new WaitForSecondsRealtime(.7f);
        var pickupData = PlayerData.CreateNew(); pickupData.Tutorial.Stage = TutorialStage.Complete; pickupData.CompletedRuns = 2;
        SaveSystem.SaveData(pickupData, true, true);
        SceneManager.LoadScene("SampleScene"); yield return new WaitForSecondsRealtime(.7f);
        Check(LocalCoopSession.Instance.ItemDropsEnabled, "Normal eligible runs retain item drops.");
        ItemPickup.Spawn(Player.transform.position, (int)ItemKind.Bomb).Collect(Player); yield return null;
        Check(TutorialDirector.Progress.ItemActive && Field<RectTransform>(TutorialDirector.Instance, "_panel") == null, "First pickup records the item lesson without interrupting gameplay.");
        items = Player.GetComponent<PlayerItems>(); items.Select(0); yield return null;
        Check(!items.HadEquippedItems && !Field<RectTransform>(Player.GetComponent<ItemWheelUI>(), "_wheel").gameObject.activeSelf, "Picking up mid-run does not enable an unequipped item wheel.");
        Time.timeScale = 1; SceneManager.LoadScene("MainMenu"); yield return new WaitForSecondsRealtime(.7f);
        Check(TutorialDirector.Progress.ItemActive && !MenuButton(MainMenuUI.Instance.MenuPanel, "PlayButton").IsInteractable() && MenuButton(MainMenuUI.Instance.MenuPanel, "IndexButton").IsInteractable(), "Pickup lesson starts on return to menu with Index reachable.");
    }
}
#endif
