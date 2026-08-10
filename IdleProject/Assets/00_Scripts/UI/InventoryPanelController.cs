using System.Collections.Generic;
using System.Linq;
using IdleBattle.Audio;
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
        private EquipmentLoadoutController loadout;
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
            var equipment = SceneRefs.Screen("Equipment");
            if (equipment != null && equipment.GetComponent<InventoryPanelController>() == null)
                equipment.gameObject.AddComponent<InventoryPanelController>();
        }

        private void Awake()
        {
            SceneRefs.Register(this);
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
                slots.Add(new SlotView(this, content.GetChild(i).gameObject));

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

        /// <summary>
        /// 장착 화면 컨트롤러는 같은 Equipment 오브젝트에 붙습니다.
        /// 부착 순서를 보장할 수 없으므로 처음 필요할 때 한 번만 해석하고 캐싱합니다.
        /// </summary>
        private EquipmentLoadoutController Loadout
        {
            get
            {
                if (loadout != null) return loadout;
                if (!TryGetComponent(out loadout)) loadout = SceneRefs.Get<EquipmentLoadoutController>();
                return loadout;
            }
        }

        public void Refresh()
        {
            // 필터 안에서 찾으면 아이템 개수만큼 씬 전체를 훑게 되므로 밖으로 끌어냅니다.
            var equippedSource = Loadout;
            var owned = dataManager.Inventory
                .Select(pair => catalog.TryGet(pair.Key, out var item) ? new OwnedItem(item, pair.Value) : default)
                .Where(value => value.Item != null && value.Amount > 0 && MatchesCategory(value.Item.Type) &&
                    !(value.Item.Type == ItemType.Equipment &&
                      equippedSource != null && equippedSource.IsEquippedItem(value.Item.ItemId)))
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
                slots.Add(new SlotView(this, clone));
            }
        }

        public enum InventoryCategory { Equipment, Consumable, Other }

        private sealed class SlotView
        {
            public readonly GameObject Root;
            private readonly InventoryPanelController owner;
            private readonly Image frame;
            private readonly Image icon;
            private readonly TMP_Text amount;
            private readonly Sprite emptyFrame;
            private readonly Button button;
            private ItemData boundItem;

            public SlotView(InventoryPanelController owner, GameObject root)
            {
                this.owner = owner;
                Root = root;
                frame = root.GetComponent<Image>();
                icon = root.transform.Find("Icon")?.GetComponent<Image>();
                amount = root.transform.Find("Count")?.GetComponent<TMP_Text>() ??
                         root.transform.Find("Level")?.GetComponent<TMP_Text>();
                emptyFrame = frame != null ? frame.sprite : null;
                button = root.GetComponent<Button>() ?? root.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(EquipBoundItem);
                // 아이템 칸은 일반 버튼보다 밝은 선택음을 냅니다. 복제한 칸도 여기서 함께 처리됩니다.
                UiSfxBinder.SetSound(button, SfxId.UiSelect);
                UiSfxBinder.Bind(root);
            }

            private void EquipBoundItem()
            {
                if (boundItem == null || owner == null) return;
                var item = boundItem;
                PopupService.Confirm(
                    "장비 장착",
                    $"<color=#{RarityColor(item.Rarity)}><b>[{RarityLabel(item.Rarity)}] {item.DisplayName}</b></color>\n" +
                    "이 장비를 착용하시겠습니까?",
                    () => Equip(item),
                    confirmLabel: "장착",
                    cancelLabel: "취소",
                    key: $"equip-{item.ItemId}");
            }

            private static string RarityColor(ItemRarity rarity)
            {
                return rarity switch
                {
                    ItemRarity.Common => "D8D8D8",
                    ItemRarity.Uncommon => "4A9FE8",
                    ItemRarity.Rare => "F2C14E",
                    ItemRarity.Epic => "B978E6",
                    ItemRarity.Legendary => "FF7A45",
                    _ => "FFFFFF"
                };
            }

            private static string RarityLabel(ItemRarity rarity)
            {
                return rarity switch
                {
                    ItemRarity.Common => "일반",
                    ItemRarity.Uncommon => "고급",
                    ItemRarity.Rare => "희귀",
                    ItemRarity.Epic => "영웅",
                    ItemRarity.Legendary => "전설",
                    _ => rarity.ToString()
                };
            }

            private void Equip(ItemData item)
            {
                if (item == null || owner == null) return;
                var target = owner.Loadout;
                if (target != null && target.TryEquip(item.ItemId))
                    PopupService.Toast($"{item.DisplayName} 장착 완료");
                else
                    AudioManager.Play(SfxId.UiDenied);
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
