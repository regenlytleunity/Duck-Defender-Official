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
        Directory.CreateDirectory("tmp/update12/tutorial");
        DontDestroyOnLoad(gameObject);
        while (!TutorialDirector.Practice || LocalCoopSession.Instance == null || LocalCoopSession.Instance.Players.Count == 0) yield return null;
        foreach (var pad in Gamepad.all.ToArray()) if (pad.enabled) { _realPads.Add(pad); InputSystem.DisableDevice(pad); }
        _pad = InputSystem.AddDevice<Gamepad>(); ControllerBindings.Reset(); InputManager.Instance.ResetToDefaults();
        Player.AssignController(_pad, true);
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
        Result = result; Directory.CreateDirectory("tmp/update12/tutorial");
        File.WriteAllText("tmp/update12/tutorial/play-verification.txt", result + "\n" + string.Join("\n", _checks));
        Debug.Log(result); Destroy(gameObject);
    }
    void OnDestroy()
    {
        if (_pad != null && _pad.added) InputSystem.RemoveDevice(_pad);
        foreach (var pad in _realPads) if (pad.added) InputSystem.EnableDevice(pad);
    }
    void Update()
    {
        // Scene reloads must bind the probe's device, even when a disabled real pad
        // remains first in Gamepad.all. Never send test input to the user's controller.
        if (_pad != null && LocalCoopSession.Instance != null && LocalCoopSession.Instance.Players.Count > 0 && Player.Controller != _pad)
            Player.AssignController(_pad, true);
    }
    void Input(Vector2 move = default, Vector2 aim = default, bool shoot = false)
    { InputSystem.QueueStateEvent(_pad, new GamepadState { leftStick = move, rightStick = aim, rightTrigger = shoot ? 1 : 0 }); }
    IEnumerator Until(Func<bool> predicate, string message, float timeout = 12)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
        Check(predicate(), message);
    }
    IEnumerator Continue(bool pointer = false)
    {
        Input();
        var director = TutorialDirector.Instance;
        if (Field<bool>(director, "_collapsed")) { Call(director, "ExpandDialogue"); yield return null; }
        string prompt = Field<string>(director, "_lastPrompt");
        yield return Until(() => director == null || Field<TMPro.TMP_Text>(director, "_message").maxVisibleCharacters >= Field<TMPro.TMP_Text>(director, "_message").textInfo.characterCount, "Dialogue finishes revealing before continue.");
        yield return new WaitForSecondsRealtime(.5f);
        Check(director != null && Field<string>(director, "_lastPrompt") == prompt, "Prompt waits for Interact: " + prompt);
        if (pointer)
        {
            var button = Field<UnityEngine.UI.Button>(director, "_continueButton");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(data, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "Continue button receives pointer raycasts.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
        else InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.South));
        yield return null; yield return null;
        yield return Until(() => director == null || TutorialDirector.Instance != director || Field<string>(director, "_lastPrompt") != prompt,
            "Interact advances: " + prompt);
        Input(); yield return new WaitForSecondsRealtime(.1f);
    }
    IEnumerator Submit(UnityEngine.UI.Button button)
    {
        Input(); yield return new WaitForSecondsRealtime(.25f);
        Check(button != null && button.IsActive() && button.IsInteractable(), "Reachable menu control: " + (button != null ? button.name : "missing"));
        if (MainMenuUI.Instance != null) Check(button.transform.IsChildOf(MainMenuUI.Instance.NavigationScope.transform), "Control belongs to current navigation scope: " + button.name);
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.South));
        yield return new WaitForSecondsRealtime(.08f); Input(); yield return new WaitForSecondsRealtime(.3f);
    }
    UnityEngine.UI.Button MenuButton(GameObject panel, string name) => panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == name);
    IEnumerator MoveTo(float x)
    {
        float end = Time.realtimeSinceStartup + 6;
        while (Mathf.Abs(Player.transform.position.x - x) > .5f && Time.realtimeSinceStartup < end)
        { Input(new Vector2(Mathf.Clamp((x - Player.transform.position.x) * .6f, -.65f, .65f), 0)); yield return null; }
        Input(); yield return new WaitForSecondsRealtime(.1f);
        Check(Mathf.Abs(Player.transform.position.x - x) < .7f, "Controller movement reaches practice goal.");
    }
    IEnumerator ReadPrompt()
    {
        Call(TutorialDirector.Instance, "ExpandDialogue");
        yield return Until(() => Field<TMPro.TMP_Text>(TutorialDirector.Instance, "_message").maxVisibleCharacters >= Field<TMPro.TMP_Text>(TutorialDirector.Instance, "_message").textInfo.characterCount, "Capture waits for complete dialogue.");
        yield return null;
    }
    IEnumerator Run()
    {
        yield return Until(() => TutorialDirector.Practice && Step == "Welcome", "Fresh save automatically enters stage 1.");
        int frame = Time.frameCount; yield return null; yield return null;
        Check(Time.frameCount > frame, "Game frames advance in Play Mode.");
        Check(WaveManager.Instance.GetCurrentWave() == 0 && EnemyBase.ActiveEnemies.Count == 0, "Practice suppresses normal waves.");
        Check(!TutorialDirector.AllowMovement && !TutorialDirector.AllowShooting, "Welcome locks gameplay controls.");
        Check(TutorialDirector.Instance.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(b => b.name == "Continue"), "Dialogue has a clickable Continue button.");
        var message = Field<TMPro.TMP_Text>(TutorialDirector.Instance, "_message");
        message.ForceMeshUpdate();
        Check(message.textInfo.characterCount == message.text.Length && message.maxVisibleCharacters < message.textInfo.characterCount, "Typewriter preloads the complete text before revealing it.");
        var linePositions = message.textInfo.characterInfo.Select(c => c.origin).ToArray();
        yield return Until(() => message.maxVisibleCharacters >= message.textInfo.characterCount, "Welcome text completes its reveal.");
        message.ForceMeshUpdate();
        Check(linePositions.SequenceEqual(message.textInfo.characterInfo.Select(c => c.origin)), "Character layout stays fixed throughout reveal.");
        Check(ControllerBindings.Get("Confirm") == "buttonSouth" && ControllerBindings.Get("Back") == "buttonEast" &&
            ControllerBindings.Get("Tutorial") == "buttonNorth" && ControllerBindings.Get("SkipTutorial") == "buttonWest", "A/B/Y/X defaults apply to tutorial controls.");
        yield return Until(() => Field<float>(TutorialDirector.Instance, "_collapse") > 0, "Dialogue begins its collapse after reading time.", 14);
        yield return new WaitForEndOfFrame();
        var bubble = Field<RectTransform>(TutorialDirector.Instance, "_panel");
        Check(bubble.pivot.x == 1 && bubble.localScale.x < 1 && Field<CanvasGroup>(TutorialDirector.Instance, "_dialogueFade").alpha == 1,
            "Speech bubble collapses right toward the duck without fading.");
        var tailMesh = Field<RectTransform>(TutorialDirector.Instance, "_tail").GetComponent<CanvasRenderer>().GetMesh();
        Check(tailMesh != null && tailMesh.vertexCount > 0 && tailMesh.colors.All(c => Mathf.Abs(c.a - .76f) < .01f), "Tail fill and outline retain the bubble transparency.");
        yield return Until(() => !Field<RectTransform>(TutorialDirector.Instance, "_panel").gameObject.activeSelf, "Dialogue hides after its reading time.", 14);
        Check(!Field<RectTransform>(TutorialDirector.Instance, "_panel").gameObject.activeSelf && !Field<RectTransform>(TutorialDirector.Instance, "_portrait").gameObject.activeSelf, "Popup and duck fully hide without leaving a collapsed panel.");
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/hidden.png");
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.North));
        yield return null; yield return null; Input();
        Check(Field<RectTransform>(TutorialDirector.Instance, "_panel").gameObject.activeSelf && Step == "Welcome", "Show tutorial binding restores text without advancing the lesson.");
        yield return new WaitForSecondsRealtime(.1f);
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.West));
        yield return Until(() => TutorialDirector.SkipConfirmationOpen, "X opens skip confirmation.");
        Input();
        Check(TutorialDirector.SkipConfirmationOpen && Time.timeScale == 0 && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == Field<UnityEngine.UI.Button>(TutorialDirector.Instance, "_keepTutorialButton").gameObject, "Skip confirmation pauses and defaults to Keep playing.");
        Call(TutorialDirector.Instance, "ConfirmSkip");
        Check(TutorialDirector.CoreActive && TutorialDirector.SkipConfirmationOpen, "Opening input cannot accidentally confirm skipping.");
        yield return new WaitForSecondsRealtime(.4f);
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/skip-confirmation.png");
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.South));
        yield return null; yield return null; Input();
        Check(!TutorialDirector.SkipConfirmationOpen && Step == "Welcome", "Default controller confirmation cancels skipping and preserves the lesson.");
        yield return new WaitForSecondsRealtime(.3f);
        var decor = GameUI.Instance.PlayerHealthBar.transform.parent;
        Check(!decor.Find("HeartDecor").gameObject.activeSelf && !decor.Find("ExpDecor").gameObject.activeSelf && !decor.Find("CoinDecor").gameObject.activeSelf, "All HUD decorations are hidden before their lessons.");
        yield return new WaitForSecondsRealtime(.4f);
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/welcome.png");
        yield return Continue(true);
        var objects = Field<List<GameObject>>(TutorialDirector.Instance, "_practiceObjects");
        var stick = Field<RectTransform>(TutorialDirector.Instance, "_stick");
        Canvas.ForceUpdateCanvases();
        var circleRect = (RectTransform)stick.parent;
        Check(Mathf.Abs(circleRect.rect.width - circleRect.rect.height) < .01f && Mathf.Abs(stick.rect.width - stick.rect.height) < .01f, "Thumbstick base and knob remain circular.");
        Check(objects.All(g => g.GetComponent<SpriteRenderer>().sprite.texture.name == "Tutorial goal gradient"), "Walking goals use the upward fading gradient.");
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/movement.png");
        var padBounds = objects[0].GetComponent<SpriteRenderer>().bounds;
        var body = Player.GetComponent<Collider2D>();
        Player.transform.position += Vector3.right * (padBounds.max.x - body.bounds.min.x - .02f);
        yield return null; yield return null;
        Check(Player.transform.position.x > padBounds.max.x && Field<bool[]>(TutorialDirector.Instance, "_visited")[0], "Grazing a pad with the collider counts with the duck center outside.");
        Check(!Field<bool>(TutorialDirector.Instance, "_ready"), "One movement goal cannot skip the other.");
        Player.transform.position += Vector3.down * 30; yield return null; yield return null;
        Check(Player.transform.position.y > Field<Vector3>(TutorialDirector.Instance, "_origin").y - 2, "Falling out of practice returns the player to the lesson.");
        Check(Field<bool[]>(TutorialDirector.Instance, "_visited")[0], "Automatic fall recovery preserves completed objectives.");
        yield return MoveTo(objects[0].transform.position.x);
        yield return MoveTo(objects[1].transform.position.x);
        yield return Continue();
        var box = Field<BoxCollider2D>(TutorialDirector.Instance, "_box");
        Check(objects[1].GetComponent<SpriteRenderer>().sprite.texture.name == "Tutorial goal gradient", "Box landing goal also uses the gradient.");
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/jump.png");
        yield return new WaitForSecondsRealtime(.6f);
        Input(Vector2.right); yield return new WaitForSecondsRealtime(.45f); Input();
        Check(!Field<bool>(TutorialDirector.Instance, "_ready"), "Walking into the side of the box cannot count as a landing.");
        Call(TutorialDirector.Instance, "ResetPlayer");
        yield return new WaitForSecondsRealtime(.3f);
        float start = Time.realtimeSinceStartup;
        while (!Field<bool>(TutorialDirector.Instance, "_ready") && Time.realtimeSinceStartup - start < 5)
        {
            float x = Mathf.Clamp((box.bounds.center.x - Player.transform.position.x) * .6f, -.6f, .6f);
            Input(new Vector2(x, Time.realtimeSinceStartup - start < .65f ? 1 : 0)); yield return null;
        }
        Input(); Check(Field<bool>(TutorialDirector.Instance, "_ready"), "Actual controller jump lands on the box.");
        yield return new WaitForSecondsRealtime(.35f);
        Check(objects[1].GetComponent<SpriteRenderer>().color.a <= .26f, "Box glow fades after landing, like the movement goals.");
        yield return Continue();
        var targets = Field<List<TutorialTarget>>(TutorialDirector.Instance, "_targets");
        Check(targets.All(t => t.GetComponent<SpriteRenderer>().sprite.texture.name == "Tutorial dartboard") && Vector3.Distance(targets[0].transform.position, Player.Weapon.FirePoint.position) > Mathf.Clamp(Camera.main.orthographicSize * .55f, 2.5f, 5), "Dartboards are farther away than the previous targets.");
        Input(); yield return new WaitForSecondsRealtime(.3f);
        Check(!Field<bool[]>(TutorialDirector.Instance, "_visited").Any(v => v), "Idle default aim cannot complete a target.");
        Canvas.ForceUpdateCanvases();
        var speech = Field<RectTransform>(TutorialDirector.Instance, "_panel");
        var speechCorners = new Vector3[4]; speech.GetWorldCorners(speechCorners);
        float speechBottom = RectTransformUtility.WorldToScreenPoint(null, speechCorners[0]).y;
        Check(speech.anchorMin == new Vector2(.22f, .77f) && speech.anchorMax == new Vector2(.80f, .98f) && speech.GetComponent<UnityEngine.UI.Image>().color.a < .8f, "Aim uses the same fixed translucent popup as other steps.");
        Check(targets[0].transform.position.y > Player.Weapon.FirePoint.position.y + .8f, "Lowest targets are raised above default horizontal aim.");
        var arrow = Player.transform.Find("Aim direction").GetComponent<SpriteRenderer>();
        Check(arrow != null && arrow.sprite.texture.name == "Outlined aim arrow" && Player.GetComponentsInChildren<LineRenderer>().All(l => l.name != "Aim outline"), "Aim arrow uses one closed silhouette without crossing lines.");
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/aim.png");
        var originalScale = Player.transform.localScale; var originalAim = Player.Aim;
        var aimField = typeof(LocalPlayer).GetField("<Aim>k__BackingField", Private);
        foreach (var direction in new[] { Vector2.right, Vector2.up, new Vector2(.6f, .8f), new Vector2(-.6f, .8f) })
            foreach (float sign in new[] { -1f, 1f })
            {
                Player.transform.localScale = new Vector3(Mathf.Abs(originalScale.x) * sign, originalScale.y, originalScale.z);
                aimField.SetValue(Player, direction); Call(Player, "LateUpdate");
                Check(Vector2.Dot(arrow.transform.TransformVector(Vector3.right).normalized, direction) > .999f, "Arrow follows actual aim with facing " + sign + " and direction " + direction);
            }
        Player.transform.localScale = originalScale; aimField.SetValue(Player, originalAim); Call(Player, "LateUpdate");
        Check(Step == "Shoot" && targets.Count == 5 && TutorialDirector.AllowShooting, "Aiming and shooting share one set of five targets.");
        foreach (var target in targets)
        {
            Input(aim: (target.transform.position - Player.Weapon.FirePoint.position).normalized, shoot: true);
            yield return Until(() => !target.gameObject.activeSelf, "A real feather destroys target " + target.Index, 5);
            var burst = GameObject.Find("Target burst");
            Check(!ParticleVisibility.Enabled || burst != null && burst.GetComponent<ParticleSystem>().particleCount > 0, "Destroyed target emits colored particles.");
            if (burst != null && target.Index == 0)
            {
                var samples = new ParticleSystem.Particle[28]; int count = burst.GetComponent<ParticleSystem>().GetParticles(samples);
                Check(count > 0 && Vector3.Distance(samples[0].position, target.transform.position) < 1, "Burst particles originate at the struck target.");
                ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/particles.png");
            }
        }
        Input(); yield return Continue();
        var approaching = EnemyBase.ActiveEnemies.ToArray();
        Check(approaching.Length == 2 && approaching.All(e => { var p = Camera.main.WorldToViewportPoint(e.BodyCollider.bounds.center); return p.x < 0 || p.x > 1; }), "Practice enemies start outside the camera.");
        Vector3 practiceOrigin = Field<Vector3>(TutorialDirector.Instance, "_origin");
        float[] distances = approaching.Select(e => Mathf.Abs(e.transform.position.x - practiceOrigin.x)).ToArray();
        bool monotonic = true;
        float approachEnd = Time.realtimeSinceStartup + 15;
        approaching[0].TakeDamage(100, Player.Stats);
        Check(approaching[0].HealthRemaining == 2, "Leftover feathers cannot kill an enemy during its approach.");
        while (Step == "Approach" && Time.realtimeSinceStartup < approachEnd)
        {
            for (int i = 0; i < approaching.Length; i++)
            {
                float distance = Mathf.Abs(approaching[i].transform.position.x - practiceOrigin.x);
                monotonic &= distance <= distances[i] + .02f && distance >= 3.98f;
                distances[i] = distance;
            }
            yield return null;
        }
        Check(Step == "Combat", "Both enemies reach their stops and unlock combat.");
        Check(monotonic, "Enemies approach monotonically without overshooting or rubberbanding.");
        Check(approaching.All(e => Mathf.Abs(Mathf.Abs(e.transform.position.x - practiceOrigin.x) - 4) < .05f), "Both enemies freeze at the designated points.");
        Check(EnemyBase.ActiveEnemies.Count == 2 && EnemyBase.ActiveEnemies.All(e => e.HealthRemaining == 2), "Exactly two practice enemies have 2 HP.");
        var enemies = EnemyBase.ActiveEnemies.ToArray();
        var practiceDropRange = enemies[0].CoinDropRange;
        enemies[0].TakeDamage(2, Player.Stats); yield return null;
        Check(LevelManager.Instance.CurrentLevel == 1 && LevelManager.Instance.CurrentXP == LevelManager.Instance.TargetXP / 2, "First enemy awards exactly half the XP bar.");
        enemies[1].TakeDamage(2, Player.Stats); yield return null;
        Check(LevelUpUI.Instance.IsOffering && LevelManager.Instance.CurrentLevel == 2, "Second enemy opens the level-up offer.");
        var options = LevelUpUI.Instance.CardContainer.GetComponentsInChildren<CardDisplay>();
        Check(options.Length == 3 && options.All(c => c.ClickButton.IsInteractable()), "All three practice cards remain focusable.");
        var healthGlow = options[0].BackgroundImage.GetComponent<TutorialGlowFollower>().Glow;
        Check(healthGlow.transform.parent == options[0].BackgroundImage.transform.parent && healthGlow.transform.GetSiblingIndex() < options[0].BackgroundImage.transform.GetSiblingIndex() && !healthGlow.raycastTarget, "Required Health glow renders behind its graphic and does not intercept input.");
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/cards.png");
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(options[0].ClickButton.gameObject);
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.DpadRight));
        yield return new WaitForSecondsRealtime(.08f); Input();
        Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == options[1].ClickButton.gameObject, "Controller navigation reaches the second card.");
        yield return new WaitForSecondsRealtime(.25f);
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.DpadRight));
        yield return new WaitForSecondsRealtime(.08f); Input();
        Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == options[2].ClickButton.gameObject, "Controller navigation reaches the third card.");
        foreach (var option in options.Where(c => c.AssignedCard.ID != "bas_health"))
        {
            option.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            Check(Field<bool>(option, "_isFlipping"), "Hover inspection works for " + option.AssignedCard.ID);
            yield return Submit(option.ClickButton);
            Check(LevelUpUI.Instance.IsOffering && Step == "Cards", "Activating " + option.AssignedCard.ID + " cannot advance the lesson.");
        }
        yield return new WaitForSecondsRealtime(.35f);
        LevelUpUI.Instance.SelectCard(options.First(c => c.AssignedCard.ID == "bas_damage").AssignedCard);
        Check(Time.unscaledTime - Field<float>(TutorialDirector.Instance, "_rejectedAt") < .05f, "Blocked card selection starts brief screen feedback while paused.");
        yield return new WaitForSecondsRealtime(.3f);
        Check(Field<Vector2>(TutorialDirector.Instance, "_shakeOffset") == Vector2.zero, "Rejection shake settles without leaving a UI offset.");
        Check(LevelUpUI.Instance.IsOffering, "Direct selection cannot bypass the required health card.");
        LevelUpUI.Instance.SelectCard(options.First(c => c.AssignedCard.ID == "bas_health").AssignedCard);
        yield return null; Check(GameUI.Instance.PlayerHealthBar.gameObject.activeSelf && Time.timeScale == 1, "Health lesson reveals the numeric bar and releases card pause.");
        Canvas.ForceUpdateCanvases();
        var healthRect = (RectTransform)GameUI.Instance.PlayerHealthBar.transform;
        var xpRect = (RectTransform)GameUI.Instance.XPBar.transform;
        Check(healthRect.anchorMax.x == .23f && healthRect.anchorMax.y < .94f && xpRect.anchorMax.x == healthRect.anchorMax.x && GameUI.Instance.XPBar.Overlay.sprite == GameUI.Instance.PlayerHealthBar.Overlay.sprite, "Health and XP share longer classic bars below the top margin.");
        Check(!decor.Find("ExpDecor").gameObject.activeSelf, "XP orb stays removed after XP is revealed.");
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/health.png");
        yield return Until(() => LevelManager.Instance.TotalCoins >= practiceDropRange.x * 2 && FindObjectsByType<Coin>(FindObjectsSortMode.None).Length == 0, "Practice coins fly to the player.");
        int earned = LevelManager.Instance.TotalCoins;
        Check(earned <= practiceDropRange.y * 2, "Both enemies use their ordinary coin drop ranges.");
        Check(GameUI.Instance.CoinText.gameObject.activeSelf && decor.Find("CoinDecor").gameObject.activeSelf &&
            GameUI.Instance.CoinText.rectTransform.anchorMax.y < xpRect.anchorMin.y, "Coin HUD is visible below XP.");
        yield return Continue(); yield return Continue();
        yield return Until(() => MainMenuUI.Instance != null && TutorialDirector.Progress.Stage == TutorialStage.Shop, "Practice returns directly to the shop lesson.");
        yield return new WaitForSecondsRealtime(.5f);
        Check(ShopManager.Instance.CurrentCoins == earned + 200, "Shop balance includes every pickup and the practice bonus.");
        var menu = MainMenuUI.Instance;
        yield return Submit(MenuButton(menu.MenuPanel, "ShopButton"));
        var packs = menu.PackContainer.GetComponentsInChildren<UnityEngine.UI.Button>();
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(packs[0].gameObject);
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.DpadRight));
        yield return new WaitForSecondsRealtime(.1f); Input(); yield return null;
        var focusedPack = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        Check(focusedPack != packs[0].gameObject && focusedPack.transform.IsChildOf(menu.PackContainer), "Controller can move away from the first pack.");
        var glows = menu.PackContainer.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i => i.name == "Tutorial green glow").ToArray();
        var packGlow = focusedPack.GetComponentInChildren<TutorialGlowFollower>();
        Check(glows.Length == 1 && glows[0] == packGlow.Glow && glows[0].transform.GetSiblingIndex() < packGlow.transform.GetSiblingIndex(), "One green underglow follows the focused pack behind its visible art.");
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/shop.png");
        yield return Submit(focusedPack.GetComponent<UnityEngine.UI.Button>());
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/confirmation.png");
        yield return Submit(menu.YesButton);
        int coins = ShopManager.Instance.CurrentCoins;
        menu.BackToMainMenu(); Check(menu.OpeningOverlay.activeSelf, "Back cannot escape an unopened tutorial pack.");
        SceneManager.LoadScene("MainMenu"); yield return new WaitForSecondsRealtime(1);
        menu = MainMenuUI.Instance;
        Check(menu.OpeningOverlay.activeSelf && ShopManager.Instance.CurrentCoins == coins, "Reload restores purchased pack without charging again.");
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.South));
        yield return new WaitForSecondsRealtime(.08f); Input();
        yield return Until(() => menu.TutorialPackRevealed, "Pack reveal completes.");
        Check(!menu.ContinueButton.gameObject.activeSelf, "Tutorial pack reveal has no manual Continue button.");
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/pack.png");
        yield return Continue();
        Check(!menu.OpeningOverlay.activeSelf && menu.ShopPanel.activeSelf, "Interact closes the pack reveal without activating a pack.");
        yield return Submit(MenuButton(menu.ShopPanel, "BackButton"));
        yield return Submit(MenuButton(menu.MenuPanel, "IndexButton"));
        var index = menu.IndexPanel.GetComponent<CardIndexUI>();
        int page = index.PageForCard(TutorialDirector.Progress.UpgradeCardID);
        while (index.CurrentPage < page) yield return Submit(index.NextButton);
        while (index.CurrentPage > page) yield return Submit(index.PreviousButton);
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/index.png");
        Check(TutorialDirector.Progress.Stage == TutorialStage.Upgrade, "Browsing enters the upgrade lesson without an extra confirmation.");
        var targetPack = index.SelectedPack;
        yield return Submit(index.PackButtons[((int)targetPack + 1) % 4]);
        Check(index.SelectedPack != targetPack && TutorialDirector.Progress.Stage == TutorialStage.Upgrade, "Pack browsing does not escape the upgrade lesson.");
        yield return Submit(index.PackButtons[(int)targetPack]);
        while (index.CurrentPage < page) yield return Submit(index.NextButton);
        while (index.CurrentPage > page) yield return Submit(index.PreviousButton);
        var upgradeID = TutorialDirector.Progress.UpgradeCardID;
        yield return Submit(index.ContentArea.GetComponentsInChildren<CardDisplay>().First(c => c.AssignedCard.ID == upgradeID).ClickButton);
        yield return Submit(menu.IndexPanel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name.StartsWith("Upgrade - ")));
        Check(ShopManager.Instance.GetCardData(upgradeID).Level == 2, "Guided index upgrade succeeds.");
        yield return Submit(MenuButton(menu.IndexPanel, "BackButton"));
        Check(!TutorialDirector.CoreActive, "Core tutorial completes and unlocks normal menus.");
        yield return Until(() => !Field<RectTransform>(TutorialDirector.Instance, "_panel").gameObject.activeSelf, "Completion message fully hides after its reading window.", 16);
        Check(Field<UnityEngine.UI.Button>(TutorialDirector.Instance, "_showTutorialButton").gameObject.activeSelf && !Field<UnityEngine.UI.Button>(TutorialDirector.Instance, "_skipButton").gameObject.activeSelf, "Completion retains recall without offering another skip.");
        InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.North));
        yield return null; yield return null; Input();
        Check(Field<RectTransform>(TutorialDirector.Instance, "_panel").gameObject.activeSelf && TutorialDirector.MenuLocked, "Recall restores the completion message without consuming its pending action.");
        yield return Continue();
        Check(SceneManager.GetActiveScene().name == "MainMenu" && !TutorialDirector.MenuLocked, "Completion Interact does not start an unintended game.");
        var shop = ShopManager.Instance;
        shop.Progress.TotalCoins = 10000; SaveSystem.SaveData(shop.Progress); shop.LoadEconomy();
        yield return Submit(MenuButton(menu.MenuPanel, "ShopButton")); shop.RefreshItemShop();
        Check(shop.BuyItem(0, shop.Progress.ItemShopStock[0], shop.Progress.ItemShopRefreshUtc), "First purchased item starts item lesson.");
        yield return null; yield return null;
        var panel = Field<RectTransform>(TutorialDirector.Instance, "_panel");
        Check(panel.GetComponent<UnityEngine.UI.Image>().color == new Color(.025f, .04f, .065f, .76f), "Item tutorial restores the normal prompt color after chromatic completion.");
        yield return ReadPrompt();
        ScreenCapture.CaptureScreenshot("tmp/update12/tutorial/item.png");
        yield return Submit(MenuButton(menu.ShopPanel, "BackButton"));
        yield return Submit(MenuButton(menu.MenuPanel, "IndexButton"));
        yield return Submit(MenuButton(menu.IndexPanel, "ItemsTab"));
        var equipment = menu.IndexPanel.GetComponent<IndexLoadoutUI>();
        Check(equipment.ItemsMode && TutorialDirector.Progress.Items == ItemTutorialStage.Equip,
            "Items tab opens equipment after the core lesson (mode=" + equipment.ItemsMode + ", stage=" + TutorialDirector.Progress.Items + ").");
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
        yield return VerifySkipping();
    }

    IEnumerator VerifySkipping()
    {
        foreach (var stage in new[] { TutorialStage.Basics, TutorialStage.Shop, TutorialStage.PackPurchased, TutorialStage.Upgrade, TutorialStage.Complete })
        {
            var data = PlayerData.CreateNew(); data.TotalCoins = 432; data.CompletedRuns = 2;
            data.CardCollection[0].Level = 3; data.ItemInventory[(int)ItemKind.Bomb] = 2;
            data.Tutorial.Stage = stage; data.Tutorial.HasObtainedItem = true;
            data.Tutorial.Items = ItemTutorialStage.Index;
            data.Tutorial.UpgradeCardID = data.CardCollection[0].CardID;
            data.Tutorial.NewCardID = data.CardCollection[1].CardID;
            SaveSystem.SaveData(data, true, true);
            Time.timeScale = 1; SceneManager.LoadScene(stage == TutorialStage.Basics ? "SampleScene" : "MainMenu");
            yield return new WaitForSecondsRealtime(.8f);
            var director = TutorialDirector.Instance;
            ClickTutorialControl(Field<UnityEngine.UI.Button>(director, "_skipButton"));
            yield return new WaitForSecondsRealtime(.45f);
            Input(); ClickTutorialControl(Field<UnityEngine.UI.Button>(director, "_confirmSkipButton"));
            yield return Until(() => MainMenuUI.Instance != null && !TutorialDirector.CoreActive && !TutorialDirector.Progress.ItemActive, "Confirmed skip exits " + stage + " safely.");
            yield return new WaitForSecondsRealtime(.3f);
            var saved = SaveSystem.LoadData();
            Check(saved.TotalCoins == 432 && saved.CompletedRuns == 2 && saved.CardCollection[0].Level == 3 && saved.ItemInventory[(int)ItemKind.Bomb] == 2, "Skipping " + stage + " preserves earned resources without tutorial rewards.");
            Check(!TutorialDirector.HoldsPause && Time.timeScale == 1 && !TutorialDirector.MenuLocked && !TutorialDirector.Practice, "Skipping " + stage + " releases pause, practice and menu gates.");
        }
    }
    void ClickTutorialControl(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var events = UnityEngine.EventSystems.EventSystem.current;
        var pointer = new UnityEngine.EventSystems.PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        events.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "Skip control receives pointer input above other overlays: " + button.name);
        UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    }
}
#endif
