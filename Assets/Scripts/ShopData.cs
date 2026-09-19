using UnityEngine;

[CreateAssetMenu(fileName = "NewShopPack", menuName = "DuckDefender/Shop Pack")]
public class ShopPackDefinition : ScriptableObject
{
    public string PackName;
    public int Cost;
    public CardPackType PackType;
    public Sprite ClosedPackIcon;
    public Sprite OpenedPackIcon; // The graphic after slicing
}