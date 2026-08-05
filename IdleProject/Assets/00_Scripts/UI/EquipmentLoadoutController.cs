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
        private static readonly string[] ItemIds =
        { "equipment-003", "equipment-004", "equipment-005", "equipment-006", "equipment-007", "equipment-008", "equipment-009", "equipment-010", "equipment-011" };
        private readonly bool[] equipped = new bool[9];
        private ItemCatalog catalog;
        private PlayerDataManager data;
        private Transform[] slotRoots = System.Array.Empty<Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            var screen = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include)?.transform.Find("Equipment");
            if (screen != null && screen.GetComponent<EquipmentLoadoutController>() == null)
                screen.gameObject.AddComponent<EquipmentLoadoutController>();
        }

        private void Awake()
        {
            catalog = Resources.Load<ItemCatalog>("Data/ItemCatalog");
            data = PlayerDataManager.Instance;
            var grid = transform.Find("SafeArea/Character_Equipments/Equipment/Grid");
            if (grid != null)
            {
                var roots = new System.Collections.Generic.List<Transform>();
                for (var i = 0; i < grid.childCount && roots.Count < ItemIds.Length; i++)
                    roots.Add(grid.GetChild(i));
                slotRoots = roots.ToArray();
            }
            if (slotRoots.Length == 0)
            {
                var slots = transform.GetComponentsInChildren<Transform>(true);
                var roots = new System.Collections.Generic.List<Transform>();
                for (var i = 0; i < ItemIds.Length; i++)
                {
                    var slot = FindSlot(slots, i + 1);
                    if (slot != null) roots.Add(slot);
                }
                slotRoots = roots.ToArray();
            }
            for (var i = 0; i < ItemIds.Length; i++)
            {
                equipped[i] = data.IsItemEquipped(ItemIds[i]);
                var slot = i < slotRoots.Length ? slotRoots[i] : null;
                if (slot == null) continue;
                var index = i;
                var button = slot.GetComponent<Button>() ?? slot.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    if (equipped[index]) TryUnequip(ItemIds[index]);
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
                var slot = i < slotRoots.Length ? slotRoots[i] : null;
                if (slot == null || !catalog.TryGet(ItemIds[i], out var item)) continue;
                var icon = slot.Find("Icon")?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.sprite = equipped[i] ? item.Icon : null;
                    icon.gameObject.SetActive(equipped[i] && item.Icon != null);
                }
                var frame = slot.GetComponent<Image>();
                if (frame != null)
                {
                    // 미장착 슬롯도 인벤토리의 일반 장비 슬롯처럼 프레임은 유지합니다.
                    frame.sprite = equipped[i]
                        ? catalog.GetRarityFrame(item.Rarity)
                        : catalog.GetRarityFrame(ItemRarity.Common);
                    frame.color = Color.white;
                }
                var level = slot.Find("Level")?.GetComponent<TMPro.TMP_Text>();
                if (level != null)
                {
                    // 강화하지 않은 장비에는 강화 수치를 표시하지 않습니다.
                    level.text = string.Empty;
                    level.gameObject.SetActive(false);
                }
                var badge = slot.Find("EquippedBadge")?.gameObject;
                if (badge != null) badge.SetActive(equipped[i] && data.GetItemCount(item) > 0);
            }
        }

        private void ClearUpgradeLabels()
        {
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
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
    }
}
