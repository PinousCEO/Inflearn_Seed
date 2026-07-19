using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class PlayerHpUI : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text healthText;

        private PlayerDataManager playerData;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreateForActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreateForActiveScene();

        private static void TryCreateForActiveScene()
        {
            if (FindFirstObjectByType<PlayerHpUI>() != null) return;

            var hpBar = FindHpBar();
            if (hpBar != null)
                hpBar.gameObject.AddComponent<PlayerHpUI>();
            else
                Debug.LogWarning("HP UI를 찾지 못했습니다. Canvas/Bottom/Status/HpBar 경로를 확인해 주세요.");
        }

        private static Transform FindHpBar()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                var hpBar = FindDescendant(canvas.transform, "HpBar");
                if (hpBar == null) hpBar = FindDescendant(canvas.transform, "HPBar");
                if (hpBar != null) return hpBar;
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == targetName) return child;
            return null;
        }

        private void OnEnable()
        {
            playerData = PlayerDataManager.Instance;
            playerData.HealthChanged -= Refresh;
            playerData.HealthChanged += Refresh;
            Refresh(playerData.CurrentHealth, playerData.MaxHealth);
        }

        private void OnDisable()
        {
            if (playerData != null) playerData.HealthChanged -= Refresh;
        }

        private void Refresh(int currentHealth, int maxHealth)
        {
            if (fill != null)
                fill.fillAmount = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
            if (healthText != null)
                healthText.SetText("{0} / {1}", currentHealth, maxHealth);
        }
    }
}
