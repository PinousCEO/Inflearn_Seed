using UnityEngine;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class LootRarityVfx : MonoBehaviour
    {
        [SerializeField] private ItemRarity rarity;
        [SerializeField] private Color themeColor = Color.white;

        public ItemRarity Rarity => rarity;
        public Color ThemeColor => themeColor;

        public void Configure(ItemRarity value, Color color)
        {
            rarity = value;
            themeColor = color;
        }
    }
}
