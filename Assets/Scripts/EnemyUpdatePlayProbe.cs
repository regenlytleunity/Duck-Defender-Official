#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Opt-in Play Mode probe. The session flag and temporary save are set explicitly by verification.
public class EnemyUpdatePlayProbe : MonoBehaviour
{
    public const string SessionKey = "DuckDefender.EnemyUpdateVerificationSave";
    public static string Result = "Not started";
    readonly List<GameObject> _created = new List<GameObject>();
    int _checks;
    PlayerHealth _health;
    PlayerController _controller;
    Rigidbody2D _playerBody;
    WaveManager _wave;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void IsolateSave()
    {
        string path = UnityEditor.SessionState.GetString(SessionKey, "");
        if (!string.IsNullOrEmpty(path)) SaveSystem.VerificationSavePath = path;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartProbe()
    {
        if (string.IsNullOrEmpty(UnityEditor.SessionState.GetString(SessionKey, ""))) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene") return;
        new GameObject("Enemy update play verification").AddComponent<EnemyUpdatePlayProbe>();
    }
    IEnumerator Start()
    {
        yield return null;
        Result = "Running";
        var work = Verify();
        while (true)
        {
            object current;
            try { if (!work.MoveNext()) break; current = work.Current; }
            catch (Exception e) { Result = "FAILED: " + e; Debug.LogError(Result); yield break; }
            yield return current;
        }
        Result = _checks + " Play Mode checks passed.";
        Debug.Log(Result);
    }
    void Check(bool value, string label)
    {
        _checks++;
        if (!value) throw new InvalidOperationException(label);
    }
    EnemyBase Spawn(string path, Vector3 position, bool elite = false, int wave = 1)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = Instantiate(prefab, position, Quaternion.identity);
        _created.Add(go);
        var enemy = go.GetComponent<EnemyBase>();
        enemy.IsElite = elite; enemy.Initialize(wave);
        return enemy;
    }
    void Clear()
    {
        foreach (var go in _created) if (go != null) Destroy(go);
        _created.Clear();
    }
    void ResetPlayer(Vector3 position)
    {
        _playerBody.position = position;
        _playerBody.linearVelocity = Vector2.zero;
        _health.Heal(100);
        _health.StopAllCoroutines();
        typeof(PlayerHealth).GetField("_isInvulnerable", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_health, false);
    }

    IEnumerator Verify()
    {
        _wave = WaveManager.Instance;
        Application.runInBackground = true;
        _wave.StopAllCoroutines(); _wave.enabled = false;
        EnemyTipUI.Instance.enabled = false;
        _controller = PlayerController.Instance;
        _health = _controller.GetComponent<PlayerHealth>();
        _playerBody = _controller.GetComponent<Rigidbody2D>();
        _controller.enabled = false;
        _controller.GetComponent<WeaponPlayer>().enabled = false;
        var weapon = _controller.GetComponent<WeaponPlayer>();
        var fireInput = typeof(WeaponPlayer).GetMethod("ProcessFireInput", BindingFlags.Instance | BindingFlags.NonPublic);
        var deadline = typeof(WeaponPlayer).GetField("_nextNormalFireTime", BindingFlags.Instance | BindingFlags.NonPublic);
        float due = Time.time + 10;
        deadline.SetValue(weapon, due);
        for (int i = 0; i < 10; i++)
        {
            fireInput.Invoke(weapon, new object[] { false });
            fireInput.Invoke(weapon, new object[] { true });
        }
        Check(Mathf.Approximately((float)deadline.GetValue(weapon), due), "spam clicks cannot shorten the next-shot deadline");
        _health.MaxHealth = 100; _health.ThornsDamage = 0;
        _playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        ResetPlayer(new Vector3(0, -1.5f, 0));
        yield return new WaitForFixedUpdate();

        var ground = Spawn("Assets/Enemies/Fast Enemy.prefab", new Vector3(-4, -1.5f, 0));
        yield return new WaitForSeconds(2.5f);
        Check(_health.CurrentHealth < 100, "ground enemy must damage a stationary player");
        Check(ground.GetComponentInChildren<Animator>() != null, "existing animation component preserved");
        Clear(); yield return null;

        ResetPlayer(new Vector3(0, -1.5f, 0));
        ground = Spawn("Assets/Enemies/Fast Enemy.prefab", new Vector3(-1.2f, -1.5f, 0));
        ((SwarmerEnemy)ground).WindupSeconds = .8f;
        yield return new WaitForSeconds(.2f);
        _playerBody.position = new Vector2(6, -1.5f);
        yield return new WaitForSeconds(.7f);
        Check(_health.CurrentHealth == 100, "moving away during windup must dodge");
        Clear(); yield return null;

        ResetPlayer(new Vector3(0, -1.5f, 0));
        ground = Spawn("Assets/Enemies/Fast Enemy.prefab", new Vector3(-1.2f, -1.5f, 0), true);
        float timeout = Time.time + 3;
        while (_health.CurrentHealth == 100 && Time.time < timeout) yield return null;
        Check(_health.CurrentHealth == 98 && _health.IsStunned, "elite strike deals two and stuns");
        yield return new WaitForSeconds(.55f);
        Check(!_health.IsStunned, "stun expires after half a second");
        Clear(); yield return null;

        ResetPlayer(new Vector3(0, -1.5f, 0));
        _health.ApplySlow(2); _health.ApplySlow(2);
        Check(Mathf.Approximately(_health.MovementMultiplier, .5f), "slow refreshes without multiplying");
        yield return new WaitForSeconds(2.1f);
        Check(_health.MovementMultiplier == 1, "slow expires");
        for (int i = 0; i < 7; i++) _health.ApplyLobberPoison();
        Check(_health.PoisonStacks == 5, "poison caps at five stacks");
        yield return new WaitForSeconds(5.1f);
        Check(_health.CurrentHealth == 95 && _health.PoisonStacks == 0, "five poison stacks deal exactly five over five seconds");

        var protectedSpawn = Spawn("Assets/Enemies/Fast Enemy.prefab", new Vector3(60, -1.5f, 0));
        float before = protectedSpawn.HealthRemaining;
        protectedSpawn.TakeDamage(100);
        Check(protectedSpawn.IsSpawnProtected && protectedSpawn.HealthRemaining == before, "offscreen spawn immune");
        protectedSpawn.transform.position = new Vector3(0, 1, 0);
        yield return null;
        Check(!protectedSpawn.IsSpawnProtected, "entering camera ends spawn immunity");
        Clear(); yield return null;

        var ball = ObjectPooler.Instance.GetBouncyEnemyProjectile();
        var projectile = ball.GetComponent<BouncyEnemyProjectile>();
        Vector3 baseScale = ball.transform.localScale;
        ball.transform.position = new Vector3(4, 3, 0);
        projectile.Launch(new Vector2(2, 1), true); ball.SetActive(true);
        Check(projectile.BouncesRemaining == 2, "elite ball starts with two bounces");
        ball.transform.localScale *= 1.5625f;
        ball.SetActive(false);
        projectile.Launch(Vector2.zero, false); ball.SetActive(true);
        Check(ball.transform.localScale == baseScale && !projectile.IsCloud, "pooled projectile size and cloud state reset");
        typeof(BouncyEnemyProjectile).GetMethod("BecomeCloud", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(projectile, new object[] { new Vector2(4, -2.3f) });
        Check(projectile.IsCloud, "poison impact enters cloud state");
        yield return new WaitForSeconds(1.1f);
        Check(!ball.activeSelf, "poison cloud expires after one second");

        var shot = ObjectPooler.Instance.GetEnemyBullet();
        var shotScript = shot.GetComponent<EnemyProjectile>();
        Vector3 shotScale = shot.transform.localScale;
        shot.transform.position = new Vector3(0, 20, 0);
        shotScript.Configure(true); shot.SetActive(true);
        Check(shot.transform.localScale == shotScale * 1.25f, "first elite shot scale");
        shot.SetActive(false);
        shotScript.Configure(false); shot.SetActive(true);
        Check(shot.transform.localScale == shotScale, "first-use elite projectile does not contaminate pooled normal size");
        shot.SetActive(false);

        ResetPlayer(new Vector3(0, -1.5f, 0));
        var flyer = Spawn("Assets/Enemies/Flying Enemy.prefab", new Vector3(4, 3, 0), true);
        flyer.TakeDamage(1000);
        Check(!flyer.IsAlive && flyer != null && !EnemyBase.ActiveEnemies.Contains(flyer), "crash is untargetable but remains visible");
        yield return new WaitForSeconds(1);
        Check(flyer != null, "elite flyer death is delayed");
        yield return new WaitForSeconds(2.2f);
        Check(flyer == null, "elite crash finishes after three seconds");

        // Leave a representative visual scene for screenshot inspection, using only the temporary save.
        ResetPlayer(new Vector3(0, -1.5f, 0));
        var tank = (TankEnemy)Spawn("Assets/Enemies/Tank Enemy.prefab", new Vector3(3, -1.5f, 0), true, 20);
        var ally = Spawn("Assets/Enemies/Fast Enemy.prefab", new Vector3(5, -1.5f, 0), false, 20);
        tank.AcquireChains(); ally.TakeDamage(4);
        Check(tank.ShieldHealth == 2, "live chain redirects to visible shield");
        EnemyTipUI.Instance.enabled = true;
        EnemyTipUI.Instance.ShowTip("elite_tank", EnemyTipUI.TitleFor("elite_tank"), EnemyTipUI.DescriptionFor("elite_tank"), tank.GetComponent<SpriteRenderer>().sprite);
        Check(EnemyTipUI.Instance.IsShowing && Time.timeScale == 0, "tutorial pauses");
        yield return null;
    }
}
#endif
