using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Added by WaveManager. Optional authored content can override the built-in enemy explanations.
public class EnemyTipUI : MonoBehaviour
{
    [Serializable]
    public class TipContent
    {
        public string ID, Title;
        [TextArea(2, 6)] public string Description;
        public Sprite Illustration;
    }
    public static EnemyTipUI Instance { get; private set; }
    public List<TipContent> Tips = new List<TipContent>();
    public float InputGraceSeconds = 1;
    public bool IsShowing => _currentID != null;
    public GameObject Panel;
    public TextMeshProUGUI TitleText, DescriptionText, ContinueText;
    public UnityEngine.UI.Image Illustration;
    HashSet<string> _seen, _shownThisRun = new HashSet<string>();
    bool _repeat, _released;
    string _currentID;
    float _shownAt, _resumeAfter, _previousScale = 1;

    void Awake()
    {
        Instance = this;
        var data = SaveSystem.LoadData();
        _seen = new HashSet<string>(data.SeenTipIDs);
        _repeat = data.ShowTips;
    }

    public void Observe(EnemyBase enemy)
    {
        if (!isActiveAndEnabled || enemy == null || !enemy.IsAlive || !enemy.IsInView(true)) return;
        string id = enemy.TipID;
        if (_shownThisRun.Contains(id) || (!_repeat && _seen.Contains(id))) return;
        if (Time.timeScale <= 0 || IsShowing || Time.unscaledTime < _resumeAfter) return;
        var custom = Tips.Find(t => t.ID == id);
        var sprite = enemy.GetComponent<SpriteRenderer>();
        ShowTip(id, custom != null ? custom.Title : TitleFor(id),
            custom != null ? custom.Description : DescriptionFor(id),
            custom != null && custom.Illustration != null ? custom.Illustration : sprite != null ? sprite.sprite : null);
    }

    // Stable IDs allow future non-enemy tips to share persistence and the same dismissal rules.
    public void ShowTip(string id, string title, string description, Sprite illustration = null)
    {
        if (string.IsNullOrEmpty(id) || IsShowing || Time.timeScale <= 0 ||
            _shownThisRun.Contains(id) || (!_repeat && _seen.Contains(id))) return;
        EnsurePanel();
        _currentID = id;
        _shownThisRun.Add(id);
        TitleText.text = title;
        DescriptionText.text = description;
        Illustration.sprite = illustration;
        Illustration.gameObject.SetActive(illustration != null);
        Panel.SetActive(true);
        _shownAt = Time.unscaledTime;
        _released = false;
        _previousScale = Time.timeScale;
        Time.timeScale = 0;
    }

    void Update()
    {
        if (!IsShowing) return;
        bool graceOver = Time.unscaledTime - _shownAt >= InputGraceSeconds;
        bool held = Input.GetMouseButton(0) || Input.touchCount > 0;
        if (graceOver && !held) _released = true;
        ContinueText.text = graceOver && _released ? "Click or tap to continue" : "Take a moment to read...";
        bool pressed = Input.GetMouseButtonDown(0);
        for (int i = 0; i < Input.touchCount; i++) pressed |= Input.GetTouch(i).phase == TouchPhase.Began;
        if (graceOver && _released && pressed) Dismiss();
    }

    public void Dismiss()
    {
        if (!IsShowing) return;
        var data = SaveSystem.LoadData();
        if (!data.SeenTipIDs.Contains(_currentID)) data.SeenTipIDs.Add(_currentID);
        SaveSystem.SaveData(data, true);
        _seen.Add(_currentID);
        _currentID = null;
        Panel.SetActive(false);
        _resumeAfter = Time.unscaledTime + .3f;
        Time.timeScale = LevelUpUI.Instance != null && LevelUpUI.Instance.IsOffering ? 0 : _previousScale;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsShowing && (LevelUpUI.Instance == null || !LevelUpUI.Instance.IsOffering)) Time.timeScale = _previousScale;
    }

    void EnsurePanel()
    {
        if (Panel != null && TitleText != null && DescriptionText != null && ContinueText != null && Illustration != null) return;
        var canvasObject = new GameObject("Enemy Information", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        // Fullscreen transparent backdrop blocks clicks on underlying buttons while a tip owns the pause.
        Panel = new GameObject("Tip Overlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        Panel.transform.SetParent(canvasObject.transform, false);
        var overlay = Panel.GetComponent<RectTransform>();
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        Panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .12f);
        var box = new GameObject("Tip", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        box.transform.SetParent(Panel.transform, false);
        var rect = box.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.12f, 1); rect.anchorMax = new Vector2(.88f, 1);
        rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -36); rect.sizeDelta = new Vector2(0, 280);
        box.GetComponent<UnityEngine.UI.Image>().color = new Color(.06f, .09f, .13f, .98f);
        var art = new GameObject("Enemy", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        art.transform.SetParent(box.transform, false);
        Illustration = art.GetComponent<UnityEngine.UI.Image>(); Illustration.preserveAspect = true; Illustration.raycastTarget = false;
        var artRect = art.GetComponent<RectTransform>();
        artRect.anchorMin = artRect.anchorMax = new Vector2(0, .5f); artRect.anchoredPosition = new Vector2(110, 0); artRect.sizeDelta = new Vector2(140, 140);
        TitleText = Text(box.transform, "Title", 34, new Vector2(210, -22), new Vector2(-28, -70));
        DescriptionText = Text(box.transform, "Description", 27, new Vector2(210, -78), new Vector2(-28, -215));
        ContinueText = Text(box.transform, "Continue", 22, new Vector2(210, -229), new Vector2(-28, -269));
        ContinueText.color = new Color(.7f, .85f, 1);
        Panel.SetActive(false);
    }

    static TextMeshProUGUI Text(Transform parent, string name, float size, Vector2 topLeft, Vector2 bottomRight)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
        rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);
        text.fontSize = size; text.color = Color.white; text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = size * .75f; text.fontSizeMax = size;
        return text;
    }

    public static string TitleFor(string id)
    {
        switch (id)
        {
            case "ground": return "Ground Enemy";
            case "flying": return "Flying Enemy";
            case "tank": return "Tank";
            case "lobber": return "Lobber";
            case "elite_ground": return "Elite Ground Enemy";
            case "elite_flying": return "Elite Flying Enemy";
            case "elite_tank": return "Elite Tank";
            case "elite_lobber": return "Elite Lobber";
            default: return id;
        }
    }
    public static string DescriptionFor(string id)
    {
        switch (id)
        {
            case "ground": return "Closes in, winds up, then strikes for 1 damage. Move away during its windup to dodge. It rests for one second between attacks.";
            case "flying": return "Hovers nearby and winds up a shot aimed at your position. Keep moving to dodge its 1-damage projectile.";
            case "tank": return "Has four times a ground enemy's health. Its chains redirect half of up to four allies' damage into the tank. Defeat the tank to break its protection.";
            case "lobber": return "Lobs poison in a high arc. Hits may poison you for five seconds, stacking up to five times. Misses leave a short-lived cloud that disappears on contact.";
            case "elite_ground": return "Larger reach, 50% more health, and a slower approach. Its strike deals 2 damage and stuns movement and feather attacks for half a second. Turrets, auras, and thorns still work.";
            case "elite_flying": return "Larger projectiles slow you by 50% for two seconds; slows refresh without stacking. After defeat it drifts downward for three seconds, then explodes for 1 damage before dropping coins.";
            case "elite_tank": return "Redirected ally damage becomes its blue shield. Each defeated linked ally grants 10% movement speed and 0.25 attack damage, up to four times.";
            case "elite_lobber": return "Has 25% more health. Fires poison, then a second ball that bounces twice, growing 25% larger and faster with each bounce. Elites award double coins and 25% more XP.";
            default: return "";
        }
    }
}
