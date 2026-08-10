using System.Collections.Generic;
using IdleBattle.Audio;
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
        private bool hasSkillIcons;

        // HP/MP는 매 프레임 호출되지만, 값이 그대로면 TMP 메시를 다시 만들 필요가 없다.
        private int lastHpCurrent = int.MinValue;
        private int lastHpMax = int.MinValue;
        private int lastMpCurrent = int.MinValue;
        private int lastMpMax = int.MinValue;

        private void OnEnable()
        {
            player = PlayerDataManager.Instance;
            // 인스펙터에 연결돼 있으면 탐색하지 않고, 없을 때만 캐싱된 씬 참조를 씁니다.
            if (battle == null) battle = SceneRefs.Get<BattleGameController>();
            player.HealthChanged -= RefreshHealth;
            player.ManaChanged -= RefreshMana;
            player.HealthChanged += RefreshHealth;
            player.ManaChanged += RefreshMana;
            RefreshHealth(player.CurrentHealth, player.MaxHealth);
            RefreshMana(player.CurrentMana, player.MaxMana);
            hasSkillIcons = false;
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
            if (!hasSkillIcons)
            {
                if (battle == null) battle = SceneRefs.Get<BattleGameController>();
                RefreshSkillIcons();
            }

            // Domain Reload 비활성화나 씬 재로드 시 이벤트를 놓쳐도 UI는 항상 실제 데이터와 일치한다.
            RefreshHealth(player.CurrentHealth, player.MaxHealth);
            RefreshMana(player.CurrentMana, player.MaxMana);
            RefreshPotionCooldowns();
            if (player.IsDead) return;

            if ((float)player.CurrentHealth / player.MaxHealth <= autoPotionThreshold &&
                Time.time >= nextHealthPotionTime)
            {
                player.Heal(healthPotionRecovery);
                AudioManager.Play(SfxId.PotionHeal);
                nextHealthPotionTime = Time.time + healthPotionCooldown;
            }

            if ((float)player.CurrentMana / player.MaxMana <= autoPotionThreshold &&
                Time.time >= nextManaPotionTime)
            {
                player.RestoreMana(manaPotionRecovery);
                AudioManager.Play(SfxId.PotionMana);
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
            // 전투 컨트롤러가 아직 스킬을 다 읽지 못했으면 다음 프레임에 다시 시도합니다.
            if (battle == null || battle.Skills.Count == 0) return;
            for (var i = 0; i < skillSlots.Count; i++)
            {
                var icon = skillSlots[i].Icon;
                if (icon == null) continue;
                var skill = i < battle.Skills.Count ? battle.Skills[i] : null;
                icon.sprite = skill != null ? skill.Icon : null;
                // 그림이 없는 칸이 흰 사각형으로 남지 않게 합니다.
                icon.enabled = icon.sprite != null;
            }
            hasSkillIcons = true;
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
            if (current == lastHpCurrent && maximum == lastHpMax) return;
            lastHpCurrent = current;
            lastHpMax = maximum;

            if (hpFill != null) hpFill.fillAmount = maximum > 0 ? (float)current / maximum : 0f;
            if (hpText != null) hpText.SetText("{0} / {1}", current, maximum);
        }

        private void RefreshMana(int current, int maximum)
        {
            if (current == lastMpCurrent && maximum == lastMpMax) return;
            lastMpCurrent = current;
            lastMpMax = maximum;

            if (mpFill != null) mpFill.fillAmount = maximum > 0 ? (float)current / maximum : 0f;
            if (mpText != null) mpText.SetText("{0} / {1}", current, maximum);
        }
    }
}
