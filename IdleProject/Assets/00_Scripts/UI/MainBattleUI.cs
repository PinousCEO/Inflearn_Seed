using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleUI : MonoBehaviour
    {
        [System.Serializable]
        public sealed class SkillSlot
        {
            [SerializeField] private Image icon;
            [SerializeField] private Image cooldown;
            [SerializeField] private TMP_Text cooldownText;

            public Image Icon => icon;
            public Image Cooldown => cooldown;
            public TMP_Text CooldownText => cooldownText;
        }

        [Header("Scene References (assign in Inspector)")]
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Image mpFill;
        [SerializeField] private TMP_Text mpText;
        [SerializeField] private BattleGameController battle;
        [SerializeField] private List<SkillSlot> skillSlots = new List<SkillSlot>(5);

        [Header("Potion UI References (HPPosition / MPPosition)")]
        [SerializeField] private Image healthPotionCooldownFill;
        [SerializeField] private TMP_Text healthPotionCooldownText;
        [SerializeField] private Image manaPotionCooldownFill;
        [SerializeField] private TMP_Text manaPotionCooldownText;

        [Header("Automatic Potion")]
        [SerializeField, Range(.05f, .95f)] private float autoPotionThreshold = .6f;
        [SerializeField, Min(1)] private int healthPotionRecovery = 45;
        [SerializeField, Min(.1f)] private float healthPotionCooldown = 8f;
        [SerializeField, Min(1)] private int manaPotionRecovery = 45;
        [SerializeField, Min(.1f)] private float manaPotionCooldown = 8f;

        private PlayerDataManager player;
        private float nextHealthPotionTime;
        private float nextManaPotionTime;

        private void OnEnable()
        {
            player = PlayerDataManager.Instance;
            if (battle == null) battle = FindFirstObjectByType<BattleGameController>();
            player.HealthChanged -= RefreshHealth;
            player.ManaChanged -= RefreshMana;
            player.HealthChanged += RefreshHealth;
            player.ManaChanged += RefreshMana;
            RefreshHealth(player.CurrentHealth, player.MaxHealth);
            RefreshMana(player.CurrentMana, player.MaxMana);
            RefreshSkillIcons();
        }

        private void OnDisable()
        {
            if (player == null) return;
            player.HealthChanged -= RefreshHealth;
            player.ManaChanged -= RefreshMana;
        }

        private void Update()
        {
            if (player == null) return;

            // Domain Reload 비활성화나 씬 재로드 시 이벤트를 놓쳐도 UI는 항상 실제 데이터와 일치한다.
            RefreshHealth(player.CurrentHealth, player.MaxHealth);
            RefreshMana(player.CurrentMana, player.MaxMana);
            RefreshPotionCooldowns();
            if (player.IsDead) return;

            if ((float)player.CurrentHealth / player.MaxHealth <= autoPotionThreshold &&
                Time.time >= nextHealthPotionTime)
            {
                player.Heal(healthPotionRecovery);
                nextHealthPotionTime = Time.time + healthPotionCooldown;
            }

            if ((float)player.CurrentMana / player.MaxMana <= autoPotionThreshold &&
                Time.time >= nextManaPotionTime)
            {
                player.RestoreMana(manaPotionRecovery);
                nextManaPotionTime = Time.time + manaPotionCooldown;
            }

            RefreshCooldowns();
        }

        private void RefreshPotionCooldowns()
        {
            RefreshPotionCooldown(
                healthPotionCooldownFill,
                healthPotionCooldownText,
                Mathf.Max(0f, nextHealthPotionTime - Time.time),
                healthPotionCooldown);
            RefreshPotionCooldown(
                manaPotionCooldownFill,
                manaPotionCooldownText,
                Mathf.Max(0f, nextManaPotionTime - Time.time),
                manaPotionCooldown);
        }

        private static void RefreshPotionCooldown(
            Image fill,
            TMP_Text text,
            float remaining,
            float cooldown)
        {
            if (fill != null)
            {
                fill.type = Image.Type.Filled;
                fill.fillAmount = cooldown > 0f ? remaining / cooldown : 0f;
            }

            if (text != null)
                text.SetText(remaining > 0f ? "{0:0.0}" : string.Empty, remaining);
        }

        private void RefreshSkillIcons()
        {
            if (battle == null) return;
            for (var i = 0; i < skillSlots.Count && i < battle.Skills.Count; i++)
            {
                var skill = battle.Skills[i];
                if (skillSlots[i].Icon != null && skill != null && skill.Icon != null)
                    skillSlots[i].Icon.sprite = skill.Icon;
            }
        }

        private void RefreshCooldowns()
        {
            if (battle == null) return;
            for (var i = 0; i < skillSlots.Count && i < battle.Skills.Count; i++)
            {
                var skill = battle.Skills[i];
                if (skill == null) continue;
                var remaining = battle.GetCooldownRemaining(i);
                if (skillSlots[i].Cooldown != null)
                {
                    skillSlots[i].Cooldown.type = Image.Type.Filled;
                    skillSlots[i].Cooldown.fillMethod = Image.FillMethod.Radial360;
                    skillSlots[i].Cooldown.fillAmount =
                        skill.Cooldown > 0f ? remaining / skill.Cooldown : 0f;
                }
                if (skillSlots[i].CooldownText != null)
                    skillSlots[i].CooldownText.SetText(
                        remaining > 0f ? "{0:0.0}" : string.Empty,
                        remaining);
            }
        }

        private void RefreshHealth(int current, int maximum)
        {
            if (hpFill != null) hpFill.fillAmount = maximum > 0 ? (float)current / maximum : 0f;
            if (hpText != null) hpText.SetText("{0} / {1}", current, maximum);
        }

        private void RefreshMana(int current, int maximum)
        {
            if (mpFill != null) mpFill.fillAmount = maximum > 0 ? (float)current / maximum : 0f;
            if (mpText != null) mpText.SetText("{0} / {1}", current, maximum);
        }
    }
}
