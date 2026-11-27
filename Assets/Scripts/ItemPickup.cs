using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public int ItemID;
    float _collectAfter;
    public static void TryDrop(Vector3 position, bool elite = false, PlayerStats owner = null)
    {
        if (!TutorialDirector.AllowItems || LocalCoopSession.Instance == null) return;
        float bonus = owner != null ? owner.KeyDropBonus : 0;
        if (owner != null && owner.HasAscension(CardAscension.KeySpecialist)) bonus = .5f;
        float chance = (elite ? .007f : .005f) * (1 + bonus + PermanentKeyBonus);
        if (Random.value < chance) Spawn(position, 0);
    }
    public static float PermanentKeyBonus;
    public static ItemPickup Spawn(Vector3 position, int id)
    {
        var go = new GameObject("Dropped key"); go.transform.position = position;
        var pickup = go.AddComponent<ItemPickup>(); pickup.ItemID = id; pickup._collectAfter = Time.time + .4f;
        var sprite = go.AddComponent<SpriteRenderer>();
        var icon = GameUI.Instance != null ? GameUI.Instance.KeyIcon : null;
        sprite.sprite = icon != null ? icon : ItemArea.Circle;
        sprite.color = icon != null ? Color.white : new Color(1, .8f, .15f);
        sprite.sortingOrder = 25;
        go.transform.localScale = Vector3.one * .65f;
        var collider = go.AddComponent<CircleCollider2D>(); collider.radius = .4f;
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 2;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.excludeLayers = ~LayerMask.GetMask("Ground");
        return pickup;
    }
    void Update()
    {
        if (Time.timeScale == 0 || Time.time < _collectAfter || LocalCoopSession.Instance == null) return;
        foreach (var player in LocalCoopSession.Instance.Players)
        {
            if (!player.Alive || (player.transform.position - transform.position).sqrMagnitude > 1) continue;
            Collect(player); break;
        }
    }
    public void Collect(LocalPlayer player = null)
    {
        if (!enabled) return;
        enabled = false;
        var data = SaveSystem.LoadData();
        data.Keys = (int)System.Math.Min(int.MaxValue, (long)data.Keys + 1);
        SaveSystem.SaveData(data);
        if (player != null)
        {
            if (player.Stats.HasAscension(CardAscension.KeySpecialist)) player.Stats.KeyDamageUntil = Time.time + 5;
            player.GetComponent<ItemWheelUI>()?.ShowPickup(0);
        }
        Destroy(gameObject);
    }
}
