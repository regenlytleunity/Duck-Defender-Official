using System.Collections.Generic;
using UnityEngine;

public partial class TutorialDirector
{
    enum PracticeStep { Welcome, Move, Jump, Aim, Shoot, Approach, Combat, Cards, Health, Finished }
    PracticeStep _step;
    Vector3 _origin;
    readonly List<GameObject> _practiceObjects = new List<GameObject>();
    readonly List<EnemyBase> _practiceEnemies = new List<EnemyBase>();
    readonly List<TutorialTarget> _targets = new List<TutorialTarget>();
    readonly bool[] _visited = new bool[5];
    BoxCollider2D _box;
    int _kills;
    float _approachAt;
    bool _jumpLeftGround;
    static Sprite _square;
    bool UsingPad => _player != null && _player.UsesGamepad;
    bool UsingTouch => !UsingPad && MobileInputController.Instance != null && MobileInputController.Instance.IsMobileEnabled;
    string Binding(string action, KeyCode fallback) => UsingPad ? ControllerBindings.Label(action) : InputManager.Instance != null ? InputManager.Instance.GetKeybind(action).ToString() : fallback.ToString();

    void RestartPractice()
    {
        if (!_practice || _step < PracticeStep.Move || _step > PracticeStep.Combat) return;
        BeginPractice(_step == PracticeStep.Combat ? PracticeStep.Approach : _step);
    }
    void ResetPlayer()
    {
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
                    Marker("Movement goal", position + Vector3.up * .08f, new Vector2(1.1f, .12f), new Color(1, .85f, .1f));
                }
                Prompt(MovePrompt(), () => BeginPractice(PracticeStep.Jump), false); break;
            case PracticeStep.Jump:
                _jumpLeftGround = false;
                float height = Mathf.Max(.8f, _player.GetComponent<Collider2D>().bounds.size.y);
                var box = Marker("Practice box", GroundPosition(_origin.x + 2.5f) + Vector3.up * height * .5f, new Vector2(1.8f, height), new Color(.4f, .45f, .55f));
                box.layer = LayerMask.NameToLayer("Ground"); box.tag = "Ground";
                _box = box.AddComponent<BoxCollider2D>();
                Marker("Land here", box.transform.position + Vector3.up * (height * .5f + .04f), new Vector2(1.8f, .08f), new Color(1, .85f, .1f));
                Prompt(JumpPrompt(), () => BeginPractice(PracticeStep.Aim), false); break;
            case PracticeStep.Aim:
            case PracticeStep.Shoot:
                SpawnTargets();
                Prompt(step == PracticeStep.Aim ? AimPrompt() : ShootPrompt(), () => BeginPractice(step == PracticeStep.Aim ? PracticeStep.Shoot : PracticeStep.Approach), false); break;
            case PracticeStep.Approach:
                _kills = 0; _approachAt = Time.time;
                LevelManager.Instance.CurrentXP = 0; LevelManager.Instance.CurrentLevel = 1;
                var prefab = System.Array.Find(WaveManager.Instance.EnemyPrefabs, p => p != null && p.GetComponent<EnemyBase>()?.EnemyKind == "ground");
                if (prefab == null) { Failure("The practice enemy is missing. Return to the menu and retry."); return; }
                for (int i = 0; i < 2; i++)
                {
                    var enemyObject = Instantiate(prefab, GroundPosition(_origin.x + (i == 0 ? -6 : 6)) + Vector3.up, Quaternion.identity);
                    _practiceObjects.Add(enemyObject);
                    var enemy = enemyObject.GetComponent<EnemyBase>(); enemy.Initialize(1); enemy.ConfigureTutorial();
                    _practiceEnemies.Add(enemy);
                    foreach (var collider in enemyObject.GetComponentsInChildren<Collider2D>())
                        Physics2D.IgnoreCollision(collider, _player.GetComponent<Collider2D>());
                }
                Prompt("Here come two practice enemies..."); break;
            case PracticeStep.Health:
                Prompt("Health is a valuable resource. Don't let it reach 0 or you lose!", () => BeginPractice(PracticeStep.Finished)); break;
            case PracticeStep.Finished:
                Prompt("Tutorial stage 1 complete! Continue to return to the home menu.", () => { SaveStage(TutorialStage.Play); ReturnToMenu(); }); break;
        }
    }
    string MovePrompt() => UsingTouch ? "Use the movement stick to touch both highlighted areas." : UsingPad ?
        "Move left and right with the " + (ControllerBindings.RightMoveStick ? "right" : "left") + " thumbstick. Touch both highlighted areas." :
        "Move left and right using " + Binding("MoveLeft", KeyCode.A) + " / " + Binding("MoveRight", KeyCode.D) + ". Touch both highlighted areas.";
    string JumpPrompt() => UsingTouch ? "Flick the movement stick up to jump. Land on top of the highlighted box." : UsingPad && ControllerBindings.Get("Jump") == "MoveUp" ?
        "Quickly move the movement thumbstick up to jump. Land on top of the box." : "Press " + Binding("Jump", KeyCode.Space) + " to jump. Land on top of the box.";
    string AimPrompt() => UsingTouch ? "Drag the aiming stick toward all five targets." : UsingPad ?
        "Use the " + (ControllerBindings.RightMoveStick ? "left" : "right") + " thumbstick to aim at all five targets." : "Use the cursor to aim at all five targets.";
    string ShootPrompt() => UsingTouch ? "Hold the aiming stick to fire feathers. Destroy all five targets." : "Press " + Binding("Shoot", KeyCode.Mouse0) + " to fire feathers. Destroy all five targets.";
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
    void SpawnTargets()
    {
        float radius = Camera.main != null ? Mathf.Clamp(Camera.main.orthographicSize * .55f, 2.5f, 5) : 4;
        Vector3 origin = _player.Weapon.FirePoint != null ? _player.Weapon.FirePoint.position : _origin;
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.PI * i / 4;
            var go = Marker("Practice target", origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, Vector2.one * .7f, new Color(1, .8f, .1f));
            go.GetComponent<SpriteRenderer>().sprite = ItemArea.Circle;
            go.layer = LayerMask.NameToLayer("Enemy");
            var target = go.AddComponent<TutorialTarget>(); target.Index = i; _targets.Add(target);
            if (_step == PracticeStep.Shoot) { var collider = go.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.radius = .5f; }
        }
    }
    void UpdatePractice()
    {
        if (_player.transform.position.y < _origin.y - 8 || Mathf.Abs(_player.transform.position.x - _origin.x) > 18) ResetPlayer();
        if (_step == PracticeStep.Move)
        {
            for (int i = 0; i < 2 && i < _practiceObjects.Count; i++)
                if (Mathf.Abs(_player.transform.position.x - _practiceObjects[i].transform.position.x) < .7f)
                { _visited[i] = true; _practiceObjects[i].GetComponent<SpriteRenderer>().color = Color.green; }
            _ready = _visited[0] && _visited[1]; _message.text = MovePrompt();
        }
        else if (_step == PracticeStep.Jump && _box != null)
        {
            var bounds = _player.GetComponent<Collider2D>().bounds;
            _jumpLeftGround |= !_player.Movement.IsGrounded;
            _ready |= _jumpLeftGround && _player.Movement.IsGrounded && Mathf.Abs(bounds.min.y - _box.bounds.max.y) < .3f &&
                bounds.center.x > _box.bounds.min.x && bounds.center.x < _box.bounds.max.x;
            _message.text = JumpPrompt();
        }
        else if (_step == PracticeStep.Aim)
        {
            Vector3 origin = _player.Weapon.FirePoint != null ? _player.Weapon.FirePoint.position : _player.transform.position;
            for (int i = 0; i < _targets.Count; i++)
                if (Vector2.Dot(_player.Weapon.AimDirection, (_targets[i].transform.position - origin).normalized) > .986f)
                { _visited[i] = true; _targets[i].GetComponent<SpriteRenderer>().color = Color.green; }
            _ready = System.Array.TrueForAll(_visited, v => v); _message.text = AimPrompt();
        }
        else if (_step == PracticeStep.Shoot) { _ready = System.Array.TrueForAll(_visited, v => v); _message.text = ShootPrompt(); }
        else if (_step == PracticeStep.Approach && Time.time - _approachAt > 1.5f)
        {
            // Reposition at verified nearby terrain if the approach got stuck on a ledge.
            for (int i = 0; i < _practiceEnemies.Count; i++) if (_practiceEnemies[i] != null)
            {
                _practiceEnemies[i].transform.position = GroundPosition(_origin.x + (i == 0 ? -4 : 4)) + Vector3.up * _practiceEnemies[i].BodyCollider.bounds.extents.y;
                _practiceEnemies[i].FreezeTutorial();
            }
            _step = PracticeStep.Combat;
            Prompt("Defeat enemies to earn experience. Earn enough experience to level up.");
        }
    }
    public void HitTarget(int index)
    {
        if (_step != PracticeStep.Shoot || index < 0 || index >= _visited.Length || _visited[index]) return;
        _visited[index] = true;
        _targets[index].gameObject.SetActive(false);
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
        ui.ShowTutorialDecor(_step >= PracticeStep.Combat, false);
        if (ui.XPBar != null) ui.XPBar.gameObject.SetActive(_step >= PracticeStep.Combat);
        if (ui.LevelText != null) ui.LevelText.gameObject.SetActive(_step >= PracticeStep.Combat);
        if (ui.PlayerHealthBar != null) ui.PlayerHealthBar.gameObject.SetActive(_step >= PracticeStep.Health);
        if (ui.CoinText != null) ui.CoinText.gameObject.SetActive(false);
        if (ui.WaveText != null) ui.WaveText.gameObject.SetActive(false);
        if (ui.EnemiesLeftText != null) ui.EnemiesLeftText.gameObject.SetActive(false);
        if (_step == PracticeStep.Cards && LevelUpUI.Instance != null)
        {
            LevelUpUI.Instance.CardContainer.GetComponentsInChildren(_visibleCards);
            foreach (var display in _visibleCards)
            {
                bool health = display.AssignedCard != null && display.AssignedCard.ID == "bas_health";
                if (display.ClickButton != null) display.ClickButton.interactable = health;
                if (health) _highlight.Show(display.BackgroundImage.rectTransform);
            }
        }
    }
    void ClearPractice()
    {
        foreach (var go in _practiceObjects) if (go != null) { go.SetActive(false); Destroy(go); }
        _practiceObjects.Clear(); _targets.Clear(); _practiceEnemies.Clear(); _box = null;
    }
}
