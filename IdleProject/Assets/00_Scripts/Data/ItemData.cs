using UnityEngine;

namespace IdleBattle
{
    public enum ItemType
    {
        Equipment,
        Consumable,
        Material,
        Currency,
        Quest,
        Other
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    [CreateAssetMenu(
        fileName = "ItemData",
        menuName = "Idle Battle/Item Data")]
    public sealed class ItemData : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName = "새 아이템";
        [SerializeField, TextArea(3, 8)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private ItemType itemType;
        [SerializeField] private ItemRarity rarity;
        [SerializeField, Min(0)] private int buyPrice;
        [SerializeField, Min(0)] private int sellPrice;
        [SerializeField, Min(1)] private int maxStack = 1;
        [SerializeField, Range(0f, 100f)] private float dropRatePercent;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemType Type => itemType;
        public ItemRarity Rarity => rarity;
        public int BuyPrice => buyPrice;
        public int SellPrice => sellPrice;
        public int MaxStack => maxStack;
        public float DropRatePercent => dropRatePercent;

        public void Initialize(string id, string initialName)
        {
            itemId = id;
            displayName = initialName;
            description = string.Empty;
            icon = null;
            itemType = ItemType.Equipment;
            rarity = ItemRarity.Common;
            buyPrice = 0;
            sellPrice = 0;
            maxStack = 1;
            dropRatePercent = 0f;
        }

        private void OnValidate()
        {
            itemId = itemId?.Trim() ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? name
                : displayName.Trim();
            buyPrice = Mathf.Max(0, buyPrice);
            sellPrice = Mathf.Max(0, sellPrice);
            maxStack = Mathf.Max(1, maxStack);
            dropRatePercent = Mathf.Clamp(dropRatePercent, 0f, 100f);
        }
    }
}
