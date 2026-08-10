using IdleBattle.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace IdleBattle.UI
{
    /// <summary>장비 화면의 9개 슬롯을 실제 보유/착용 데이터와 연결합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentLoadoutController : MonoBehaviour
    {
        // 특수효과가 붙는 전설 9종을 그대로 씁니다. 목록이 한 곳에만 있어야
        // 슬롯 순서와 효과 번호(1~9)가 어긋나지 않습니다.
        private static readonly string[] ItemIds = LegendaryEquipmentSystem.LegendaryIds;
        private readonly bool[] equipped = new bool[9];
        private ItemCatalog catalog;
        private PlayerDataManager data;
        // 슬롯의 자식 참조를 Awake에서 한 번만 해석해 둡니다.
        // Refresh는 아이템을 얻을 때마다 호출되므로 여기서 매번 이름으로 찾으면 안 됩니다.
        private SlotView[] slotViews = System.Array.Empty<SlotView>();
        private TMP_Text[] cachedLabels = System.Array.Empty<TMP_Text>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            var screen = SceneRefs.Screen("Equipment");
            if (screen != null && screen.GetComponent<EquipmentLoadoutController>() == null)
                screen.gameObject.AddComponent<EquipmentLoadoutController>();
        }

        private void Awake()
        {
            // 다른 UI가 씬 전체를 훑지 않고 이 컨트롤러를 찾을 수 있게 등록합니다.
            SceneRefs.Register(this);
            catalog = Resources.Load<ItemCatalog>("Data/ItemCatalog");
            data = PlayerDataManager.Instance;
            cachedLabels = GetComponentsInChildren<TMP_Text>(true);

            var roots = new System.Collections.Generic.List<Transform>(ItemIds.Length);
            var grid = transform.Find("SafeArea/Character_Equipments/Equipment/Grid");
            if (grid != null)
            {
                for (var i = 0; i < grid.childCount && roots.Count < ItemIds.Length; i++)
                    roots.Add(grid.GetChild(i));
            }
            if (roots.Count == 0)
            {
                var slots = transform.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < ItemIds.Length; i++)
                {
                    var slot = FindSlot(slots, i + 1);
                    if (slot != null) roots.Add(slot);
                }
            }

            slotViews = new SlotView[roots.Count];
            for (var i = 0; i < roots.Count; i++) slotViews[i] = new SlotView(roots[i]);

            for (var i = 0; i < ItemIds.Length; i++)
            {
                equipped[i] = data.IsItemEquipped(ItemIds[i]);
                var slot = i < slotViews.Length ? slotViews[i].Root : null;
                if (slot == null) continue;
                var index = i;
                var button = slot.GetComponent<Button>() ?? slot.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    if (!equipped[index]) return;
                    var itemName = catalog != null && catalog.TryGet(ItemIds[index], out var item)
                        ? item.DisplayName
                        : "장비";
                    if (TryUnequip(ItemIds[index])) PopupService.Toast($"{itemName} 해제 완료");
                });
            }
            data.InventoryChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (data != null) data.InventoryChanged -= Refresh;
        }

        private void Refresh()
        {
            if (catalog == null || data == null) return;
            ClearUpgradeLabels();
            for (var i = 0; i < ItemIds.Length; i++)
            {
                // 착용 상태는 항상 저장 데이터에서 다시 읽습니다.
                // Awake 시점에는 아직 로그인/로드 전이라 전부 미착용으로 보이기 때문입니다.
                equipped[i] = data.IsItemEquipped(ItemIds[i]);
                if (i >= slotViews.Length || slotViews[i].Root == null) continue;
                if (!catalog.TryGet(ItemIds[i], out var item)) continue;
                var slot = slotViews[i];
                if (slot.Icon != null)
                {
                    slot.Icon.sprite = equipped[i] ? item.Icon : null;
                    slot.Icon.gameObject.SetActive(equipped[i] && item.Icon != null);
                }
                if (slot.Frame != null)
                {
                    // 미장착 슬롯도 인벤토리의 일반 장비 슬롯처럼 프레임은 유지합니다.
                    slot.Frame.sprite = equipped[i]
                        ? catalog.GetRarityFrame(item.Rarity)
                        : catalog.GetRarityFrame(ItemRarity.Common);
                    slot.Frame.color = Color.white;
                }
                if (slot.Level != null)
                {
                    // 강화하지 않은 장비에는 강화 수치를 표시하지 않습니다.
                    slot.Level.text = string.Empty;
                    slot.Level.gameObject.SetActive(false);
                }
                if (slot.Badge != null) slot.Badge.SetActive(equipped[i] && data.GetItemCount(item) > 0);
            }
        }

        private void ClearUpgradeLabels()
        {
            foreach (var text in cachedLabels)
            {
                if (text == null) continue;
                var value = text.text != null ? text.text.Trim() : string.Empty;
                if (value == "+12" || (value.StartsWith("+") && value.Length > 1))
                {
                    text.text = string.Empty;
                    text.gameObject.SetActive(false);
                }
            }
        }

        public bool TryEquip(string itemId)
        {
            for (var i = 0; i < ItemIds.Length; i++)
            {
                if (ItemIds[i] != itemId) continue;
                equipped[i] = true;
                LegendaryEquipmentSystem.Instance.SetEquipped(i + 1, true);
                data.SetItemEquipped(itemId, true);
                AudioManager.Play(SfxId.Equip);
                Refresh();
                return true;
            }
            return false;
        }

        public bool IsEquippedItem(string itemId)
        {
            for (var i = 0; i < ItemIds.Length; i++)
                if (ItemIds[i] == itemId) return equipped[i];
            return false;
        }

        public bool TryUnequip(string itemId)
        {
            for (var i = 0; i < ItemIds.Length; i++)
            {
                if (ItemIds[i] != itemId) continue;
                equipped[i] = false;
                LegendaryEquipmentSystem.Instance.SetEquipped(i + 1, false);
                data.SetItemEquipped(itemId, false);
                AudioManager.Play(SfxId.Unequip);
                Refresh();
                return true;
            }
            return false;
        }

        private static Transform FindSlot(Transform[] transforms, int index)
        {
            var name = $"EquippedSlot_{index:00}";
            foreach (var value in transforms) if (value.name == name) return value;
            return null;
        }

        /// <summary>슬롯 한 칸의 자식 참조 묶음. 이름 탐색은 생성 시 단 한 번만 수행합니다.</summary>
        private readonly struct SlotView
        {
            public readonly Transform Root;
            public readonly Image Frame;
            public readonly Image Icon;
            public readonly TMP_Text Level;
            public readonly GameObject Badge;

            public SlotView(Transform root)
            {
                Root = root;
                Frame = root != null ? root.GetComponent<Image>() : null;
                var icon = root != null ? root.Find("Icon") : null;
                Icon = icon != null ? icon.GetComponent<Image>() : null;
                var level = root != null ? root.Find("Level") : null;
                Level = level != null ? level.GetComponent<TMP_Text>() : null;
                var badge = root != null ? root.Find("EquippedBadge") : null;
                Badge = badge != null ? badge.gameObject : null;
            }
        }
    }
}
