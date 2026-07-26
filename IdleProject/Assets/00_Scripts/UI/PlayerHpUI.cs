using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class PlayerHpUI : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text healthText;

        private PlayerDataManager playerData;

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
