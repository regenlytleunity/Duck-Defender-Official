if (!EditorApplication.isPlaying || string.IsNullOrEmpty(SaveSystem.VerificationSavePath)) throw new Exception("An isolated Play Mode session is required.");
var players = LocalCoopSession.Instance.Players;
if (players.Count != 2) throw new Exception("Need two verification seats.");
var first = players[0]; var second = players[1];
var results = new List<string>();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); results.Add(message); }
float Range(LocalPlayer player) => Vector2.Distance(player.transform.position, player.EffectCursorPosition);
void SetStick(LocalPlayer player, Vector2 stick, bool swapped = false) {
    var state = new UnityEngine.InputSystem.LowLevel.GamepadState();
    if (swapped) state.leftStick = stick; else state.rightStick = stick;
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(player.Controller, state);
}
System.Collections.IEnumerator Tests() {
    WaveManager.Instance.StopAllCoroutines();
    EnemyTipUI.Instance.Dismiss(); Time.timeScale = 1;
    foreach (var enemy in EnemyBase.ActiveEnemies.ToArray()) { enemy.gameObject.SetActive(false); UnityEngine.Object.Destroy(enemy.gameObject); }
    var source = WaveManager.Instance.EnemyPrefabs.First(p => p.GetComponent<EnemyBase>().EnemyKind == "ground");
    var ground = UnityEngine.Object.Instantiate(source, first.transform.position + Vector3.right * 3, Quaternion.identity).GetComponent<EnemyBase>();
    ground.SpawnProtectionSeconds = 0; ground.Initialize(2); ground.CoinPrefab = null; ground.XPValue = 0;
    Check(ground.MaxHealth == 2 && ground.HealthRemaining == 2, "Wave two ground enemy spawns with two HP.");
    ground.TakeDamage(1,null); Check(ground.HealthRemaining == 1, "One feather's base damage leaves one HP.");
    ground.TakeDamage(1,null); Check(!ground.IsAlive,"Second point of damage defeats the enemy.");
    var ids = new HashSet<Rect>();
    for (int i = 1; i <= ItemCatalog.Count; i++) {
        var sprite = ItemCatalog.Icon(i);
        Check(sprite != null && sprite.name == ItemCatalog.Names[i] && ids.Add(sprite.rect) &&
          sprite.rect.size == Vector2.one * 32 && sprite.texture.filterMode == FilterMode.Point &&
          sprite.bounds.size.x == 1 && ItemCatalog.Icon(i) == sprite, "Unique cached 32-pixel icon for " + ItemCatalog.Names[i]);
    }
    var ui = GameUI.Instance; var xp = (RectTransform)ui.XPBar.transform; var coins = ui.CoinText.rectTransform;
    var decor = (RectTransform)coins.parent.Find("CoinDecor");
    Check(coins.anchorMax.y < xp.anchorMin.y && decor.anchorMax.y < xp.anchorMin.y && coins.anchorMin.y > .864f, "Co-op coin row fits between XP and the first classic health label.");
    first.Stats.ActivateAscension(CardAscension.CursorAura);
    second.Stats.ActivateAscension(CardAscension.CursorAura);
    for(int i = 0; i < 4; i++) first.Stats.Items.AddSawblade();
    SetStick(first, Vector2.right*.4f); SetStick(second,Vector2.up);
    yield return new WaitForSeconds(1.6f);
    Check(Range(first) < .02f && Mathf.Abs(Range(second)-10)<.01f,"Light stick reaches the duck and the second seat retains its own distance.");
    SetStick(first,Vector2.right*.72f); yield return new WaitForSeconds(.45f); SetStick(first,Vector2.zero);
    yield return new WaitForSeconds(.15f); float middle = Range(first);
    Check(middle > 2 && middle < 7,"Firm partial tilt moves the effects outward.");
    SetStick(first,Vector2.up); yield return new WaitForSeconds(.3f); SetStick(first,Vector2.zero); yield return new WaitForSeconds(.2f);
    Check(Mathf.Abs(Range(first)-middle)<.05f && first.Aim.y>.99f,"Full tilt changes direction and neutral holds that reach.");
    var saws = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(s=>s.name=="Item sawblade").ToArray();
    Vector3 midpoint = Vector3.zero; foreach (var saw in saws) midpoint+=saw.transform.position/saws.Length;
    Check(saws.Length == 4 && Vector2.Distance(midpoint, first.EffectCursorPosition)<.035f && Vector2.Distance(first.Movement.AuraChild.transform.position,midpoint)<.035f, "Actual saw orbit and ascended aura stay centered together.");
    Check(Vector2.Distance(second.Movement.AuraChild.transform.position,second.EffectCursorPosition)<.035f, "Second player's actual aura follows its own controller.");
    var muzzle=first.Weapon.FirePoint.position;
    Check(Vector2.Distance(Camera.main.ScreenToWorldPoint(InputHelper.GetMousePosition(first)), muzzle+(Vector3)first.Aim*10)<.02f,"Ordinary weapon aim keeps its full projection.");
    ControllerBindings.SwapSticks();
    SetStick(first, Vector2.left*.72f,true); yield return new WaitForSeconds(1.7f); SetStick(first,Vector2.zero,true);
    Check(Mathf.Abs(Range(first)-10)<.02f && first.Aim.x<-.99f,"Swapped aiming stick extends left to the ten-unit limit.");
}
System.Collections.IEnumerator Run() {
    string result=null; var tests=Tests();
    while(true) {
        object current;
        try { if(!tests.MoveNext()) break; current=tests.Current; }
        catch(Exception e) { result="FAIL: "+e; break; }
        yield return current;
    }
    System.IO.File.WriteAllText("tmp/update12/gameplay.txt",(result??("PASS: "+results.Count+" targeted gameplay checks"))+"\n"+string.Join("\n",results));
}
LocalCoopSession.Instance.StartCoroutine(Run());
return "Started targeted Play Mode checks.";
