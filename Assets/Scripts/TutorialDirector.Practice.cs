using System.Collections.Generic;
using UnityEngine;

public partial class TutorialDirector
{
    enum PracticeStep { Welcome, Move, Jump, Shoot, Approach, Combat, Cards, Health, Finished }
    PracticeStep _step;
    Vector3 _origin;
    readonly List<GameObject> _practiceObjects = new List<GameObject>();
    readonly List<EnemyBase> _practiceEnemies = new List<EnemyBase>();
    readonly List<TutorialTarget> _targets = new List<TutorialTarget>();
    readonly bool[] _visited = new bool[5];
    BoxCollider2D _box;
    SpriteRenderer _landingGoal;
    int _kills;
    Collider2D _playerCollider;
    bool _jumpedForBox;
    readonly ContactPoint2D[] _boxContacts = new ContactPoint2D[8];
    static Sprite _square, _goalGradient, _dartboard;
    static Material _targetParticleMaterial;
    bool UsingPad => _player != null && _player.UsesGamepad;
    bool UsingTouch => !UsingPad && MobileInputController.Instance != null && MobileInputController.Instance.IsMobileEnabled;
    string Binding(string action, KeyCode fallback) => UsingPad ? ControllerBindings.Label(action) : InputManager.Instance != null ? InputManager.Instance.GetKeybind(action).ToString() : fallback.ToString();

    void ResetPlayer()
    {
        _jumpedForBox = false;
        _player.transform.position = _origin;
        var body = _player.GetComponent<Rigidbody2D>(); body.position = _origin; body.linearVelocity = Vector2.zero;
        _player.Movement.ResetAfterRespawn();
    }
    void BeginPractice(PracticeStep step)
    {
        ClearPractice(); System.Array.Clear(_visited, 0, _visited.Length);
        _step = step; _lastPrompt = null; Pause(false);
        _highlight?.Hide();
        if (step >= PracticeStep.Move && step <= PracticeStep.Approach) ResetPlayer();
        switch (step)
        {
            case PracticeStep.Welcome:
                Prompt("Welcome to Duck Defender! Let's run through the basics.", () => BeginPractice(PracticeStep.Move)); break;
            case PracticeStep.Move:
                for (int i = 0; i < 2; i++)
                {
                    Vector3 position = GroundPosition(_origin.x + (i == 0 ? -3 : 3));
                    Goal("Movement goal", position, 1.4f);
                }
                Prompt(MovePrompt(), () => BeginPractice(PracticeStep.Jump), false); break;
            case PracticeStep.Jump:
                _player.Movement.Jumped += OnPracticeJump;
                float height = Mathf.Max(.8f, _player.GetComponent<Collider2D>().bounds.size.y);
                var box = Marker("Practice box", GroundPosition(_origin.x + 2.5f) + Vector3.up * height * .5f, new Vector2(1.8f, height), new Color(.4f, .45f, .55f));
                box.layer = LayerMask.NameToLayer("Ground"); box.tag = "Ground";
                _box = box.AddComponent<BoxCollider2D>();
                _landingGoal = Goal("Land here", box.transform.position + Vector3.up * (height * .5f), 1.8f);
                Prompt(JumpPrompt(), () => BeginPractice(PracticeStep.Shoot), false); break;
            case PracticeStep.Shoot:
                SpawnTargets();
                Prompt(ShootPrompt(), () => BeginPractice(PracticeStep.Approach), false); break;
            case PracticeStep.Approach:
                _kills = 0;
                LevelManager.Instance.CurrentXP = 0; LevelManager.Instance.CurrentLevel = 1;
                var prefab = System.Array.Find(WaveManager.Instance.EnemyPrefabs, p => p != null && p.GetComponent<EnemyBase>()?.EnemyKind == "ground");
                if (prefab == null) { Failure("The practice enemy is missing. Return to the menu and retry."); return; }
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1 : 1;
                    var camera = Camera.main;
                    float edge = camera != null ? camera.transform.position.x + side * camera.orthographicSize * camera.aspect : _origin.x + side * 12;
                    var enemyObject = Instantiate(prefab, GroundPosition(edge + side * 3) + Vector3.up, Quaternion.identity);
                    _practiceObjects.Add(enemyObject);
                    var enemy = enemyObject.GetComponent<EnemyBase>(); enemy.Initialize(1);
                    float feetOffset = enemy.transform.position.y - enemy.BodyCollider.bounds.min.y;
                    enemy.transform.position = GroundPosition(edge + side * (enemy.BodyCollider.bounds.extents.x + 2)) + Vector3.up * feetOffset;
                    enemy.ConfigureTutorial(GroundPosition(_origin.x + side * 4) + Vector3.up * feetOffset);
                    _practiceEnemies.Add(enemy);
                    foreach (var collider in enemyObject.GetComponentsInChildren<Collider2D>())
                        Physics2D.IgnoreCollision(collider, _player.GetComponent<Collider2D>());
                }
                Prompt("Here come two practice enemies..."); break;
            case PracticeStep.Health:
                Prompt("Health is a valuable resource. Don't let it reach 0 or you lose!", () => BeginPractice(PracticeStep.Finished)); break;
            case PracticeStep.Finished:
                Prompt("Practice complete! Collect your 200-coin bonus and head to the shop.", FinishPractice, !Coin.AnyActive); break;
        }
    }
    string MovePrompt() => UsingTouch ? "Use the movement stick to touch both highlighted areas." : UsingPad ?
        "Move left and right with the " + (ControllerBindings.RightMoveStick ? "right" : "left") + " thumbstick. Touch both highlighted areas." :
        "Move left and right using " + Binding("MoveLeft", KeyCode.A) + " / " + Binding("MoveRight", KeyCode.D) + ". Touch both highlighted areas.";
    string JumpPrompt() => UsingTouch ? "Flick the movement stick up to jump. Land on top of the highlighted box." : UsingPad && ControllerBindings.Get("Jump") == "MoveUp" ?
        "Quickly move the movement thumbstick up to jump. Land on top of the box." : "Press " + Binding("Jump", KeyCode.Space) + " to jump. Land on top of the box.";
    string ShootPrompt()
    {
        if (UsingTouch) return "Point the aiming stick and hold it to fire. Destroy all 5 targets.";
        string aim = UsingPad ? "Use the " + (ControllerBindings.RightMoveStick ? "left" : "right") + " thumbstick to aim" : "Aim with the cursor";
        return aim + " and press " + Binding("Shoot", KeyCode.Mouse0) + " to fire. Destroy all 5 targets.";
    }
    Vector3 GroundPosition(float x)
    {
        var hit = Physics2D.Raycast(new Vector2(x, _origin.y + 4), Vector2.down, 30, _player.Movement.GroundLayer);
        return hit.collider != null ? (Vector3)hit.point : new Vector3(x, _origin.y - _player.GetComponent<Collider2D>().bounds.extents.y);
    }
    GameObject Marker(string name, Vector3 position, Vector2 size, Color color)
    {
        if (_square == null) _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
        var go = new GameObject(name); go.transform.position = position; go.transform.localScale = size;
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = _square; renderer.color = color; renderer.sortingOrder = 100;
        _practiceObjects.Add(go); return go;
    }
    public static Sprite GuidanceGradient
    {
        get
        {
            if (_goalGradient == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Tutorial goal gradient", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float edge = Mathf.Clamp01(Mathf.Min(x, size - 1 - x) / 3f);
                    pixels[y * size + x] = new Color(1, 1, 1, edge * Mathf.Pow(1 - y / (float)(size - 1), 1.6f));
                }
                texture.SetPixels(pixels); texture.Apply(false, true);
                _goalGradient = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            }
            return _goalGradient;
        }
    }
    SpriteRenderer Goal(string name, Vector3 ground, float width)
    {
        var goal = Marker(name, ground + Vector3.up * .75f, new Vector2(width, 1.5f), new Color(.25f, 1, .12f, .9f));
        var renderer = goal.GetComponent<SpriteRenderer>();
        renderer.sprite = GuidanceGradient;
        return renderer;
    }
    static Sprite Dartboard()
    {
        if (_dartboard != null) return _dartboard;
        const int size = 192;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Tutorial dartboard", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        Color dark = new Color(.055f, .065f, .08f), light = new Color(.94f, .92f, .83f), red = new Color(.9f, .1f, .14f), green = new Color(.08f, .55f, .3f);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            Vector2 point = new Vector2(x + .5f, y + .5f) / size * 2 - Vector2.one;
            float radius = point.magnitude;
            bool alternate = (Mathf.FloorToInt((Mathf.Atan2(point.y, point.x) + Mathf.PI) / (Mathf.PI / 10)) & 1) == 0;
            Color color = radius < .10f ? red : radius < .18f ? green : radius > .9f ? dark :
                radius > .80f || radius > .48f && radius < .57f ? (alternate ? red : green) : alternate ? light : dark;
            color.a = Mathf.Clamp01((1 - radius) * size * .5f);
            pixels[y * size + x] = color;
        }
        texture.SetPixels(pixels); texture.Apply(false, true);
        _dartboard = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        return _dartboard;
    }
    void SpawnTargets()
    {
        float radius = Camera.main != null ? Mathf.Clamp(Camera.main.orthographicSize, 4, 8.5f) : 6.5f;
        Vector3 origin = _player.Weapon.FirePoint != null ? _player.Weapon.FirePoint.position : _origin;
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.PI * i / 4;
            var go = Marker("Practice target", origin + Vector3.up * 1.1f + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, Vector2.one * .9f, Color.white);
            go.GetComponent<SpriteRenderer>().sprite = Dartboard();
            go.layer = LayerMask.NameToLayer("Enemy");
            var target = go.AddComponent<TutorialTarget>(); target.Index = i; _targets.Add(target);
            var collider = go.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.radius = .5f;
        }
    }
    void OnPracticeJump() => _jumpedForBox = true;

    void UpdatePractice()
    {
        if (_player.transform.position.y < _origin.y - 8 || Mathf.Abs(_player.transform.position.x - _origin.x) > 18) ResetPlayer();
        if (_step == PracticeStep.Move)
        {
            var body = _playerCollider.bounds;
            for (int i = 0; i < 2; i++)
            {
                var goal = _practiceObjects[i].GetComponent<SpriteRenderer>();
                var pad = goal.bounds;
                _visited[i] |= body.max.x >= pad.min.x && body.min.x <= pad.max.x &&
                    body.max.y >= pad.min.y && body.min.y <= pad.max.y;
                if (_visited[i]) FadeGoal(goal);
            }
            _ready = _visited[0] && _visited[1]; SetPromptText(MovePrompt());
        }
        else if (_step == PracticeStep.Jump && _box != null)
        {
            if (_jumpedForBox && !_ready)
            {
                int count = _playerCollider.GetContacts(_boxContacts);
                for (int i = 0; i < count; i++)
                {
                    var hit = _boxContacts[i];
                    if (hit.collider == _box && hit.normal.y > .5f) { _ready = true; break; }
                }
            }
            if (_ready) FadeGoal(_landingGoal);
            SetPromptText(JumpPrompt());
        }
        else if (_step == PracticeStep.Shoot) { _ready = System.Array.TrueForAll(_visited, v => v); SetPromptText(ShootPrompt()); }
        else if (_step == PracticeStep.Finished) _ready = !Coin.AnyActive;
        else if (_step == PracticeStep.Approach)
        {
            foreach (var enemy in _practiceEnemies)
            {
                if (enemy == null) { Failure("A practice enemy is missing. Returning to the menu to retry..."); return; }
                if (!enemy.TutorialStopped) return;
            }
            _step = PracticeStep.Combat;
            Prompt("Defeat enemies to earn experience and level up. They also drop coins. Watch the coins fly to you!");
        }
    }
    static void FadeGoal(SpriteRenderer goal)
    {
        if (goal == null) return;
        var color = goal.color;
        color.a = Mathf.MoveTowards(color.a, .25f, Time.unscaledDeltaTime * 2.5f);
        goal.color = color;
    }
    public void HitTarget(int index)
    {
        if (_step != PracticeStep.Shoot || index < 0 || index >= _visited.Length || _visited[index]) return;
        _visited[index] = true;
        TargetBurst(_targets[index].transform.position);
        _targets[index].gameObject.SetActive(false);
    }
    void TargetBurst(Vector3 position)
    {
        if (!ParticleVisibility.Enabled) return;
        var go = new GameObject("Target burst"); go.transform.position = position;
        _practiceObjects.Add(go);
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false; main.playOnAwake = false; main.duration = .7f;
        main.startLifetime = .55f; main.startSpeed = 0; main.gravityModifier = .3f;
        main.maxParticles = 28; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.enabled = false;
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
        if (_targetParticleMaterial == null) _targetParticleMaterial = new Material(Shader.Find("Sprites/Default"));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _targetParticleMaterial; renderer.sortingOrder = 110;
        go.AddComponent<ParticleVisibility>();
        particles.Play();
        Color[] colors = { new Color(.9f, .1f, .14f), new Color(.08f, .55f, .3f), new Color(.94f, .92f, .83f), new Color(.055f, .065f, .08f) };
        for (int i = 0; i < 28; i++)
        {
            float angle = i * Mathf.PI * 2 / 28;
            particles.Emit(new ParticleSystem.EmitParams { position = position,
                velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(1.5f, 3.5f),
                startColor = colors[i % colors.Length], startSize = Random.Range(.06f, .13f), startLifetime = Random.Range(.35f, .65f) }, 1);
        }
    }
    public void PracticeEnemyDefeated()
    {
        if (!_practice || _step != PracticeStep.Combat || _kills >= 2) return;
        _kills++;
        var level = LevelManager.Instance;
        if (_kills == 2)
        {
            _step = PracticeStep.Cards;
            Prompt("Congratulations! Leveling up lets you pick 1 of 3 powerful upgrades. Choose the highlighted Health Upgrade.");
        }
        // Exact half-bar awards, independent of XP multipliers and odd target values.
        level.AddTutorialXP(_kills == 1 ? level.TargetXP / 2 : level.TargetXP - level.CurrentXP);
    }
    public static List<CardDefinition> PracticeCards()
    {
        if (!Practice || Instance._step != PracticeStep.Cards || CardManager.Instance == null) return null;
        var result = new List<CardDefinition>();
        foreach (string id in new[] { "bas_health", "bas_damage", "bas_mobility" })
        {
            var card = CardManager.Instance.AllCards.Find(c => c != null && c.ID == id);
            if (card == null) { Instance.Failure("A tutorial upgrade is missing. Return to the menu and retry."); return result; }
            result.Add(card);
        }
        return result;
    }
    public static bool CanSelectCard(CardDefinition card) => !Practice || Instance._step == PracticeStep.Cards && card != null && card.ID == "bas_health";
    public static void SelectedCard() { if (Practice) Instance.BeginPractice(PracticeStep.Health); }
    void UpdatePracticeHUD()
    {
        var ui = GameUI.Instance; if (ui == null) return;
        ui.ShowTutorialDecor(_step >= PracticeStep.Combat, _step >= PracticeStep.Combat);
        if (ui.XPBar != null) ui.XPBar.gameObject.SetActive(_step >= PracticeStep.Combat);
        if (ui.LevelText != null) ui.LevelText.gameObject.SetActive(_step >= PracticeStep.Combat);
        if (ui.PlayerHealthBar != null) ui.PlayerHealthBar.gameObject.SetActive(_step >= PracticeStep.Health);
        if (ui.CoinText != null) ui.CoinText.gameObject.SetActive(_step >= PracticeStep.Combat);
        if (ui.WaveText != null) ui.WaveText.gameObject.SetActive(false);
        if (ui.EnemiesLeftText != null) ui.EnemiesLeftText.gameObject.SetActive(false);
        if (_step == PracticeStep.Cards && LevelUpUI.Instance != null)
        {
            LevelUpUI.Instance.CardContainer.GetComponentsInChildren(_visibleCards);
            foreach (var display in _visibleCards)
            {
                bool health = display.AssignedCard != null && display.AssignedCard.ID == "bas_health";
                // All cards remain focusable/hoverable. LevelUpUI guards activation.
                if (health) _highlight.Show(display.BackgroundImage.rectTransform);
            }
        }
    }
    void ClearPractice()
    {
        if (_player != null && _player.Movement != null) _player.Movement.Jumped -= OnPracticeJump;
        foreach (var go in _practiceObjects) if (go != null) { go.SetActive(false); Destroy(go); }
        _practiceObjects.Clear(); _targets.Clear(); _practiceEnemies.Clear(); _box = null; _landingGoal = null;
    }
}
