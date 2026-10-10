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
    PlayerStats _ownerStats;
    PlayerStats OwnerStats => _ownerStats != null ? _ownerStats : (_ownerStats = GetComponent<PlayerStats>());

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
    private int _flatMaxHealth;
    private float _maxHealthBonus;
    private float _rebirthHealthMultiplier = 1f;
    public bool IsDead => _isDead;

    public int CurrentHealth => _currentHealth;
    public bool IsStunned => Time.time < _stunnedUntil;
    public float MovementMultiplier => Time.time < _slowedUntil ? .5f : 1f;
    public int PoisonStacks => Time.time < _poisonUntil ? _poisonStacks : 0;
    float _stunnedUntil, _slowedUntil, _poisonUntil, _nextPoisonTick;
    int _poisonStacks;
    float _fractionalIncomingDamage;

    public void ApplyStun(float seconds)
    {
        if (_isDead) return;
        _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + seconds);
    }

    public void ApplySlow(float seconds)
    {
        if (!_isDead) _slowedUntil = Mathf.Max(_slowedUntil, Time.time + seconds);
    }

    public void ApplyLobberPoison()
    {
        if (_isDead) return;
        if (Time.time >= _poisonUntil) { _poisonStacks = 0; _nextPoisonTick = Time.time + 1; }
        _poisonStacks = Mathf.Min(5, _poisonStacks + 1);
        _poisonUntil = Time.time + 5;
    }

    void Update()
    {
        if (_isDead || Time.timeScale == 0 || _poisonStacks == 0) return;
        // Poison has its own clock: ordinary hit invulnerability must not erase its ticks.
        while (_nextPoisonTick <= Time.time && _nextPoisonTick <= _poisonUntil + .001f)
        {
            TryTakeDamage(_poisonStacks * .2f, true);
            _nextPoisonTick += 1;
            if (_isDead) break;
        }
        if (Time.time >= _poisonUntil) _poisonStacks = 0;
    }

    void Start()
    {
        _currentHealth = MaxHealth;
        _flatMaxHealth = MaxHealth;
        _spriteRen = GetComponent<SpriteRenderer>();
        _animator = GetComponent<PlayerAnimator>();
        _controller = GetComponent<PlayerController>();
        UpdateUI();
    }

    public void Heal(int amount)
    {
        if (_isDead) return;
        int previous = _currentHealth;
        _currentHealth += Mathf.Max(0, amount);
        if (_currentHealth >= MaxHealth) { _currentHealth = MaxHealth; _fractionalIncomingDamage = 0; }
        var player = GetComponent<LocalPlayer>();
        if (player != null) player.Healing += Mathf.Max(0, _currentHealth - previous);
        UpdateUI();
    }

    public void ApplyWaveRegen()
    {
        if (RegenPerWave > 0) Heal(Mathf.Max(0, RegenPerWave));
    }

    public void AddMaxHealth(int flat)
    {
        if (_flatMaxHealth == 0) _flatMaxHealth = MaxHealth;
        _flatMaxHealth += flat;
        RecalculateMaxHealth();
    }

    public void AddMaxHealthPercent(float bonus)
    {
        if (_flatMaxHealth == 0) _flatMaxHealth = MaxHealth;
        _maxHealthBonus += bonus;
        RecalculateMaxHealth();
    }

    void RecalculateMaxHealth()
    {
        int previous = MaxHealth;
        int upgradedHealth = Mathf.Max(1, Mathf.RoundToInt(_flatMaxHealth * (1f + _maxHealthBonus)));
        MaxHealth = Mathf.Max(1, Mathf.RoundToInt(upgradedHealth * _rebirthHealthMultiplier));
        Heal(Mathf.Max(0, MaxHealth - previous));
    }

    void ApplyRebirthHealthBoost()
    {
        if (_flatMaxHealth == 0) _flatMaxHealth = MaxHealth;
        _rebirthHealthMultiplier = 1f + PlayerStats.RebirthBonus;
        RecalculateMaxHealth();
    }

    public void TakeDamage(int damage)
    {
        TryTakeDamage(damage);
    }

    public bool TryTakeDamage(float damage, bool poisonTick = false)
    {
        if (damage <= 0 || Time.timeScale == 0) return false;
        if ((!poisonTick && _isInvulnerable) || _isDead || _currentHealth <= 0) return false;

        // 1.4.11: Respect Blink invulnerability via PlayerController
        if (_controller != null && _controller.IsInvulnerable) return false;

        // Incoming damage is enemy contact, ammunition, or lobber poison.
        _fractionalIncomingDamage += damage * GameDifficulty.DamageMultiplier;
        int wholeDamage = Mathf.FloorToInt(_fractionalIncomingDamage + .00001f);
        _fractionalIncomingDamage -= wholeDamage;
        _currentHealth -= wholeDamage;
        if (OwnerStats != null && OwnerStats.HasAscension(CardAscension.Pincushion))
            GetComponent<AscensionEffects>()?.ReleaseNeedles();
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
                _poisonStacks = 0;
                _fractionalIncomingDamage = 0;
                return true;
            }

            Die();
        }
        else if (!poisonTick)
        {
            StartCoroutine(InvulnerabilityRoutine(InvulnerabilityTime));
        }
        return true;
    }

    /// <summary>
    /// 1.4.11: If Second Wind is unlocked and off cooldown, restore HP and grant invuln 
    /// instead of dying. Marks Second Wind as used so it can't fire again until cooldown.
    /// </summary>
    bool TryTriggerSecondWind()
    {
        if (OwnerStats == null) return false;
        var stats = OwnerStats;
        if (stats.HasAscension(CardAscension.Rebirth) && !stats.RebirthUsed)
        {
            stats.RebirthUsed = true;
            stats.RebirthStatBonus += PlayerStats.RebirthBonus;
            stats.NotifySecondWindChanged();
            ApplyRebirthHealthBoost();
            _currentHealth = MaxHealth;
            GetComponent<AscensionEffects>()?.Rebirth();
            StartCoroutine(InvulnerabilityRoutine(5));
            UpdateUI();
            return true;
        }
        if (!OwnerStats.IsSecondWindReady()) return false;

        // Restore % of max health
        float pct = Mathf.Clamp01(OwnerStats.SecondWindHealthRecovery);
        int recoveredHP = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * pct));
        _currentHealth = recoveredHP;
        UpdateUI();

        // Consume the Second Wind charge (starts the cooldown)
        OwnerStats.ConsumeSecondWind();

        // Invulnerability window
        float invulnDur = Mathf.Max(0.5f, OwnerStats.SecondWindInvulnDuration);
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
            if (enemy != null) enemy.TakeDamage(Mathf.RoundToInt(OwnerStats != null ? OwnerStats.CalculateDamage(ThornsDamage, false) : ThornsDamage), OwnerStats);
        }
    }

    void UpdateUI()
    {
        if (!LocalCoopSession.Multiplayer && GameUI.Instance != null)
        {
            GameUI.Instance.UpdatePlayerHealth(_currentHealth, MaxHealth);
        }
    }

    void Die()
    {
        if (_isDead) return;
        _isDead = true;
        if (LocalCoopSession.Multiplayer && LocalCoopSession.Instance != null)
        {
            StopAllCoroutines();
            if (_spriteRen != null) { _spriteRen.enabled = true; _spriteRen.color = Color.white; }
            _animator?.TriggerDeath();
            var movement = GetComponent<PlayerController>(); movement.StopAllCoroutines(); movement.enabled = false;
            if (movement.AuraChild != null) movement.AuraChild.UpdateAura(0, 0);
            var weapon = GetComponent<WeaponPlayer>(); weapon.StopAllCoroutines(); weapon.enabled = false;
            GetComponent<Rigidbody2D>().simulated = false;
            LocalCoopSession.Instance.PlayerDied(GetComponent<LocalPlayer>());
            return;
        }

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

    public void Respawn(Vector3 position)
    {
        if (!_isDead) return;
        StopAllCoroutines();
        _isDead = false; _isInvulnerable = false; _currentHealth = MaxHealth;
        _stunnedUntil = _slowedUntil = _poisonUntil = 0; _poisonStacks = 0; _fractionalIncomingDamage = 0;
        transform.position = position;
        var body = GetComponent<Rigidbody2D>(); body.simulated = true; body.linearVelocity = Vector2.zero;
        GetComponent<PlayerController>().ResetAfterRespawn(); GetComponent<PlayerController>().enabled = true;
        GetComponent<WeaponPlayer>().enabled = true;
        _animator?.Revive(); if (_spriteRen != null) { _spriteRen.enabled = true; _spriteRen.color = Color.white; }
        StartCoroutine(InvulnerabilityRoutine(2)); UpdateUI();
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
