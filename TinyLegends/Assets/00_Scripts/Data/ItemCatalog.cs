using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdleBattle
{
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Idle Battle/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private List<ItemData> items = new();
        [SerializeField] private Sprite[] rarityFrames = new Sprite[5];
        [SerializeField] private Sprite tabNormal;
        [SerializeField] private Sprite tabSelected;
        private Dictionary<string, ItemData> byId;

        public IReadOnlyList<ItemData> Items => items;
        public Sprite TabNormal => tabNormal;
        public Sprite TabSelected => tabSelected;

        public Sprite GetRarityFrame(ItemRarity rarity)
        {
            var index = Mathf.Clamp((int)rarity, 0, rarityFrames.Length - 1);
            return rarityFrames != null && index < rarityFrames.Length ? rarityFrames[index] : null;
        }

        public bool TryGet(string itemId, out ItemData item)
        {
            if (byId == null)
                byId = items.Where(value => value != null && !string.IsNullOrWhiteSpace(value.ItemId))
                    .GroupBy(value => value.ItemId, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            return byId.TryGetValue(itemId ?? string.Empty, out item);
        }

#if UNITY_EDITOR
        public void SetItems(IEnumerable<ItemData> values)
        {
            items = values.Where(value => value != null)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal).ToList();
            byId = null;
        }

        public void SetUiSprites(Sprite[] frames, Sprite normal, Sprite selected)
        {
            rarityFrames = frames;
            tabNormal = normal;
            tabSelected = selected;
        }
#endif
    }
}
