using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using System.Collections;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance; 

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

    void Awake()
    {
        Instance = this;
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

    public void UpdateWaveText(int wave) => WaveText.text = "WAVE: " + wave;
    public void UpdateEnemiesLeft(int count) => EnemiesLeftText.text = "ENEMIES: " + count;
    public void ShowWaveBanner(int wave) => StartCoroutine(BannerRoutine(wave));

    private IEnumerator BannerRoutine(int wave)
    {
        WaveBannerObject.SetActive(true);
        WaveBannerText.text = "WAVE " + wave;
        yield return new WaitForSeconds(3.0f);
        WaveBannerObject.SetActive(false);
    }
}