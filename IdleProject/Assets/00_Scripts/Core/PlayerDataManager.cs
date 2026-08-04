using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class PlayerDataManager : MonoBehaviour
    {
        private static PlayerDataManager instance;

        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0)] private int currentHealth = 100;
        [SerializeField, Min(1)] private int maxMana = 100;
        [SerializeField, Min(0)] private int currentMana = 100;
        private readonly Dictionary<string, int> inventory = new();
        private const string SaveFileName = "player-data.json";

        public static PlayerDataManager Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<PlayerDataManager>();
                if (instance == null)
                    instance = new GameObject(nameof(PlayerDataManager)).AddComponent<PlayerDataManager>();
                return instance;
            }
        }

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;
        public int CurrentMana => currentMana;
        public int MaxMana => maxMana;
        public IReadOnlyDictionary<string, int> Inventory => inventory;

        public event Action<int, int> HealthChanged;
        public event Action<int> Damaged;
        public event Action Died;
        public event Action<int, int> ManaChanged;
        public event Action<ItemData, int> ItemAcquired;
        public event Action InventoryChanged;

        public string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            maxMana = Mathf.Max(1, maxMana);
            currentMana = Mathf.Clamp(currentMana, 0, maxMana);
            Load();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return;

            var previousHealth = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            Damaged?.Invoke(previousHealth - currentHealth);
            HealthChanged?.Invoke(currentHealth, maxHealth);
            if (currentHealth == 0) Died?.Invoke();
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead) return;

            var nextHealth = Mathf.Min(maxHealth, currentHealth + amount);
            if (nextHealth == currentHealth) return;
            currentHealth = nextHealth;
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetMaxHealth(int value, bool fillHealth = false)
        {
            var nextMaxHealth = Mathf.Max(1, value);
            if (nextMaxHealth == maxHealth && !fillHealth) return;

            maxHealth = nextMaxHealth;
            currentHealth = fillHealth ? maxHealth : Mathf.Clamp(currentHealth, 0, maxHealth);
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void ResetHealth()
        {
            if (currentHealth == maxHealth) return;
            currentHealth = maxHealth;
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public bool TrySpendMana(int amount)
        {
            if (amount < 0 || currentMana < amount) return false;
            currentMana -= amount;
            ManaChanged?.Invoke(currentMana, maxMana);
            return true;
        }

        public void RestoreMana(int amount)
        {
            if (amount <= 0) return;
            var next = Mathf.Min(maxMana, currentMana + amount);
            if (next == currentMana) return;
            currentMana = next;
            ManaChanged?.Invoke(currentMana, maxMana);
        }

        public void SetMaxMana(int value, bool fillMana = false)
        {
            maxMana = Mathf.Max(1, value);
            currentMana = fillMana ? maxMana : Mathf.Clamp(currentMana, 0, maxMana);
            ManaChanged?.Invoke(currentMana, maxMana);
        }

        public void AddItem(ItemData item, int amount = 1)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || amount <= 0)
                return;

            inventory.TryGetValue(item.ItemId, out var currentAmount);
            var newAmount = currentAmount + amount;
            inventory[item.ItemId] = newAmount;
            Save();
            ItemAcquired?.Invoke(item, newAmount);
            InventoryChanged?.Invoke();
        }

        public int GetItemCount(ItemData item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId)) return 0;
            return inventory.TryGetValue(item.ItemId, out var amount) ? amount : 0;
        }

        public void Save()
        {
            var data = new PlayerSaveData
            {
                version = 1,
                items = inventory
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new InventorySaveEntry { itemId = pair.Key, amount = pair.Value })
                    .ToList()
            };

            try
            {
                var path = SaveFilePath;
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporaryPath, path);
            }
            catch (Exception exception)
            {
                Debug.LogError($"인벤토리 저장에 실패했습니다: {exception.Message}", this);
            }
        }

        public void Load()
        {
            inventory.Clear();
            try
            {
                if (!File.Exists(SaveFilePath)) return;
                var data = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(SaveFilePath));
                if (data?.items == null) return;
                foreach (var entry in data.items)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.amount <= 0) continue;
                    inventory[entry.itemId.Trim()] = entry.amount;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"인벤토리 불러오기에 실패했습니다: {exception.Message}", this);
            }
            finally
            {
                InventoryChanged?.Invoke();
            }
        }

        [Serializable]
        private sealed class PlayerSaveData
        {
            public int version = 1;
            public List<InventorySaveEntry> items = new();
        }

        [Serializable]
        private sealed class InventorySaveEntry
        {
            public string itemId;
            public int amount;
        }
    }
}
