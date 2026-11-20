using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Uses the existing card artwork and CardDisplay, with one independently controlled panel per seat.
public class CoopRunUI : MonoBehaviour
{
    public static CoopRunUI Instance { get; private set; }
    RectTransform _canvas, _offer, _results;
    TMP_Text _connection;
    readonly List<RectTransform> _bars = new List<RectTransform>();
    readonly List<UnityEngine.UI.Image> _fills = new List<UnityEngine.UI.Image>();
    readonly List<TMP_Text> _healthLabels = new List<TMP_Text>(), _arrows = new List<TMP_Text>();
    readonly List<TMP_Text> _names = new List<TMP_Text>();
    readonly List<Material> _healthMaterials = new List<Material>();
    readonly List<List<CardDefinition>> _options = new List<List<CardDefinition>>();
    readonly List<List<UnityEngine.UI.Button>> _buttons = new List<List<UnityEngine.UI.Button>>();
    readonly List<TMP_Text> _readyLabels = new List<TMP_Text>();
    readonly int[] _selected = new int[4];
    readonly bool[] _ready = new bool[4];
    readonly float[] _nextMove = new float[4];
    readonly ControllerSelectionFrame[] _selectionFrames = new ControllerSelectionFrame[4];
    LevelUpUI _levelUI;
    float _selectAfter;
    float _commitAt;
    bool Classic => LocalCoopSession.HealthStyle == LocalCoopSession.HealthBarStyle.Classic;

    void Awake() { Instance = this; }
    void Start()
    {
        if (_canvas != null) return;
        _canvas = CoopUIElements.Canvas("Co-op HUD", transform, 900);
        _connection = CoopUIElements.Text(_canvas, "Controller disconnected. Reconnect it to continue.", new Vector2(.15f, .85f), new Vector2(.85f, .94f), 32);
        _connection.gameObject.SetActive(false);
        var ui = GameUI.Instance;
        if (ui != null && ui.XPBar != null)
        {
            var hud = ui.XPBar.transform.parent;
            foreach (string decoration in new[] { "HeartDecor", "ExpDecor" })
            { var item = hud.Find(decoration); if (item != null) item.gameObject.SetActive(false); }
            var coin = hud.Find("CoinDecor") as RectTransform;
            if (coin != null) CoopUIElements.Stretch(coin, new Vector2(.012f, .937f), new Vector2(.03f, .979f));
            var xp = ui.XPBar;
            xp.Background.preserveAspect = xp.Overlay.preserveAspect = false;
            CoopUIElements.Stretch((RectTransform)xp.transform, new Vector2(.2f, .945f), new Vector2(.8f, .99f));
            CoopUIElements.Stretch(xp.Background.rectTransform, Vector2.zero, Vector2.one);
            CoopUIElements.Stretch(xp.Overlay.rectTransform, Vector2.zero, Vector2.one);
            xp.Overlay.rectTransform.offsetMin = new Vector2(6, 6); xp.Overlay.rectTransform.offsetMax = new Vector2(-6, -6);
            if (ui.LevelText != null) { CoopUIElements.Stretch(ui.LevelText.rectTransform, new Vector2(.43f, .9f), new Vector2(.57f, .945f)); CoopUIElements.WhiteInfill(ui.LevelText); }
            if (ui.WaveText != null) CoopUIElements.Stretch(ui.WaveText.rectTransform, new Vector2(.81f, .93f), new Vector2(.99f, .99f));
            if (ui.EnemiesLeftText != null) CoopUIElements.Stretch(ui.EnemiesLeftText.rectTransform, new Vector2(.81f, .87f), new Vector2(.99f, .93f));
            if (ui.CoinText != null) CoopUIElements.Stretch(ui.CoinText.rectTransform, new Vector2(.035f, .925f), new Vector2(.18f, .98f));
            foreach (var label in new TMP_Text[] { ui.LevelText, ui.WaveText, ui.EnemiesLeftText, ui.CoinText })
                if (label != null)
                {
                    label.gameObject.SetActive(true); label.margin = Vector4.zero; label.alignment = TextAlignmentOptions.Center;
                    label.enableAutoSizing = true; label.fontSizeMax = 32; label.fontSizeMin = 18; CoopUIElements.WhiteInfill(label);
                }
        }
        foreach (var player in LocalCoopSession.Instance.Players)
        {
            RectTransform bar; UnityEngine.UI.Image fill;
            if (Classic && ui != null && ui.PlayerHealthBar != null)
            {
                var classic = Instantiate(ui.PlayerHealthBar, _canvas); classic.name = "Classic health P" + (player.Index + 1);
                classic.gameObject.SetActive(true); bar = (RectTransform)classic.transform;
                CoopUIElements.Stretch(bar, new Vector2(.02f, .81f - player.Index * .095f), new Vector2(.19f, .85f - player.Index * .095f));
                CoopUIElements.Stretch(classic.Background.rectTransform, Vector2.zero, Vector2.one);
                fill = classic.Overlay; CoopUIElements.Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
                classic.Background.preserveAspect = fill.preserveAspect = false;
                fill.rectTransform.offsetMin = new Vector2(5, 5); fill.rectTransform.offsetMax = new Vector2(-5, -5);
                var material = new Material(Resources.Load<Shader>("HealthPalette")); material.SetColor("_PlayerColor", player.Color);
                fill.material = material; _healthMaterials.Add(material);
            }
            else
            {
                bar = CoopUIElements.Panel(_canvas, "Player health", Vector2.zero, Vector2.zero, new Color(.08f, .1f, .15f, .9f));
                bar.sizeDelta = new Vector2(140, 28); bar.pivot = new Vector2(.5f, .5f);
                fill = CoopUIElements.Panel(bar, "Health", Vector2.zero, Vector2.one, player.Color).GetComponent<UnityEngine.UI.Image>();
            }
            fill.raycastTarget = false;
            _bars.Add(bar); _fills.Add(fill);
            var labelBackground = CoopUIElements.Panel(bar, "Health label", Classic ? new Vector2(0, 1.05f) : new Vector2(0, .22f), Classic ? new Vector2(1, 1.85f) : Vector2.one, new Color(.04f, .06f, .09f, .88f));
            labelBackground.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            _healthLabels.Add(CoopUIElements.Text(labelBackground, "", Vector2.zero, Vector2.one, Classic ? 22 : 18));
            var name = CoopUIElements.Text(_canvas, player.DisplayName, Vector2.zero, Vector2.zero, 20);
            name.rectTransform.sizeDelta = new Vector2(120, 28); name.fontSizeMin = 10; name.textWrappingMode = TextWrappingModes.NoWrap; _names.Add(name);
            var arrow = CoopUIElements.Text(_canvas, "", Vector2.zero, Vector2.zero, 30);
            arrow.rectTransform.sizeDelta = new Vector2(140, 55); _arrows.Add(arrow);
        }
    }

    void Update()
    {
        var session = LocalCoopSession.Instance;
        if (session == null || _canvas == null) return;
        _connection.gameObject.SetActive(session.DevicesMissing && !session.GameOver);
        if (session.GameOver)
        {
            if (_offer != null) _offer.gameObject.SetActive(false);
            if (Time.unscaledTime < session.ResultsAt) return;
            if (_results == null) ShowResults();
            if (Time.unscaledTime > _selectAfter && Gamepad.all.Count > 0 && ControllerBindings.Pressed(Gamepad.all[0], "Confirm")) ReturnToMenu();
            return;
        }
        if (_offer == null || !_offer.gameObject.activeSelf || session.DevicesMissing) return;
        for (int i = 0; i < session.Players.Count; i++)
        {
            var player = session.Players[i]; var pad = player.Controller;
            if (!player.UsesGamepad || pad == null || !pad.added || _options[i].Count == 0) continue;
            float x = ControllerBindings.MenuMove(pad).x;
            if (Mathf.Abs(x) < .4f) _nextMove[i] = 0;
            else if (!_ready[i] && Time.unscaledTime >= _nextMove[i])
            { _selected[i] = (_selected[i] + (x > 0 ? 1 : -1) + _options[i].Count) % _options[i].Count; _nextMove[i] = Time.unscaledTime + .25f; Highlight(i); }
            if (Time.unscaledTime >= _selectAfter && ControllerBindings.Pressed(pad, "Confirm")) Select(i, _selected[i]);
        }
        CheckReady();
    }

    void LateUpdate()
    {
        for (int i = 0; i < _selectionFrames.Length; i++)
        {
            if (_selectionFrames[i] == null) continue;
            if (_offer != null && _offer.gameObject.activeInHierarchy && i < _buttons.Count && _buttons[i].Count > 0)
                _selectionFrames[i].Show((RectTransform)_buttons[i][_selected[i]].transform, _ready[i] ? Color.green : (Color?)null);
            else _selectionFrames[i].Hide();
        }
        var session = LocalCoopSession.Instance; var camera = Camera.main;
        if (session == null || camera == null || _canvas == null) return;
        for (int i = 0; i < _bars.Count; i++)
        {
            var player = session.Players[i];
            Vector3 labelPosition = player.transform.position + Vector3.up * 1.2f;
            if (Classic && player.Sprite != null) labelPosition.y = player.Sprite.bounds.max.y + .15f;
            Vector3 point = camera.WorldToViewportPoint(labelPosition);
            bool visible = player.Alive && point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1;
            _bars[i].gameObject.SetActive((Classic || visible) && !session.GameOver);
            _arrows[i].gameObject.SetActive(player.Alive && !visible && !session.GameOver);
            if (!Classic) { _bars[i].anchorMin = _bars[i].anchorMax = new Vector2(point.x, point.y); _bars[i].anchoredPosition = Vector2.zero; }
            float health = Mathf.Clamp01((float)player.Health.CurrentHealth / player.Health.MaxHealth);
            if (Classic) _fills[i].fillAmount = health;
            else _fills[i].rectTransform.anchorMax = new Vector2(health, 1);
            _healthLabels[i].text = (Classic ? player.DisplayName + "  " : "") + player.Health.CurrentHealth + "/" + player.Health.MaxHealth;
            _names[i].gameObject.SetActive(visible && !session.GameOver);
            _names[i].rectTransform.anchorMin = _names[i].rectTransform.anchorMax = new Vector2(point.x, point.y);
            _names[i].rectTransform.anchoredPosition = new Vector2(0, Classic ? 0 : 32);
            var edge = new Vector2(Mathf.Clamp(point.x, .055f, .945f), Mathf.Clamp(point.y, .08f, .92f));
            _arrows[i].rectTransform.anchorMin = _arrows[i].rectTransform.anchorMax = edge;
            _arrows[i].rectTransform.anchoredPosition = Vector2.zero;
            _arrows[i].text = point.x < 0 ? "< P" + (i + 1) : point.x > 1 ? "P" + (i + 1) + " >" : point.y > 1 ? "^ P" + (i + 1) : "P" + (i + 1) + " v";
        }
    }

    public void ShowCards(LevelUpUI ui)
    {
        _levelUI = ui;
        if (_canvas == null) Start();
        if (_offer != null) { _offer.gameObject.SetActive(false); Destroy(_offer.gameObject); }
        _offer = CoopUIElements.Panel(_canvas, "Choose upgrades", Vector2.zero, Vector2.one, new Color(.025f, .04f, .07f, .98f));
        _options.Clear(); _buttons.Clear(); _readyLabels.Clear();
        _selectAfter = Time.unscaledTime + .35f; _commitAt = 0;
        var session = LocalCoopSession.Instance;
        for (int i = 0; i < session.Players.Count; i++)
        {
            int seat = i; var player = session.Players[i];
            _selected[i] = 0; _ready[i] = false; _nextMove[i] = 0;
            var panel = PlayerPanel(_offer, i, session.Players.Count);
            CoopUIElements.Text(panel, player.DisplayName + " - CHOOSE AN UPGRADE", new Vector2(.02f, .88f), new Vector2(.98f, .99f), 25);
            var ready = CoopUIElements.Text(panel, ChoicePrompt(player), new Vector2(.02f, .015f), new Vector2(.98f, .1f), 22);
            _readyLabels.Add(ready);
            var options = CardManager.Instance.GetRandomCards(3, player.Stats); _options.Add(options);
            var buttons = new List<UnityEngine.UI.Button>(); _buttons.Add(buttons);
            float panelWidth = 1920f / 2, panelHeight = session.Players.Count > 2 ? 540 : 1080;
            float scale = Mathf.Min((panelWidth - 50) / 3 / 500, panelHeight * .74f / 700);
            for (int c = 0; c < options.Count; c++)
            {
                int choice = c;
                var card = Instantiate(ui.CardPrefab, panel).GetComponent<CardDisplay>();
                card.Setup(options[c]);
                // The solo prefab has layout groups sized for a full-height row.
                // Co-op owns these cloned transforms so each card fits its seat.
                foreach (var layout in card.GetComponentsInChildren<UnityEngine.UI.LayoutGroup>(true)) layout.enabled = false;
                foreach (var aspect in card.GetComponentsInChildren<UnityEngine.UI.AspectRatioFitter>(true)) aspect.enabled = false;
                CoopUIElements.Stretch(card.BackgroundImage.rectTransform, Vector2.zero, Vector2.one);
                CoopUIElements.Stretch((RectTransform)card.NameText.transform.parent, Vector2.zero, Vector2.one);
                ConfigureCardText(card.NameText, new Vector2(.08f, .67f), new Vector2(.92f, .94f), 46);
                ConfigureCardText(card.DescriptionText, new Vector2(.08f, .1f), new Vector2(.92f, .63f), 38);
                var rect = card.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2((c + .5f) / 3, .49f); rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(500, 700); rect.localScale = Vector3.one * scale; rect.anchoredPosition = Vector2.zero;
                var button = card.ClickButton; button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => { if (!player.UsesGamepad) Select(seat, choice); });
                var navigation = button.navigation; navigation.mode = UnityEngine.UI.Navigation.Mode.None; button.navigation = navigation;
                buttons.Add(button);
            }
            if (options.Count == 0) { _ready[i] = true; ready.text = "No eligible cards - ready"; }
            if (_selectionFrames[i] == null) _selectionFrames[i] = new ControllerSelectionFrame("Player " + (i + 1) + " card selection");
            Highlight(i);
        }
        CheckReady();
    }

    void Highlight(int seat)
    {
        if (_selectionFrames[seat] == null) return;
        if (_buttons[seat].Count == 0) _selectionFrames[seat].Hide();
        else _selectionFrames[seat].Show((RectTransform)_buttons[seat][_selected[seat]].transform, _ready[seat] ? Color.green : (Color?)null);
    }
    static void ConfigureCardText(TMP_Text label, Vector2 min, Vector2 max, float size)
    {
        CoopUIElements.Stretch(label.rectTransform, min, max); CoopUIElements.WhiteInfill(label);
        label.margin = Vector4.zero; label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSize = label.fontSizeMax = size; label.fontSizeMin = 22;
    }
    public void Select(int seat, int choice)
    {
        if (_offer == null || !_offer.gameObject.activeSelf || seat < 0 || seat >= _options.Count || choice < 0 || choice >= _options[seat].Count || Time.unscaledTime < _selectAfter || LocalCoopSession.Instance.DevicesMissing) return;
        _ready[seat] = !_ready[seat]; _selected[seat] = choice; _commitAt = 0;
        var player = LocalCoopSession.Instance.Players[seat];
        _readyLabels[seat].text = _ready[seat] ? "READY - " + (player.UsesGamepad ? ControllerBindings.Label("Confirm") : "click") + " to deselect" : ChoicePrompt(player); Highlight(seat);
        AudioManager.Instance?.PlaySFX("Selected_Card"); CheckReady();
    }
    void CheckReady()
    {
        for (int i = 0; i < LocalCoopSession.PlayerCount; i++) if (!_ready[i]) { _commitAt = 0; return; }
        if (_commitAt == 0) { _commitAt = Time.unscaledTime + 1.25f; return; }
        if (Time.unscaledTime < _commitAt) return;
        for (int i = 0; i < LocalCoopSession.PlayerCount; i++) if (_options[i].Count > 0)
            CardManager.Instance.ApplyCardEffect(_options[i][_selected[i]], LocalCoopSession.Instance.Players[i].Stats);
        _offer.gameObject.SetActive(false); _levelUI.CompleteCoopOffer();
    }
    static string ChoicePrompt(LocalPlayer player) => player.UsesGamepad ? "Left / right to browse - " + ControllerBindings.Label("Confirm") + " to select / deselect" : "Click a card to select / deselect";
    public static RectTransform PlayerPanel(Transform parent, int index, int count)
    {
        float bottom = count > 2 ? (index < 2 ? .5f : 0) : 0;
        return CoopUIElements.Panel(parent, "Player " + (index + 1), new Vector2((index % 2) * .5f + .005f, bottom + .005f),
            new Vector2((index % 2 + 1) * .5f - .005f, bottom + (count > 2 ? .5f : 1) - .005f), new Color(.08f, .11f, .16f));
    }
    void ShowResults()
    {
        _selectAfter = Time.unscaledTime + 1;
        _results = CoopUIElements.Panel(_canvas, "Run results", Vector2.zero, Vector2.one, new Color(.025f, .04f, .07f));
        var body = CoopUIElements.Panel(_results, "Players", new Vector2(0, .15f), new Vector2(1, .9f), Color.clear);
        CoopUIElements.Text(_results, "RUN COMPLETE", new Vector2(.1f, .91f), new Vector2(.9f, 1), 42);
        foreach (var player in LocalCoopSession.Instance.Players)
        {
            var panel = PlayerPanel(body, player.Index, LocalCoopSession.PlayerCount);
            CoopUIElements.Text(panel, player.DisplayName + "\n\nDamage dealt: " + player.DamageDealt.ToString("0.#") +
                "\nEnemies killed: " + player.Kills + "\nHealth healed: " + player.Healing + "\nCards picked: " + player.CardsPicked +
                "\nDeaths: " + player.Deaths + "\nCoins collected: " + player.CoinsCollected, new Vector2(.06f, .04f), new Vector2(.94f, .96f), 28);
        }
        CoopUIElements.Button(_results, "Return to main menu (" + ControllerBindings.Label("Confirm") + ")", new Vector2(.64f, .035f), new Vector2(.97f, .115f), ReturnToMenu);
    }
    void ReturnToMenu()
    {
        LevelManager.Instance?.FlushCoinSave(); Time.timeScale = 1; LocalCoopSession.RequestedPlayers = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
    void OnDisable() { foreach (var frame in _selectionFrames) frame?.Hide(); }
    void OnDestroy()
    {
        foreach (var frame in _selectionFrames) frame?.Dispose();
        foreach (var material in _healthMaterials) Destroy(material);
        if (Instance == this) Instance = null;
    }
}

public static class CoopUIElements
{
    static TMP_FontAsset _font;
    public static void SetFont(TMP_FontAsset font) { if (font != null) _font = font; }
    public static void WhiteInfill(TMP_Text text)
    {
        if (_font != null) { text.font = _font; text.fontSharedMaterial = _font.material; }
        text.color = Color.white;
        // Preserve the authored v2 bitmap atlas and its material; never regenerate from the TTF.
    }
    public static RectTransform Canvas(string name, Transform parent, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        go.transform.SetParent(parent, false); var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        return go.GetComponent<RectTransform>();
    }
    public static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); Stretch(rect, min, max); go.GetComponent<UnityEngine.UI.Image>().color = color; return rect;
    }
    public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }
    public static TMP_Text Text(Transform parent, string content, Vector2 min, Vector2 max, float size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>(); Stretch(text.rectTransform, min, max);
        text.text = content; text.color = Color.white; text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMin = size * .7f; text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; WhiteInfill(text); return text;
    }
    public static UnityEngine.UI.Button Button(Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction click)
    {
        var rect = Panel(parent, label, min, max, new Color(.16f, .23f, .32f));
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        Text(rect, label, new Vector2(.025f, .04f), new Vector2(.975f, .96f), 28); button.onClick.AddListener(click); return button;
    }
}
