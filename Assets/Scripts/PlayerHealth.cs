using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 1.4.11 ADDS:
/// - Second Wind: if HP reaches 0 and Second Wind is ready, restore % MaxHealth and grant invuln.
///   After triggering, enters a run-specific cooldown - if you die again before it's ready, you die.
/// - Respects PlayerController.IsInvulnerable (for Blink invuln)
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Stats")]
    public int MaxHealth = 5;
    [Tooltip("Base invulnerability window after taking damage.")]
    public float InvulnerabilityTime = 1.0f;
    public string MainMenuSceneName = "MainMenu";

    [Header("Survival Upgrades")]
    public int RegenPerWave = 0;
    public int ThornsDamage = 0;

    private int _currentHealth;
    private bool _isInvulnerable = false;
    private bool _isDead = false;
    private SpriteRenderer _spriteRen;
    private PlayerAnimator _animator;
    private PlayerController _controller;

    public int CurrentHealth => _currentHealth;

    void Start()
    {
        _currentHealth = MaxHealth;
        _spriteRen = GetComponent<SpriteRenderer>();
        _animator = GetComponent<PlayerAnimator>();
        _controller = GetComponent<PlayerController>();
        UpdateUI();
    }

    public void Heal(int amount)
    {
        if (_isDead) return;
        _currentHealth += amount;
        if (_currentHealth > MaxHealth) _currentHealth = MaxHealth;
        UpdateUI();
    }

    public void ApplyWaveRegen()
    {
        if (RegenPerWave > 0) Heal(RegenPerWave);
    }

    public void TakeDamage(int damage)
    {
        if (_isInvulnerable || _isDead || _currentHealth <= 0) return;

        // 1.4.11: Respect Blink invulnerability via PlayerController
        if (_controller != null && _controller.IsInvulnerable) return;

        _currentHealth -= damage;
        UpdateUI();

        // 1.4.13: Play hurt SFX. Placed AFTER the invuln/death/blink early-outs so 
        // it only plays when damage actually lands. If the player dies from this hit, 
        // the death sound takes over in Die() — but we still want the hurt sound to 
        // play for the impact itself (then Die() plays Game_Over over it, which is 
        // the desired feel: ouch! → game over).
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Player_Hurt");
        }

        if (_currentHealth <= 0)
        {
            // === 1.4.11 SECOND WIND CHECK ===
            // Before dying, try to trigger Second Wind.
            if (TryTriggerSecondWind())
            {
                // Saved! Don't die.
                return;
            }

            Die();
        }
        else
        {
            StartCoroutine(InvulnerabilityRoutine(InvulnerabilityTime));
        }
    }

    /// <summary>
    /// 1.4.11: If Second Wind is unlocked and off cooldown, restore HP and grant invuln 
    /// instead of dying. Marks Second Wind as used so it can't fire again until cooldown.
    /// </summary>
    bool TryTriggerSecondWind()
    {
        if (PlayerStats.Instance == null) return false;
        if (!PlayerStats.Instance.IsSecondWindReady()) return false;

        // Restore % of max health
        float pct = Mathf.Clamp01(PlayerStats.Instance.SecondWindHealthRecovery);
        int recoveredHP = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * pct));
        _currentHealth = recoveredHP;
        UpdateUI();

        // Consume the Second Wind charge (starts the cooldown)
        PlayerStats.Instance.ConsumeSecondWind();

        // Invulnerability window
        float invulnDur = Mathf.Max(0.5f, PlayerStats.Instance.SecondWindInvulnDuration);
        StartCoroutine(InvulnerabilityRoutine(invulnDur));

        // SFX / visual feedback
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Level_Up"); // angel-y vibe
        }

        if (GameUI.Instance != null)
        {
            GameUI.Instance.ShowDamagePopup(transform.position + Vector3.up * 1.5f, recoveredHP, true);
        }

        Debug.Log("[Second Wind] Triggered - player saved!");
        return true;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (ThornsDamage > 0 && collision.gameObject.CompareTag("Enemy"))
        {
            EnemyBase enemy = collision.gameObject.GetComponent<EnemyBase>();
            if (enemy != null) enemy.TakeDamage(ThornsDamage);
        }
    }

    void UpdateUI()
    {
        if (GameUI.Instance != null)
        {
            GameUI.Instance.UpdatePlayerHealth(_currentHealth, MaxHealth);
        }
    }

    void Die()
    {
        if (_isDead) return;
        _isDead = true;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Game_Over");
            AudioManager.Instance.StopMusic();
        }

        if (_animator != null) _animator.TriggerDeath();

        if (GetComponent<PlayerController>())
            GetComponent<PlayerController>().enabled = false;
        if (GetComponent<WeaponPlayer>())
            GetComponent<WeaponPlayer>().enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        if (GameUI.Instance != null) GameUI.Instance.ShowGameOver();

        StartCoroutine(ReturnToMenuRoutine());
    }

    IEnumerator ReturnToMenuRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        SceneManager.LoadScene(MainMenuSceneName);
    }

    IEnumerator InvulnerabilityRoutine(float duration)
    {
        _isInvulnerable = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_spriteRen != null) _spriteRen.enabled = false;
            yield return new WaitForSeconds(0.1f);
            if (_spriteRen != null) _spriteRen.enabled = true;
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.2f;
        }
        _isInvulnerable = false;
    }
}