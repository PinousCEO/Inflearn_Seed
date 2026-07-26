using System;
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

        public event Action<int, int> HealthChanged;
        public event Action<int> Damaged;
        public event Action Died;
        public event Action<int, int> ManaChanged;

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
    }
}
