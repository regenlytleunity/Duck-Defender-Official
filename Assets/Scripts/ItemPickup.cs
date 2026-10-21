using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public int ItemID;
    float _collectAfter;
    public static void TryDrop(Vector3 position)
    {
        if (LocalCoopSession.Instance == null || !LocalCoopSession.Instance.ItemDropsEnabled || Random.value >= .005f) return;
        var data = SaveSystem.LoadData();
        int id = data.FirstItemDropped ? Random.Range(1, ItemCatalog.Count + 1) : (int)ItemKind.Bomb;
        if (!data.FirstItemDropped) { data.FirstItemDropped = true; SaveSystem.SaveData(data); }
        Spawn(position, id);
    }
    public static ItemPickup Spawn(Vector3 position, int id)
    {
        var go = new GameObject("Dropped " + ItemCatalog.Names[id]); go.transform.position = position;
        var pickup = go.AddComponent<ItemPickup>(); pickup.ItemID = id; pickup._collectAfter = Time.time + .4f;
        var sprite = go.AddComponent<SpriteRenderer>(); sprite.sprite = ItemCatalog.Icon(id); sprite.sortingOrder = 25;
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
        if (!enabled || !ItemCatalog.Valid(ItemID)) return;
        enabled = false;
        var data = SaveSystem.LoadData();
        data.ItemInventory[ItemID] = (int)System.Math.Min(int.MaxValue, (long)data.ItemInventory[ItemID] + 1);
        SaveSystem.SaveData(data);
        if (player != null) player.GetComponent<ItemWheelUI>()?.ShowPickup(ItemID);
        Destroy(gameObject);
    }
}
