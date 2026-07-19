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

        public event Action<int, int> HealthChanged;
        public event Action<int> Damaged;
        public event Action Died;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => _ = Instance;

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
    }
}
