using System;
using System.Collections.Generic;
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
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField, Min(0)] private int experience;
        [SerializeField, Min(0)] private long coins;
        private string characterId = string.Empty;
        private string playerName = string.Empty;
        private readonly Dictionary<string, int> inventory = new();
        private readonly HashSet<string> equippedItems = new(StringComparer.Ordinal);
        private const int SaveVersion = 2;
        private System.Threading.Tasks.Task loadTask;

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
        public int Level => level;
        public int Experience => experience;
        public int ExperienceToNextLevel => GetExperienceRequirement(level);
        public long Coins => coins;
        public IReadOnlyCollection<string> EquippedItems => equippedItems;
        public bool IsLoaded { get; private set; }

        /// <summary>Select 씬에서 고른 캐릭터(직업) ID입니다. 아직 고르지 않았으면 비어 있습니다.</summary>
        public string CharacterId => characterId;

        /// <summary>Select 씬에서 입력한 플레이어 이름입니다.</summary>
        public string PlayerName => playerName;

        /// <summary>데이터를 불러왔고, 캐릭터까지 정해져 있으면 true. Title에서 Select 건너뛰기 판별에 씁니다.</summary>
        public bool HasCharacter => IsLoaded && !string.IsNullOrWhiteSpace(characterId);

        public event Action<int, int> HealthChanged;
        public event Action<int> Damaged;
        public event Action Died;
        public event Action<int, int> ManaChanged;
        public event Action<ItemData, int> ItemAcquired;
        public event Action InventoryChanged;
        public event Action<int, int, int> ExperienceChanged;
        public event Action<long> CoinsChanged;

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
            if (!IsLoaded) return;
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || amount <= 0)
                return;

            inventory.TryGetValue(item.ItemId, out var currentAmount);
            var newAmount = currentAmount + amount;
            inventory[item.ItemId] = newAmount;
            Save();
            ItemAcquired?.Invoke(item, newAmount);
            InventoryChanged?.Invoke();
        }

        public void NotifyInventoryChanged()
        {
            InventoryChanged?.Invoke();
        }

        public void AddExperience(int amount)
        {
            if (!IsLoaded) return;
            if (amount <= 0) return;
            experience += amount;
            while (experience >= GetExperienceRequirement(level))
            {
                experience -= GetExperienceRequirement(level);
                level++;
                maxHealth += 10;
                maxMana += 3;
                currentHealth = maxHealth;
                currentMana = maxMana;
            }
            Save();
            ExperienceChanged?.Invoke(level, experience, GetExperienceRequirement(level));
            HealthChanged?.Invoke(currentHealth, maxHealth);
            ManaChanged?.Invoke(currentMana, maxMana);
        }

        public void AddCoins(long amount)
        {
            if (!IsLoaded) return;
            if (amount <= 0) return;
            coins = Math.Max(0L, coins + amount);
            Save();
            CoinsChanged?.Invoke(coins);
        }

        public static int GetExperienceRequirement(int targetLevel)
        {
            return Mathf.Max(20, Mathf.RoundToInt(50f * Mathf.Pow(1.16f, Mathf.Max(0, targetLevel - 1))));
        }

        public int GetItemCount(ItemData item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId)) return 0;
            return inventory.TryGetValue(item.ItemId, out var amount) ? amount : 0;
        }

        public bool IsItemEquipped(string itemId) => !string.IsNullOrWhiteSpace(itemId) && equippedItems.Contains(itemId);

        public void SetItemEquipped(string itemId, bool value)
        {
            if (!IsLoaded) return;
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (value) equippedItems.Add(itemId);
            else equippedItems.Remove(itemId);
            Save();
            InventoryChanged?.Invoke();
        }

        public void Save()
        {
            if (!IsLoaded)
            {
                Debug.LogWarning("Firestore save skipped because guest login/data loading is not complete.", this);
                return;
            }
            var json = JsonUtility.ToJson(CreateSaveData(), true);
            _ = SaveToFirestoreAsync(json);
        }

        public void Load()
        {
            loadTask ??= LoadFromFirestoreAsync();
        }

        /// <summary>Firestore 로드가 끝날 때까지 기다립니다. 아직 시작 전이면 시작합니다.</summary>
        public System.Threading.Tasks.Task EnsureLoadedAsync()
        {
            loadTask ??= LoadFromFirestoreAsync();
            return loadTask;
        }

        /// <summary>Select 씬에서 고른 캐릭터와 이름을 저장합니다. 로드가 끝난 뒤에 반영해 덮어쓰기를 막습니다.</summary>
        public async System.Threading.Tasks.Task SetCharacterAsync(string newCharacterId, string newPlayerName)
        {
            await EnsureLoadedAsync();
            characterId = newCharacterId?.Trim() ?? string.Empty;
            playerName = newPlayerName?.Trim() ?? string.Empty;
            Save();
        }

        private PlayerSaveData CreateSaveData()
        {
            return new PlayerSaveData
            {
                version = SaveVersion,
                level = level,
                experience = experience,
                coins = coins,
                characterId = characterId,
                playerName = playerName,
                items = inventory
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new InventorySaveEntry { itemId = pair.Key, amount = pair.Value })
                    .ToList(),
                equippedItems = equippedItems.OrderBy(value => value, StringComparer.Ordinal).ToList()
            };
        }

        private void ApplySaveData(PlayerSaveData data)
        {
            if (data == null) return;
            inventory.Clear();
            equippedItems.Clear();
            if (data.version < SaveVersion)
            {
                level = 1;
                experience = 0;
                coins = 0;
                characterId = string.Empty;
                playerName = string.Empty;
                return;
            }

            level = Mathf.Max(1, data.level);
            experience = Mathf.Max(0, data.experience);
            coins = Math.Max(0L, data.coins);
            characterId = data.characterId?.Trim() ?? string.Empty;
            playerName = data.playerName?.Trim() ?? string.Empty;
            if (data.items != null)
                foreach (var entry in data.items)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.itemId) && entry.amount > 0)
                        inventory[entry.itemId.Trim()] = entry.amount;
            if (data.equippedItems != null)
                foreach (var itemId in data.equippedItems)
                    if (!string.IsNullOrWhiteSpace(itemId)) equippedItems.Add(itemId.Trim());
        }

        private async System.Threading.Tasks.Task LoadFromFirestoreAsync()
        {
            try
            {
                var json = await FirebaseInitializer.Instance.LoadPlayerJsonAsync();
                if (this == null) return;

                if (string.IsNullOrWhiteSpace(json))
                {
                    await FirebaseInitializer.Instance.SavePlayerJsonAsync(
                        JsonUtility.ToJson(CreateSaveData(), true), SaveVersion);
                    IsLoaded = true;
                    NotifyDataLoaded();
                    return;
                }

                ApplySaveData(JsonUtility.FromJson<PlayerSaveData>(json));
                IsLoaded = true;
                NotifyDataLoaded();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Firestore load failed: {exception.Message}", this);
            }
        }

        private async System.Threading.Tasks.Task SaveToFirestoreAsync(string json)
        {
            try
            {
                await FirebaseInitializer.Instance.SavePlayerJsonAsync(json, SaveVersion);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Firestore save failed: {exception.Message}", this);
            }
        }

        private void NotifyDataLoaded()
        {
            InventoryChanged?.Invoke();
            ExperienceChanged?.Invoke(level, experience, GetExperienceRequirement(level));
            CoinsChanged?.Invoke(coins);
            HealthChanged?.Invoke(currentHealth, maxHealth);
            ManaChanged?.Invoke(currentMana, maxMana);
        }

        [Serializable]
        private sealed class PlayerSaveData
        {
            public int version = SaveVersion;
            public int level = 1;
            public int experience;
            public long coins;
            public string characterId = string.Empty;
            public string playerName = string.Empty;
            public List<InventorySaveEntry> items = new();
            public List<string> equippedItems = new();
        }

        [Serializable]
        private sealed class InventorySaveEntry
        {
            public string itemId;
            public int amount;
        }
    }
}
