using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using System.Collections;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance; 
    public TMP_FontAsset UIFont;

    [Header("HUD")]
    public TextMeshProUGUI WaveText;
    public TextMeshProUGUI EnemiesLeftText;
    
    [Header("Player Health Bar")]
    [Tooltip("Sprite-based health bar (green overlay on red background). Use this instead of the old Slider.")]
    public SpriteHealthBar PlayerHealthBar;
    
    [Header("XP HUD")]
    [Tooltip("Sprite-based XP bar (fill overlay on background). Use this instead of the old Slider.")]
    public SpriteXPBar XPBar;
    public TextMeshProUGUI LevelText;

    [Header("Economy HUD")]
    public TextMeshProUGUI CoinText; 

    [Header("Banners")]
    public GameObject WaveBannerObject;
    public TextMeshProUGUI WaveBannerText;

    [Header("Game Over")]
    public GameObject GameOverScreen; 

    [Header("Visual FX")]
    public GameObject DamagePopupPrefab; 
    public Transform PopupCanvas;
    TMP_Text _soloHealthText;
    int _healthCurrent, _healthMax;
    Transform _heartDecor, _xpDecor, _coinDecor;
    Material _xpMaterial;

    void Awake()
    {
        Instance = this;
        CoopUIElements.SetFont(UIFont);
        _heartDecor = PlayerHealthBar != null ? PlayerHealthBar.transform.parent.Find("HeartDecor") : null;
        _xpDecor = XPBar != null ? XPBar.transform.parent.Find("ExpDecor") : null;
        _coinDecor = CoinText != null ? CoinText.transform.parent.Find("CoinDecor") : null;
        // Hide the authored HUD before the first rendered practice frame, including
        // its sibling decorations rather than only the bar/text GameObjects.
        if (TutorialDirector.CoreActive && TutorialDirector.Progress.Stage == TutorialStage.Basics)
        {
            ShowTutorialDecor(false, false);
            if (PlayerHealthBar != null) PlayerHealthBar.gameObject.SetActive(false);
            if (XPBar != null) XPBar.gameObject.SetActive(false);
            if (LevelText != null) LevelText.gameObject.SetActive(false);
            if (CoinText != null) CoinText.gameObject.SetActive(false);
            if (WaveText != null) WaveText.gameObject.SetActive(false);
            if (EnemiesLeftText != null) EnemiesLeftText.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        StyleExperienceBar();
        if (LocalCoopSession.Multiplayer || PlayerHealthBar == null) return;
        var bar = (RectTransform)PlayerHealthBar.transform;
        if (_heartDecor != null) _heartDecor.gameObject.SetActive(false);
        CoopUIElements.Stretch(bar, new Vector2(.02f, .875f), new Vector2(.23f, .915f));
        if (PlayerHealthBar.Background != null) { CoopUIElements.Stretch(PlayerHealthBar.Background.rectTransform, Vector2.zero, Vector2.one); PlayerHealthBar.Background.preserveAspect = false; }
        if (PlayerHealthBar.Overlay != null)
        {
            CoopUIElements.Stretch(PlayerHealthBar.Overlay.rectTransform, Vector2.zero, Vector2.one);
            PlayerHealthBar.Overlay.preserveAspect = false;
            PlayerHealthBar.Overlay.rectTransform.offsetMin = new Vector2(5, 5);
            PlayerHealthBar.Overlay.rectTransform.offsetMax = new Vector2(-5, -5);
        }
        var label = CoopUIElements.Panel(bar, "Health label", new Vector2(0, 1.05f), new Vector2(1, 1.85f), new Color(.04f, .06f, .09f, .88f));
        label.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        _soloHealthText = CoopUIElements.Text(label, _healthCurrent + " / " + _healthMax, Vector2.zero, Vector2.one, 22);
        if (XPBar != null) CoopUIElements.Stretch((RectTransform)XPBar.transform, new Vector2(.02f, .795f), new Vector2(.23f, .835f));
        if (LevelText != null)
        {
            CoopUIElements.Stretch(LevelText.rectTransform, new Vector2(.02f, .837f), new Vector2(.23f, .869f));
            LevelText.margin = Vector4.zero; LevelText.alignment = TextAlignmentOptions.Center;
            LevelText.enableAutoSizing = true; LevelText.fontSize = LevelText.fontSizeMax = 22; LevelText.fontSizeMin = 18;
            CoopUIElements.WhiteInfill(LevelText);
        }
    }

    void StyleExperienceBar()
    {
        if (_xpDecor != null) _xpDecor.gameObject.SetActive(false);
        if (XPBar == null || PlayerHealthBar == null) return;
        if (XPBar.Background != null && PlayerHealthBar.Background != null)
        {
            XPBar.Background.sprite = PlayerHealthBar.Background.sprite;
            XPBar.Background.color = new Color(.28f, .3f, .38f);
            XPBar.Background.preserveAspect = false;
            CoopUIElements.Stretch(XPBar.Background.rectTransform, Vector2.zero, Vector2.one);
        }
        if (XPBar.Overlay != null && PlayerHealthBar.Overlay != null)
        {
            XPBar.Overlay.sprite = PlayerHealthBar.Overlay.sprite;
            XPBar.Overlay.color = Color.white; XPBar.Overlay.preserveAspect = false;
            XPBar.Overlay.type = Image.Type.Filled; XPBar.Overlay.fillMethod = Image.FillMethod.Horizontal; XPBar.Overlay.fillOrigin = 0;
            CoopUIElements.Stretch(XPBar.Overlay.rectTransform, Vector2.zero, Vector2.one);
            XPBar.Overlay.rectTransform.offsetMin = new Vector2(5, 5); XPBar.Overlay.rectTransform.offsetMax = new Vector2(-5, -5);
            var shader = Resources.Load<Shader>("HealthPalette");
            if (shader != null)
            {
                _xpMaterial = new Material(shader); _xpMaterial.SetColor("_PlayerColor", new Color(.2f, .65f, 1));
                XPBar.Overlay.material = _xpMaterial;
            }
        }
    }

    public void ShowTutorialDecor(bool xp, bool coins)
    {
        // The classic numeric health bar replaces the standalone heart decoration.
        if (_heartDecor != null) _heartDecor.gameObject.SetActive(false);
        if (_xpDecor != null) _xpDecor.gameObject.SetActive(false);
        if (_coinDecor != null) _coinDecor.gameObject.SetActive(coins);
    }

    public void ShowDamagePopup(Vector3 worldPos, int amount, bool isCrit)
    {
        if (DamagePopupPrefab != null && PopupCanvas != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            GameObject popup = Instantiate(DamagePopupPrefab, PopupCanvas);
            popup.transform.position = screenPos;

            DamagePopup dp = popup.GetComponent<DamagePopup>();
            if (dp != null) dp.Setup(amount, isCrit);
        }
    }

    // --- HUD Updates ---
    public void UpdateCoinText(int coins)
    {
        if (CoinText != null) CoinText.text = coins.ToString("N0") + " G"; 
    }

    public void UpdatePlayerHealth(int current, int max)
    {
        _healthCurrent = current; _healthMax = max;
        if (_soloHealthText != null) _soloHealthText.text = current + " / " + max;
        if (PlayerHealthBar != null)
        {
            PlayerHealthBar.UpdateHealth(current, max);
        }
    }

    public void UpdateXPBar(int current, int max, int level)
    {
        if (XPBar != null)
        {
            XPBar.UpdateXP(current, max);
        }
        if (LevelText != null) LevelText.text = "LVL " + level;
    }

    public void ShowGameOver()
    {
        if (GameOverScreen != null) GameOverScreen.SetActive(true);
    }

    public void UpdateWaveText(int wave) => WaveText.text = "WAVE: " + wave + (TutorialDirector.ProtectedRun && !TutorialDirector.Practice ? "/5" : "");
    public void UpdateEnemiesLeft(int count) => EnemiesLeftText.text = "ENEMIES: " + count;
    public void ShowWaveBanner(int wave) => StartCoroutine(BannerRoutine(wave));

    void OnDestroy() { if (_xpMaterial != null) Destroy(_xpMaterial); }

    private IEnumerator BannerRoutine(int wave)
    {
        WaveBannerObject.SetActive(true);
        WaveBannerText.text = "WAVE " + wave;
        yield return new WaitForSeconds(3.0f);
        WaveBannerObject.SetActive(false);
    }
}
