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
    readonly List<List<CardDefinition>> _options = new List<List<CardDefinition>>();
    readonly List<List<UnityEngine.UI.Button>> _buttons = new List<List<UnityEngine.UI.Button>>();
    readonly List<TMP_Text> _readyLabels = new List<TMP_Text>();
    readonly int[] _selected = new int[4];
    readonly bool[] _ready = new bool[4];
    readonly float[] _nextMove = new float[4];
    LevelUpUI _levelUI;
    float _selectAfter;

    void Awake() { Instance = this; }
    void Start()
    {
        _canvas = CoopUIElements.Canvas("Co-op HUD", transform, 900);
        _connection = CoopUIElements.Text(_canvas, "Controller disconnected. Reconnect it to continue.", new Vector2(.15f, .85f), new Vector2(.85f, .94f), 32);
        _connection.gameObject.SetActive(false);
        foreach (var player in LocalCoopSession.Instance.Players)
        {
            var bar = CoopUIElements.Panel(_canvas, "Player health", Vector2.zero, Vector2.zero, new Color(.08f, .1f, .15f, .9f));
            bar.sizeDelta = new Vector2(140, 28); bar.pivot = new Vector2(.5f, .5f);
            var fill = CoopUIElements.Panel(bar, "Health", Vector2.zero, Vector2.one,
                LocalCoopSession.ColoredHealthBars ? player.Color : Color.green).GetComponent<UnityEngine.UI.Image>();
            fill.raycastTarget = false;
            _bars.Add(bar); _fills.Add(fill);
            var labelBackground = CoopUIElements.Panel(bar, "Health label", new Vector2(0, .22f), Vector2.one, new Color(.04f, .06f, .09f, .88f));
            labelBackground.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            _healthLabels.Add(CoopUIElements.Text(labelBackground, "", Vector2.zero, Vector2.one, 18));
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
            if (_results == null) ShowResults();
            if (Time.unscaledTime > _selectAfter && Gamepad.all.Count > 0 && Gamepad.all[0].buttonEast.wasPressedThisFrame) ReturnToMenu();
            return;
        }
        if (_offer == null || !_offer.gameObject.activeSelf || session.DevicesMissing) return;
        for (int i = 0; i < session.Players.Count; i++)
        {
            var player = session.Players[i]; var pad = player.Controller;
            if (_ready[i] || !player.UsesGamepad || pad == null || !pad.added || _options[i].Count == 0) continue;
            float x = Mathf.Abs(pad.dpad.x.ReadValue()) > .1f ? pad.dpad.x.ReadValue() : pad.leftStick.x.ReadValue();
            if (Mathf.Abs(x) < .4f) _nextMove[i] = 0;
            else if (Time.unscaledTime >= _nextMove[i])
            { _selected[i] = (_selected[i] + (x > 0 ? 1 : -1) + _options[i].Count) % _options[i].Count; _nextMove[i] = Time.unscaledTime + .25f; Highlight(i); }
            if (Time.unscaledTime >= _selectAfter && pad.buttonEast.wasPressedThisFrame) Select(i, _selected[i]);
        }
    }

    void LateUpdate()
    {
        var session = LocalCoopSession.Instance; var camera = Camera.main;
        if (session == null || camera == null || _canvas == null) return;
        for (int i = 0; i < _bars.Count; i++)
        {
            var player = session.Players[i]; Vector3 point = camera.WorldToViewportPoint(player.transform.position + Vector3.up * 1.2f);
            bool visible = player.Alive && point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1;
            _bars[i].gameObject.SetActive(visible && !session.GameOver);
            _arrows[i].gameObject.SetActive(player.Alive && !visible && !session.GameOver);
            _bars[i].anchorMin = _bars[i].anchorMax = new Vector2(point.x, point.y);
            _bars[i].anchoredPosition = Vector2.zero;
            _fills[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)player.Health.CurrentHealth / player.Health.MaxHealth), 1);
            _healthLabels[i].text = "P" + (i + 1) + "  " + player.Health.CurrentHealth + "/" + player.Health.MaxHealth;
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
        _selectAfter = Time.unscaledTime + .35f;
        var session = LocalCoopSession.Instance;
        for (int i = 0; i < session.Players.Count; i++)
        {
            int seat = i; var player = session.Players[i];
            _selected[i] = 0; _ready[i] = false; _nextMove[i] = 0;
            var panel = PlayerPanel(_offer, i, session.Players.Count);
            CoopUIElements.Text(panel, "PLAYER " + (i + 1) + " - CHOOSE AN UPGRADE", new Vector2(.02f, .88f), new Vector2(.98f, .99f), 25);
            var ready = CoopUIElements.Text(panel, player.UsesGamepad ? "Left / right to choose - B to select" : "Click a card to select", new Vector2(.02f, .015f), new Vector2(.98f, .1f), 22);
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
                var outline = card.BackgroundImage.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = player.Color; outline.effectDistance = new Vector2(7, -7);
            }
            if (options.Count == 0) { _ready[i] = true; ready.text = "No eligible cards - ready"; }
            Highlight(i);
        }
        CheckReady();
    }

    void Highlight(int seat)
    {
        for (int i = 0; i < _buttons[seat].Count; i++) _buttons[seat][i].GetComponentInChildren<UnityEngine.UI.Outline>().enabled = i == _selected[seat] && !_ready[seat];
    }
    static void ConfigureCardText(TMP_Text label, Vector2 min, Vector2 max, float size)
    {
        CoopUIElements.Stretch(label.rectTransform, min, max); CoopUIElements.WhiteInfill(label);
        label.margin = Vector4.zero; label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSize = label.fontSizeMax = size; label.fontSizeMin = 22;
    }
    public void Select(int seat, int choice)
    {
        if (_offer == null || !_offer.gameObject.activeSelf || _ready[seat] || choice < 0 || choice >= _options[seat].Count || Time.unscaledTime < _selectAfter) return;
        _ready[seat] = true;
        CardManager.Instance.ApplyCardEffect(_options[seat][choice], LocalCoopSession.Instance.Players[seat].Stats);
        foreach (var button in _buttons[seat]) button.interactable = false;
        _readyLabels[seat].text = "READY - waiting for teammates"; Highlight(seat);
        AudioManager.Instance?.PlaySFX("Selected_Card"); CheckReady();
    }
    void CheckReady()
    {
        for (int i = 0; i < LocalCoopSession.PlayerCount; i++) if (!_ready[i]) return;
        _offer.gameObject.SetActive(false); _levelUI.CompleteCoopOffer();
    }
    static RectTransform PlayerPanel(Transform parent, int index, int count)
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
            CoopUIElements.Text(panel, "PLAYER " + (player.Index + 1) + "\n\nDamage dealt: " + player.DamageDealt.ToString("0.#") +
                "\nEnemies killed: " + player.Kills + "\nHealth healed: " + player.Healing + "\nCards picked: " + player.CardsPicked +
                "\nDeaths: " + player.Deaths + "\nCoins collected: " + player.CoinsCollected, new Vector2(.06f, .04f), new Vector2(.94f, .96f), 28);
        }
        CoopUIElements.Button(_results, "Return to main menu (B)", new Vector2(.64f, .035f), new Vector2(.97f, .115f), ReturnToMenu);
    }
    void ReturnToMenu()
    {
        LevelManager.Instance?.FlushCoinSave(); Time.timeScale = 1; LocalCoopSession.RequestedPlayers = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
}

public static class CoopUIElements
{
    public static void WhiteInfill(TMP_Text text)
    {
        // The custom bitmap atlas bakes black glyph interiors into its RGB pixels.
        // New labels use the existing default SDF font so face color is controllable.
        if (text.fontSharedMaterial.shader.name.Contains("Bitmap"))
        { text.font = TMP_Settings.defaultFontAsset; text.fontSharedMaterial = text.font.material; }
        text.color = Color.white;
        // Use a local material instance: some authored font presets have a black face.
        text.fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        text.outlineColor = Color.black; text.outlineWidth = .2f;
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
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
    }
    public static UnityEngine.UI.Button Button(Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction click)
    {
        var rect = Panel(parent, label, min, max, new Color(.16f, .23f, .32f));
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        Text(rect, label, new Vector2(.025f, .04f), new Vector2(.975f, .96f), 28); button.onClick.AddListener(click); return button;
    }
}
