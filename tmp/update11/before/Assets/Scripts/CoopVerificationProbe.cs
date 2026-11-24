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

// Opt-in hardware simulation and isolated-save integration checks; excluded from players.
public class CoopVerificationProbe : MonoBehaviour
{
    public const string SessionKey = "DuckDefender.CoopVerification";
    public static string Result = "Not started";
    public static string OutputPath => Path.Combine(Path.GetTempPath(), "duck-coop-verification");
    readonly List<Gamepad> _pads = new List<Gamepad>();
    readonly List<Gamepad> _originalPads = new List<Gamepad>();
    readonly List<string> _checks = new List<string>();
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); _checks.Add(message); Result = "Running: " + _checks.Count + " - " + message; }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static object Call(object target, string method, params object[] values) => target.GetType().GetMethod(method, Private).Invoke(target, values);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartIfRequested()
    {
        if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
        Result = "Running";
        var probe = new GameObject("Co-op verification").AddComponent<CoopVerificationProbe>();
        DontDestroyOnLoad(probe.gameObject);
    }
    IEnumerator Start()
    {
        var run = Run();
        while (true)
        {
            object current = null; bool more;
            try { more = run.MoveNext(); if (more) current = run.Current; }
            catch (Exception ex)
            {
                Result = "FAIL after " + _checks.Count + " checks: " + ex;
                Debug.LogError(Result); File.WriteAllText(Path.Combine(OutputPath, "result.txt"), Result + "\n" + string.Join("\n", _checks));
                Time.timeScale = 0; yield break;
            }
            if (!more) break;
            yield return current;
        }
        Result = "PASS: " + _checks.Count + " co-op integration checks";
        File.WriteAllText(Path.Combine(OutputPath, "result.txt"), Result + "\n" + string.Join("\n", _checks));
        Debug.Log(Result); Time.timeScale = 0;
    }
    IEnumerator Run()
    {
        Directory.CreateDirectory(OutputPath);
        Check(!string.IsNullOrEmpty(SaveSystem.VerificationSavePath), "Test save is isolated from the host account");
        // The host uses Gamepad.all[0]; temporarily isolate simulated devices from
        // connected hardware so a real controller cannot take the test host seat.
        _originalPads.AddRange(Gamepad.all);
        foreach (var pad in _originalPads) InputSystem.RemoveDevice(pad);
        for (int i = 0; i < 4; i++) _pads.Add(InputSystem.AddDevice<Gamepad>());
        ControllerBindings.Reset();
        yield return null;
        var testData = SaveSystem.LoadData(); testData.TotalCoins = 100000;
        testData.CardCollection = ShopManager.Instance.AllCards.Where(c => c != null && !c.IsBasic).Select(c => new CardSaveData(c.ID) { Duplicates = 100 }).ToList();
        SaveSystem.SaveData(testData); ShopManager.Instance.LoadEconomy();
        var menu = MainMenuUI.Instance;
        menu.OpenShop(); yield return null;
        Call(menu, "OnPackClicked", ShopManager.Instance.AvailablePacks[0]); yield return null;
        Check(!menu.ShopPanel.GetComponent<CanvasGroup>().interactable && menu.NavigationScope == menu.ConfirmPanel, "Purchase confirmation locks shop input and navigation scope");
        Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.transform.IsChildOf(menu.ConfirmPanel.transform), "Purchase confirmation takes controller focus");
        menu.CancelPurchase(); Check(menu.ShopPanel.GetComponent<CanvasGroup>().interactable, "Cancel restores shop input");
        menu.OpenSettings(); yield return null;
        var settings = menu.SettingsPanel.GetComponent<SettingsMenuUI>();
        Check(settings.CameraZoomSlider.gameObject.activeInHierarchy, "Zoom is visible on the main settings screen");
        settings.OpenKeybinds(); yield return null;
        Check(!settings.CameraZoomSlider.gameObject.activeInHierarchy && settings.KeybindPanel.transform.Find("Controller bindings") != null, "Keybinds hides zoom and includes controller tab");
        var bindings = (ControllerBindingsUI)typeof(SettingsMenuUI).GetField("_controllerBindings", Private).GetValue(settings);
        bindings.Show(true); bindings.Begin(0); yield return new WaitForSecondsRealtime(.4f);
        InputSystem.QueueStateEvent(_pads[0], new GamepadState().WithButton(GamepadButton.North)); yield return null; yield return null;
        Check(ControllerBindings.Get("Jump") == "buttonNorth" && !bindings.Capturing, "Controller remapping captures and saves the chosen button");
        Check(!ControllerBindings.TrySet("Dash", "buttonNorth", out _), "Conflicting gameplay mappings are rejected");
        ControllerBindings.Reset(); InputSystem.QueueStateEvent(_pads[0], new GamepadState());
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "controller-bindings.png")); yield return new WaitForEndOfFrame();
        settings.CloseKeybinds(); yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "settings.png")); yield return new WaitForEndOfFrame();
        menu.OpenIndex(); yield return null;
        var normalIndex = menu.IndexPanel.GetComponent<CardIndexUI>(); normalIndex.CycleLayout(); normalIndex.CycleLayout();
        yield return null;
        Check(normalIndex.PageCount == 1 && normalIndex.LayoutText.text.Contains("ALL") && normalIndex.ContentArea.GetComponentsInChildren<CardDisplay>().Length == ShopManager.Instance.AllCards.Count(c => c != null && !c.IsBasic && c.PackCategory == normalIndex.SelectedPack), "ALL layout displays every card in the current category");
        var indexCard = normalIndex.ContentArea.GetComponentInChildren<CardDisplay>();
        Check(indexCard.ClickButton.interactable && indexCard.ClickButton.navigation.mode == UnityEngine.UI.Navigation.Mode.Explicit, "Index cards are reachable with explicit controller navigation");
        if (indexCard.UpgradeButton.IsInteractable())
        {
            indexCard.ClickButton.onClick.Invoke();
            Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == indexCard.UpgradeButton.gameObject, "Confirm on an index card focuses its upgrade action");
        }
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "index-all.png")); yield return new WaitForEndOfFrame();
        var host = MainMenuUI.Instance.GetComponent<HostMenuUI>(); host.Open();
        Check(host.Panel.activeSelf && !MainMenuUI.Instance.MenuPanel.activeSelf, "Host menu opens exclusively");
        var playerRow = host.Panel.GetComponentsInChildren<TMPro.TMP_Text>().First(t => t.text.StartsWith("Players:")).GetComponentInParent<UnityEngine.UI.Button>();
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(playerRow.gameObject);
        InputSystem.QueueStateEvent(_pads[0], new GamepadState().WithButton(GamepadButton.East));
        yield return null; yield return null;
        Check(playerRow.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("3"), "Host controller B activates a menu option exactly once");
        InputSystem.QueueStateEvent(_pads[0], new GamepadState());
        Set(host, "_count", 4); Call(host, "Refresh");
        yield return null; Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "host.png"));
        yield return new WaitForEndOfFrame(); yield return null;
        host.OpenCards(); yield return null;
        var index = MainMenuUI.Instance.IndexPanel.GetComponent<CardIndexUI>();
        Check(index.HostFilterMode && !index.CoinsText.gameObject.activeSelf && !index.EssenceText.gameObject.activeSelf, "Host card index hides economy counters");
        var displayed = index.ContentArea.GetComponentInChildren<CardDisplay>();
        Check(displayed != null && displayed.ClickButton.interactable, "Host cards are selectable");
        int beforeDisabled = LocalCoopSession.DisabledCards.Count;
        displayed.ClickButton.onClick.Invoke(); Check(LocalCoopSession.DisabledCards.Count == beforeDisabled + 1, "Host can disable a card");
        Check(displayed.ProgressText.gameObject.activeInHierarchy && displayed.ProgressText.text == "Disabled" && displayed.UpgradeCostText.text == "Disabled", "Filter replaces both the copies and cost labels");
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "card-filter.png"));
        yield return new WaitForEndOfFrame(); yield return null;
        displayed.ClickButton.onClick.Invoke(); Check(LocalCoopSession.DisabledCards.Count == beforeDisabled, "Host can re-enable a card");
        host.ReturnFromCards(); host.StartGame();
        yield return new WaitForSecondsRealtime(.4f);
        var lobby = CoopLobbyUI.Instance;
        Check(lobby != null && lobby.IsOpen && !UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents, "Start co-op opens independent customization panels");
        int originalColor = LocalCoopSession.PlayerColors[0];
        InputSystem.QueueStateEvent(_pads[1], new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
        Check(LocalCoopSession.PlayerColors[0] == originalColor && LocalCoopSession.PlayerColors[1] == 2, "Only the sending controller changes its duck color");
        InputSystem.QueueStateEvent(_pads[1], new GamepadState()); yield return null;
        var previews = (UnityEngine.UI.Image[])typeof(CoopLobbyUI).GetField("_previews", Private).GetValue(lobby);
        Check(host.DuckPreviewSprite != null && previews.All(p => p.sprite == host.DuckPreviewSprite && p.preserveAspect && !p.raycastTarget && p.color == Color.white), "All seats preview the authored duck sprite with its aspect ratio intact");
        var otherPreview = previews[1].material;
        for (int color = 0; color < CoopLobbyUI.Colors.Length; color++)
        {
            LocalCoopSession.PlayerColors[0] = color; Call(lobby, "Refresh", 0);
            Check(previews[0].material.shader.name == "Duck Defender/Duck Preview Palette" && previews[0].material.GetColor("_PlayerColor") == CoopLobbyUI.Colors[color] && previews[1].material == otherPreview, "Duck preview palette updates independently for color " + color);
        }
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "duck-previews.png")); yield return new WaitForEndOfFrame();
        LocalCoopSession.PlayerColors[0] = originalColor; Call(lobby, "Refresh", 0);
        var lobbyFocus = (int[])typeof(CoopLobbyUI).GetField("_focus", Private).GetValue(lobby);
        lobbyFocus[2] = 1;
        InputSystem.QueueStateEvent(_pads[2], new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
        Check(LocalCoopSession.PlayerNames[2] == "" && LocalCoopSession.PlayerNames[0] == "PLAYER 1", "Selecting Name clears only the editing player's default name");
        InputSystem.QueueStateEvent(_pads[2], new GamepadState()); yield return null;
        Call(lobby, "FinishName", 2);
        Check(LocalCoopSession.PlayerNames[2] == "PLAYER 3", "Finishing an empty name restores the seat's default");
        Call(lobby, "Activate", 2); Call(lobby, "TypeKey", 2, 36); Call(lobby, "FinishName", 2);
        Check(LocalCoopSession.PlayerNames[2] == "PLAYER 3", "Whitespace-only names also restore the default");
        LocalCoopSession.PlayerNames[2] = "OLD NAME"; Call(lobby, "Activate", 2);
        Check(LocalCoopSession.PlayerNames[2] == "", "Reopening Name clears a previous custom name too");
        for (int k = 0; k < 12; k++) Call(lobby, "TypeKey", 2, k);
        Check(LocalCoopSession.PlayerNames[2] == "ABCDEFGHIJ" && LocalCoopSession.PlayerNames[0] == "PLAYER 1", "Names cap at ten characters and remain isolated by seat");
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "customization.png")); yield return new WaitForEndOfFrame();
        Call(lobby, "FinishName", 2);
        for (int i = 0; i < 4; i++) { lobbyFocus[i] = 2; Call(lobby, "Activate", i); }
        yield return new WaitForSecondsRealtime(1.5f);
        yield return null; yield return null;
        GameDifficulty.Select(0, false); EnemyTipUI.Instance.enabled = false;
        var session = LocalCoopSession.Instance; var players = session.Players;
        Check(players.Count == 4 && players.Select(p => p.Stats).Distinct().Count() == 4, "Four independent player instances spawn");
        Check(players.Select(p => p.Controller).Distinct().Count() == 4 && players.All(p => p.UsesGamepad), "Four devices are assigned independently");
        Check(PlayerStats.Instance == players[0].Stats && PlayerController.Instance == players[0].Movement, "Compatibility singleton stays on player one");
        var authoredFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/DuckDefenderTestFontv2.asset");
        Check(GameUI.Instance.UIFont == authoredFont && CoopRunUI.Instance.GetComponentsInChildren<TMPro.TMP_Text>(true).All(t => t.font == authoredFont), "Co-op UI uses the exact authored DuckDefenderTestFontv2 asset");
        Check(LevelManager.Instance.TargetXP == 400, "Four-player shared XP target scales by four");
        Check(players[2].GetComponent<SpriteRenderer>().sharedMaterial.shader.name == "Duck Defender/Player Palette", "Player blue uses palette replacement instead of multiplying yellow artwork");
        InputSystem.QueueStateEvent(_pads[0], new GamepadState { leftStick = Vector2.left, rightStick = Vector2.right });
        InputSystem.QueueStateEvent(_pads[1], new GamepadState { leftStick = Vector2.right, rightStick = Vector2.up, rightTrigger = 1 });
        yield return null; yield return null;
        Check(InputHelper.GetHorizontal(players[0]) < -.9f && InputHelper.GetHorizontal(players[1]) > .9f && Mathf.Abs(InputHelper.GetHorizontal(players[2])) < .01f, "Movement stays on the assigned controller");
        Check(!InputHelper.GetShootHeld(players[0]) && InputHelper.GetShootHeld(players[1]), "Only the firing controller shoots");
        Check(Vector2.Dot(players[0].Weapon.AimDirection, Vector2.right) > .99f && Vector2.Dot(players[1].Weapon.AimDirection, Vector2.up) > .99f, "Aim directions remain independent");
        foreach (var pad in _pads) InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return null;
        InputSystem.RemoveDevice(_pads[3]); yield return null; yield return null;
        Check(session.DevicesMissing && Time.timeScale == 0, "Controller disconnection pauses the run");
        InputSystem.AddDevice(_pads[3]); yield return null; yield return null;
        Check(!session.DevicesMissing && Time.timeScale == 1 && players[3].Controller == _pads[3], "Reconnection retains the same seat and resumes");
        var cards = CardManager.Instance;
        var basic = cards.AllCards.First(c => c.IsBasic && c.Modifiers.Any(m => m.StatType == StatType.Damage));
        int initialDamage = players[1].Weapon.CurrentStats.Damage;
        cards.ApplyCardEffect(basic, players[0].Stats);
        Check(players[0].Weapon.CurrentStats.Damage > initialDamage && players[1].Weapon.CurrentStats.Damage == initialDamage, "A damage upgrade affects only its recipient");
        Check(cards.GetRunPickups(basic.ID, players[0].Stats) == 1 && cards.GetRunPickups(basic.ID, players[1].Stats) == 0, "Card histories are independent");
        var special = cards.AllCards.First(c => c.Modifiers.Any(m => m.StatType == StatType.FrostyFeatherThreshold));
        cards.ApplyCardEffect(special, players[2].Stats);
        Check(players[2].Stats.GetSpecialFeatherByCardID(special.ID) != null && players[0].Stats.GetSpecialFeatherByCardID(special.ID) == null, "Special feathers belong to the selecting player");
        players[0].Stats.XPMultiplier = 1.2f; players[1].Stats.XPMultiplier = 1.3f;
        Check(Mathf.Abs(LocalCoopSession.TeamXPBonus() - 1.5f) < .001f, "Team XP bonuses add together");
        players[0].Stats.EnemyHealthMissingPercent = .2f; players[1].Stats.EnemyHealthMissingPercent = .3f;
        Check(Mathf.Abs(LocalCoopSession.TeamSabotage() - .5f) < .001f, "Sabotage combines across builds");
        int coins = LevelManager.Instance.TotalCoins;
        LevelManager.Instance.AddCoins(10, players[1].Stats); LevelManager.Instance.FlushCoinSave();
        Check(LevelManager.Instance.TotalCoins == coins + 10 && SaveSystem.LoadData().TotalCoins == coins + 10 && players[1].CoinsCollected == 10 && players[0].CoinsCollected == 0, "Coins credit the host and track the collecting seat");
        LevelUpUI.Instance.ShowLevelUpOptions();
        yield return new WaitForSecondsRealtime(.4f);
        Check(Time.timeScale == 0 && CoopRunUI.Instance.GetComponentsInChildren<CardDisplay>().Length == 12, "Four-player level up presents twelve cards while paused");
        Canvas.ForceUpdateCanvases();
        foreach (var card in CoopRunUI.Instance.GetComponentsInChildren<CardDisplay>())
        {
            var corners = new Vector3[4]; card.BackgroundImage.rectTransform.GetWorldCorners(corners);
            var pane = (RectTransform)card.transform.parent;
            Check(corners.All(c => pane.rect.Contains(pane.InverseTransformPoint(c))), "Card artwork remains inside its player's pane");
            card.DescriptionText.ForceMeshUpdate();
            Check(!card.DescriptionText.isTextOverflowing, "Co-op card description fits");
        }
        InputSystem.QueueStateEvent(_pads[1], new GamepadState { leftStick = Vector2.right });
        yield return null; yield return null;
        var selections = (int[])typeof(CoopRunUI).GetField("_selected", Private).GetValue(CoopRunUI.Instance);
        Check(selections[1] == 1 && selections[0] == 0, "Card navigation changes only the assigned player's selection");
        InputSystem.QueueStateEvent(_pads[1], new GamepadState());
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "four-player-cards.png"));
        yield return new WaitForEndOfFrame(); yield return null;
        int picked = players[0].CardsPicked;
        InputSystem.QueueStateEvent(_pads[0], new GamepadState().WithButton(GamepadButton.East));
        yield return null; yield return null;
        var ready = (bool[])typeof(CoopRunUI).GetField("_ready", Private).GetValue(CoopRunUI.Instance);
        Check(players[0].CardsPicked == picked && ready[0] && !ready[1], "B marks only the sending player's card pending without applying it");
        InputSystem.QueueStateEvent(_pads[0], new GamepadState()); yield return null; yield return null;
        InputSystem.QueueStateEvent(_pads[0], new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
        Check(!ready[0] && players[0].CardsPicked == picked, "B deselects without granting or removing an upgrade");
        InputSystem.QueueStateEvent(_pads[0], new GamepadState()); yield return null; yield return null;
        InputSystem.QueueStateEvent(_pads[0], new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "selected-card.png")); yield return new WaitForEndOfFrame();
        Check(LevelUpUI.Instance.IsOffering && Time.timeScale == 0, "One selection cannot resume other players' choices");
        LevelUpUI.Instance.ShowLevelUpOptions();
        for (int i = 1; i < 4; i++) InputSystem.QueueStateEvent(_pads[i], new GamepadState().WithButton(GamepadButton.East));
        yield return new WaitForSecondsRealtime(1.5f);
        Check(LevelUpUI.Instance.IsOffering && Time.timeScale == 0 && players.All(p => p.CardsPicked >= 1), "Queued level-ups present the next choices without unpausing");
        foreach (var pad in _pads) InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return new WaitForSecondsRealtime(.4f);
        foreach (var pad in _pads) InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
        yield return new WaitForSecondsRealtime(1.5f);
        Check(!LevelUpUI.Instance.IsOffering && Time.timeScale == 1, "All four selections resume gameplay");
        foreach (var pad in _pads) InputSystem.QueueStateEvent(pad, new GamepadState());
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "gameplay.png"));
        yield return new WaitForEndOfFrame(); yield return null;
        var wave = WaveManager.Instance;
        while (wave.GetCurrentWave() == 0) yield return null;
        Check(wave.QueuedEnemies + wave.SpawnedEnemiesAlive == 16, "Wave one contains exactly four times its normal enemies");
        wave.StopAllCoroutines(); Time.timeScale = 0;
        players[0].transform.position = new Vector3(WorldCamera.Instance.Left + 2, 0);
        players[3].transform.position = new Vector3(WorldCamera.Instance.Right - 2, 0);
        yield return new WaitForSecondsRealtime(.7f);
        var arrows = (List<TMPro.TMP_Text>)typeof(CoopRunUI).GetField("_arrows", Private).GetValue(CoopRunUI.Instance);
        Check(Camera.main.orthographicSize <= WorldCamera.Instance.MaximumZoom + .01f && arrows[0].gameObject.activeSelf && arrows[3].gameObject.activeSelf, "Camera respects its zoom cap and shows both offscreen player arrows");
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "camera-arrows.png"));
        yield return new WaitForEndOfFrame(); yield return null;
        foreach (var player in players) player.transform.position = new Vector3(WorldCamera.Instance.Right - 2, 0, 0);
        Physics2D.SyncTransforms(); yield return new WaitForSecondsRealtime(.6f);
        bool spawnedBeyondRight = false, spawnedLeft = false;
        for (int i = 0; i < 30; i++)
        {
            Check(WorldCamera.Instance.TrySpawnPosition(wave.EnemyPrefabs[0], out var point), "Valid spawn exists near the map edge " + i);
            var view = Camera.main.WorldToViewportPoint(point);
            Check(point.x > WorldCamera.Instance.Left - WorldCamera.Instance.SpawnPadding && point.x < WorldCamera.Instance.Right + WorldCamera.Instance.SpawnPadding && (view.x < 0 || view.x > 1), "Spawn remains on extended terrain and outside the camera " + i);
            spawnedBeyondRight |= point.x > WorldCamera.Instance.Right; spawnedLeft |= point.x < players[0].transform.position.x;
        }
        Check(spawnedBeyondRight && spawnedLeft, "Enemies can spawn on both sides while players stand at the right edge");
        foreach (var player in players) player.transform.position = new Vector3(WorldCamera.Instance.Left + 2, 0, 0);
        Physics2D.SyncTransforms(); yield return new WaitForSecondsRealtime(.6f);
        bool spawnedBeyondLeft = false, spawnedRight = false;
        for (int i = 0; i < 30; i++)
        {
            Check(WorldCamera.Instance.TrySpawnPosition(wave.EnemyPrefabs[0], out var point), "Valid spawn exists at the left edge " + i);
            spawnedBeyondLeft |= point.x < WorldCamera.Instance.Left; spawnedRight |= point.x > players[0].transform.position.x;
        }
        Check(spawnedBeyondLeft && spawnedRight, "Enemies can spawn on both sides while players stand at the left edge");
        var enemyObject = Instantiate(wave.EnemyPrefabs[0], Camera.main.transform.position + new Vector3(0, 0, 10), Quaternion.identity);
        var enemy = enemyObject.GetComponent<EnemyBase>(); enemy.BaseHealth = 100; enemy.SpawnProtectionSeconds = 0; enemy.Initialize(1);
        Check(WorldCamera.Instance.PlayerWalls.All(w => Physics2D.GetIgnoreCollision(enemy.BodyCollider, w)), "Enemies can cross player boundary colliders");
        var projectile = ObjectPooler.Instance.GetPooledObject().GetComponent<Projectile>();
        players[1].Stats.GlobalDamageBonus = 1;
        var ballistic = new Projectile.BallisticData { Damage = 3, DamageMultiplier = 1, PierceCount = 10 };
        projectile.Initialize(ballistic, players[1].Stats);
        float hp = enemy.HealthRemaining; Call(projectile, "HitEnemy", enemy);
        Check(Mathf.Abs(hp - enemy.HealthRemaining - 6) < .001f && players[1].DamageDealt >= 6, "Projectile damage and attribution use its owner");
        projectile.Initialize(ballistic, players[2].Stats); hp = enemy.HealthRemaining; Call(projectile, "HitEnemy", enemy);
        Check(Mathf.Abs(hp - enemy.HealthRemaining - 3) < .001f && projectile.OwnerStats == players[2].Stats, "Pooled reuse replaces the previous owner's upgrades");
        Destroy(enemyObject); projectile.gameObject.SetActive(false);
        Time.timeScale = 1;
        Set(players[0].Health, "_isInvulnerable", false); players[0].Health.TakeDamage(10000);
        Check(players[0].Health.IsDead && !session.GameOver, "One death does not end co-op");
        Check(players[0].GetComponent<SpriteRenderer>().enabled && players[0].GetComponent<Animator>().GetBool("isDead") && players[0].GetComponent<SpriteRenderer>().sharedMaterial.GetColor("_PlayerColor") == players[0].Color, "Co-op death plays the animation and retains the player's palette");
        int upgrades = cards.GetRunPickups(basic.ID, players[0].Stats);
        session.BeginWave(); Check(players[0].Alive && players[0].Health.CurrentHealth == players[0].Health.MaxHealth && cards.GetRunPickups(basic.ID, players[0].Stats) == upgrades, "Next-wave respawn restores health and preserves upgrades");
        LocalCoopSession.Respawning = false; Set(players[0].Health, "_isInvulnerable", false); players[0].Health.TakeDamage(10000); session.BeginWave();
        Check(!players[0].Alive, "Respawn toggle is respected");
        for (int i = 1; i < players.Count; i++) { Set(players[i].Health, "_isInvulnerable", false); players[i].Health.TakeDamage(10000); }
        yield return null;
        Check(session.GameOver && Time.timeScale == 0, "Game over waits for all four deaths");
        Check(Time.unscaledTime < session.ResultsAt, "Results leave time for the final death animation");
        yield return new WaitForSecondsRealtime(2.1f);
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "results.png"));
        yield return new WaitForEndOfFrame(); yield return null;
        LocalCoopSession.RequestedPlayers = 3; LocalCoopSession.HealthStyle = LocalCoopSession.HealthBarStyle.Classic;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene"); yield return null; yield return null;
        EnemyTipUI.Instance.enabled = false;
        Check(LocalCoopSession.PlayerCount == 3 && LevelManager.Instance.TargetXP == 300, "Three-player run has three seats and triple XP target");
        Check(CoopRunUI.Instance.GetComponentsInChildren<SpriteHealthBar>().Length == 3, "Classic style clones the existing health artwork for every player");
        ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "classic-health.png")); yield return new WaitForEndOfFrame();
        LevelUpUI.Instance.ShowLevelUpOptions(); yield return new WaitForSecondsRealtime(.4f);
        Check(CoopRunUI.Instance.GetComponentsInChildren<CardDisplay>().Length == 9, "Three-player card screen offers three options per seat");
        LocalCoopSession.RequestedPlayers = 2; LocalCoopSession.KeyboardTest = true; LocalCoopSession.Respawning = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene"); yield return null; yield return null;
        EnemyTipUI.Instance.enabled = false;
        Check(LocalCoopSession.PlayerCount == 2 && !LocalCoopSession.Instance.Players[0].UsesGamepad && LocalCoopSession.Instance.Players[1].UsesGamepad, "Developer mode assigns keyboard and controller to different seats");
        Check(LevelManager.Instance.TargetXP == 200, "Two-player XP target scales by two");
        LocalCoopSession.RequestedPlayers = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene"); yield return null; yield return null;
        EnemyTipUI.Instance.enabled = false;
        Check(LocalCoopSession.PlayerCount == 1 && LevelManager.Instance.TargetXP == 100 && CoopRunUI.Instance == null, "Solo mode retains one player, normal XP, and its original HUD");
        Check(LocalCoopSession.Instance.Players[0].UsesGamepad, "A connected controller takes priority in solo gameplay");
        foreach (var pad in _pads) InputSystem.RemoveDevice(pad); yield return null; yield return null;
        Check(!LocalCoopSession.Instance.Players[0].UsesGamepad, "Solo falls back to keyboard and mouse after disconnect");
        InputSystem.AddDevice(_pads[0]); yield return null; yield return null;
        Check(LocalCoopSession.Instance.Players[0].UsesGamepad && LocalCoopSession.Instance.Players[0].Controller == _pads[0], "Solo hotplug switches back to the connected controller");
        WaveManager.Instance.StopAllCoroutines();
    }
    void OnDestroy()
    {
        foreach (var pad in _pads) if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        foreach (var pad in _originalPads) if (pad != null && !pad.added) InputSystem.AddDevice(pad);
    }
}
#endif
