using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanelController : MonoBehaviour
    {
        private static readonly Color TabSelectedText = new(1f, .95f, .82f, 1f);
        private static readonly Color TabNormalText = new(.62f, .58f, .50f, 1f);

        private ItemCatalog catalog;
        private PlayerDataManager dataManager;
        private RectTransform content;
        private TMP_Text countText;
        private readonly List<SlotView> slots = new();
        private readonly List<TabView> tabs = new();
        private InventoryCategory selectedCategory = InventoryCategory.Equipment;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToCurrentScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToCurrentScene();

        private static void AttachToCurrentScene()
        {
            var equipment = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(canvas => canvas.transform.Find("Equipment")?.gameObject)
                .FirstOrDefault(value => value != null);
            if (equipment != null && equipment.GetComponent<InventoryPanelController>() == null)
                equipment.AddComponent<InventoryPanelController>();
        }

        private void Awake()
        {
            catalog = Resources.Load<ItemCatalog>("Data/ItemCatalog");
            dataManager = PlayerDataManager.Instance;
            var inventory = transform.Find("SafeArea/Inventory");
            var scrollRect = inventory?.Find("Scroll View")?.GetComponent<ScrollRect>();
            content = scrollRect != null ? scrollRect.content : null;
            countText = inventory?.Find("Info/Count")?.GetComponent<TMP_Text>();

            if (catalog == null || content == null || inventory == null)
            {
                Debug.LogWarning("Inventory/Scroll View/Content 또는 ItemCatalog를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            for (var i = 0; i < content.childCount; i++)
                slots.Add(new SlotView(content.GetChild(i).gameObject));

            SetupTabs(inventory.Find("Horizontal"));
        }

        private void SetupTabs(Transform tabRoot)
        {
            if (tabRoot == null) return;
            var categories = new[]
            {
                InventoryCategory.Equipment,
                InventoryCategory.Consumable,
                InventoryCategory.Other
            };

            for (var i = 0; i < categories.Length && i < tabRoot.childCount; i++)
            {
                var root = tabRoot.GetChild(i).gameObject;
                var button = root.GetComponent<Button>() ?? root.AddComponent<Button>();
                var image = root.GetComponent<Image>();
                var label = root.GetComponentInChildren<TMP_Text>(true);
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                var category = categories[i];
                button.onClick.AddListener(() => SelectCategory(category));
                tabs.Add(new TabView(category, image, label, button));
            }
        }

        private void OnEnable()
        {
            if (!enabled || catalog == null) return;
            dataManager.InventoryChanged -= Refresh;
            dataManager.InventoryChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (dataManager != null) dataManager.InventoryChanged -= Refresh;
        }

        private void OnDestroy()
        {
            foreach (var tab in tabs)
                if (tab.Button != null) tab.Button.onClick.RemoveAllListeners();
        }

        public void SelectCategory(InventoryCategory category)
        {
            if (selectedCategory == category) return;
            selectedCategory = category;
            Refresh();
        }

        public void Refresh()
        {
            var owned = dataManager.Inventory
                .Select(pair => catalog.TryGet(pair.Key, out var item) ? new OwnedItem(item, pair.Value) : default)
                .Where(value => value.Item != null && value.Amount > 0 && MatchesCategory(value.Item.Type) &&
                    !(value.Item.Type == ItemType.Equipment &&
                      FindFirstObjectByType<EquipmentLoadoutController>(FindObjectsInactive.Include)?.IsEquippedItem(value.Item.ItemId) == true))
                .OrderByDescending(value => value.Item.Rarity)
                .ThenBy(value => value.Item.ItemId)
                .ToList();

            EnsureSlotCount(owned.Count);
            for (var i = 0; i < slots.Count; i++)
            {
                slots[i].Root.SetActive(true);
                if (i < owned.Count) slots[i].Bind(owned[i], catalog.GetRarityFrame(owned[i].Item.Rarity));
                else slots[i].Clear();
            }

            if (countText != null) countText.text = owned.Count.ToString();
            RefreshTabs();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private bool MatchesCategory(ItemType type)
        {
            return selectedCategory switch
            {
                InventoryCategory.Equipment => type == ItemType.Equipment,
                InventoryCategory.Consumable => type == ItemType.Consumable,
                _ => type != ItemType.Equipment && type != ItemType.Consumable
            };
        }

        private void RefreshTabs()
        {
            foreach (var tab in tabs)
            {
                var selected = tab.Category == selectedCategory;
                if (tab.Image != null)
                {
                    tab.Image.sprite = selected ? catalog.TabSelected : catalog.TabNormal;
                    tab.Image.color = Color.white;
                    tab.Image.type = Image.Type.Sliced;
                }
                if (tab.Label != null) tab.Label.color = selected ? TabSelectedText : TabNormalText;
            }
        }

        private void EnsureSlotCount(int count)
        {
            if (slots.Count == 0) return;
            while (slots.Count < count)
            {
                var clone = Instantiate(slots[0].Root, content, false);
                clone.name = $"Item ({slots.Count})";
                slots.Add(new SlotView(clone));
            }
        }

        public enum InventoryCategory { Equipment, Consumable, Other }

        private sealed class SlotView
        {
            public readonly GameObject Root;
            private readonly Image frame;
            private readonly Image icon;
            private readonly TMP_Text amount;
            private readonly Sprite emptyFrame;
            private readonly Button button;
            private ItemData boundItem;

            public SlotView(GameObject root)
            {
                Root = root;
                frame = root.GetComponent<Image>();
                icon = root.transform.Find("Icon")?.GetComponent<Image>();
                amount = root.transform.Find("Count")?.GetComponent<TMP_Text>() ??
                         root.transform.Find("Level")?.GetComponent<TMP_Text>();
                emptyFrame = frame != null ? frame.sprite : null;
                button = root.GetComponent<Button>() ?? root.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(EquipBoundItem);
            }

            private void EquipBoundItem()
            {
                if (boundItem == null) return;
                var loadout = FindFirstObjectByType<EquipmentLoadoutController>(FindObjectsInactive.Include);
                if (loadout != null && loadout.TryEquip(boundItem.ItemId))
                    Debug.Log($"장비 장착: {boundItem.DisplayName}");
            }

            public void Bind(OwnedItem owned, Sprite rarityFrame)
            {
                boundItem = owned.Item;
                if (frame != null)
                {
                    frame.sprite = rarityFrame != null ? rarityFrame : emptyFrame;
                    frame.color = Color.white;
                    frame.type = Image.Type.Sliced;
                }
                if (icon != null)
                {
                    icon.sprite = owned.Item.Icon;
                    icon.color = Color.white;
                    icon.preserveAspect = true;
                    icon.gameObject.SetActive(owned.Item.Icon != null);
                }
                if (amount != null)
                {
                    amount.text = owned.Amount > 1 ? $"x{owned.Amount}" : string.Empty;
                    amount.gameObject.SetActive(owned.Amount > 1);
                }
                Root.name = $"Item_{owned.Item.ItemId}";
            }

            public void Clear()
            {
                boundItem = null;
                if (frame != null) frame.sprite = emptyFrame;
                if (icon != null)
                {
                    icon.sprite = null;
                    icon.gameObject.SetActive(false);
                }
                if (amount != null) amount.gameObject.SetActive(false);
            }
        }

        private readonly struct TabView
        {
            public readonly InventoryCategory Category;
            public readonly Image Image;
            public readonly TMP_Text Label;
            public readonly Button Button;
            public TabView(InventoryCategory category, Image image, TMP_Text label, Button button)
            { Category = category; Image = image; Label = label; Button = button; }
        }

        private readonly struct OwnedItem
        {
            public readonly ItemData Item;
            public readonly int Amount;
            public OwnedItem(ItemData item, int amount) { Item = item; Amount = amount; }
        }
    }
}
