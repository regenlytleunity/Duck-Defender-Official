using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-300)]
public class LocalCoopSession : MonoBehaviour
{
    public static LocalCoopSession Instance { get; private set; }
    public static int RequestedPlayers = 1;
    public static bool KeyboardTest, Respawning = true;
    public enum HealthBarStyle { Classic, AbovePlayer }
    public static HealthBarStyle HealthStyle = HealthBarStyle.AbovePlayer;
    public static readonly string[] PlayerNames = { "PLAYER 1", "PLAYER 2", "PLAYER 3", "PLAYER 4" };
    public static readonly int[] PlayerColors = { 2, 1, 4, 3 };
    public static readonly Gamepad[] PlayerControllers = new Gamepad[4];
    public static readonly HashSet<string> DisabledCards = new HashSet<string>();
    public readonly List<LocalPlayer> Players = new List<LocalPlayer>(4);
    public static int PlayerCount => Instance != null ? Mathf.Max(1, Instance.Players.Count) : Mathf.Clamp(RequestedPlayers, 1, 4);
    public static bool Multiplayer => PlayerCount > 1;
    public bool GameOver { get; private set; }
    public bool DevicesMissing { get; private set; }
    public float ResultsAt { get; private set; }
    bool _devicePause;

    void Awake() { Instance = this; }
    void Start()
    {
        var primary = FindFirstObjectByType<PlayerStats>();
        if (primary == null) return;
        var turretTemplate = FindFirstObjectByType<TurretManager>();
        int count = Mathf.Clamp(RequestedPlayers, 1, 4);
        PlayerStats.Instance = primary; PlayerController.Instance = primary.GetComponent<PlayerController>();
        var bodies = new List<GameObject> { primary.gameObject };
        for (int i = 1; i < count; i++)
        {
            var body = Instantiate(primary.gameObject, primary.transform.position + Vector3.right * i * 1.2f, primary.transform.rotation);
            body.name = "Player " + (i + 1); bodies.Add(body);
        }
        for (int i = 0; i < count; i++)
        {
            bool pad = count == 1 ? Gamepad.all.Count > 0 : !(KeyboardTest && count == 2 && i == 0);
            int deviceIndex = KeyboardTest && count == 2 ? i - 1 : i;
            var player = bodies[i].GetComponent<LocalPlayer>() ?? bodies[i].AddComponent<LocalPlayer>();
            var controller = count > 1 && PlayerControllers[i] != null ? PlayerControllers[i] : pad && deviceIndex >= 0 && deviceIndex < Gamepad.all.Count ? Gamepad.all[deviceIndex] : null;
            player.Configure(i, controller, pad);
            Players.Add(player);
            if (turretTemplate != null)
            {
                var manager = i == 0 ? turretTemplate : Instantiate(turretTemplate, transform);
                manager.PlayerTransform = player.transform; manager.AutoFindPlayer = false;
            }
        }
        if (count > 1)
        {
            gameObject.AddComponent<CoopRunUI>();
            if (GameUI.Instance != null && GameUI.Instance.PlayerHealthBar != null) GameUI.Instance.PlayerHealthBar.gameObject.SetActive(false);
        }
        gameObject.AddComponent<ControllerMenuNavigation>();
        // Local teammates do not push one another or count as ground.
        for (int i = 0; i < Players.Count; i++) for (int j = i + 1; j < Players.Count; j++)
            foreach (var a in Players[i].GetComponentsInChildren<Collider2D>())
                foreach (var b in Players[j].GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(a, b);
    }

    void Update()
    {
        if (GameOver) return;
        if (!Multiplayer)
        {
            if (Players.Count == 0) return;
            var current = Players[0].Controller;
            var controller = current != null && current.added ? current : Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
            if (current != controller || Players[0].UsesGamepad != (controller != null)) Players[0].AssignController(controller, controller != null);
            return;
        }
        DevicesMissing = false;
        foreach (var player in Players) if (!player.Connected) DevicesMissing = true;
        if (DevicesMissing) { _devicePause = true; Time.timeScale = 0; }
        else if (_devicePause)
        {
            _devicePause = false;
            if ((LevelUpUI.Instance == null || !LevelUpUI.Instance.IsOffering) && (EnemyTipUI.Instance == null || !EnemyTipUI.Instance.IsShowing)) Time.timeScale = 1;
        }
    }

    public void PlayerDied(LocalPlayer player)
    {
        player.Deaths++;
        foreach (var other in Players) if (other.Alive) return;
        GameOver = true; Time.timeScale = 0; ResultsAt = Time.unscaledTime + 2;
        LevelManager.Instance?.FlushCoinSave();
        AudioManager.Instance?.PlaySFX("Game_Over"); AudioManager.Instance?.StopMusic();
    }

    public void BeginWave()
    {
        if (!Multiplayer || !Respawning || GameOver) return;
        LocalPlayer survivor = null;
        foreach (var player in Players) if (player.Alive) { survivor = player; break; }
        if (survivor == null) return;
        foreach (var player in Players) if (!player.Alive) player.Health.Respawn(survivor.transform.position);
    }

    public static PlayerStats NearestAlive(Vector2 position)
    {
        if (Instance == null || Instance.Players.Count == 0)
        {
            var primary = PlayerStats.Instance;
            return primary != null && primary.GetComponent<PlayerHealth>()?.IsDead != true ? primary : null;
        }
        PlayerStats nearest = null; float distance = float.PositiveInfinity;
        foreach (var player in Instance.Players)
        {
            if (player == null || !player.Alive) continue;
            float d = ((Vector2)player.transform.position - position).sqrMagnitude;
            if (d < distance) { distance = d; nearest = player.Stats; }
        }
        return nearest;
    }

    public static float TeamXPBonus()
    {
        if (Instance == null || Instance.Players.Count == 0) return PlayerStats.Instance != null ? PlayerStats.Instance.XPMultiplier : 1;
        float multiplier = 1;
        foreach (var player in Instance.Players) multiplier += Mathf.Max(0, player.Stats.XPMultiplier - 1);
        return multiplier;
    }
    public static float TeamSabotage()
    {
        if (Instance == null || Instance.Players.Count == 0) return PlayerStats.Instance != null ? PlayerStats.Instance.EnemyHealthMissingPercent : 0;
        float missing = 0;
        foreach (var player in Instance.Players) missing += player.Stats.EnemyHealthMissingPercent;
        return Mathf.Clamp(missing, 0, .95f);
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
