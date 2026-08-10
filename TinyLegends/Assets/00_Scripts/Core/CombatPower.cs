using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleBattle
{
    /// <summary>전투력을 한 번 계산한 결과입니다. 총합과 함께 항목별 점수를 남겨 표시·검증에 씁니다.</summary>
    public readonly struct CombatPowerResult
    {
        /// <summary>랭킹에 올라가는 최종 점수입니다.</summary>
        public readonly long Total;

        public readonly float Offense;
        public readonly float Survival;
        public readonly float Support;
        public readonly float LevelBonus;

        public CombatPowerResult(long total, float offense, float survival, float support, float levelBonus)
        {
            Total = total;
            Offense = offense;
            Survival = survival;
            Support = support;
            LevelBonus = levelBonus;
        }

        /// <summary>디버그·툴팁용 한 줄 요약입니다.</summary>
        public override string ToString()
            => $"{Total:N0} (공격 {Offense:N0} · 생존 {Survival:N0} · 지원 {Support:N0} · 레벨 {LevelBonus:N0})";
    }

    /// <summary>
    /// 지금 플레이어의 치명타 수치입니다. 전투에서 한 대 때릴 때마다 이 값으로 굴립니다.
    /// </summary>
    public readonly struct CriticalProfile
    {
        /// <summary>치명타가 터질 확률(%)입니다.</summary>
        public readonly float ChancePercent;

        /// <summary>치명타일 때의 총 피해 배율(%)입니다. 150이면 1.5배입니다.</summary>
        public readonly float DamagePercent;

        public CriticalProfile(float chancePercent, float damagePercent)
        {
            ChancePercent = chancePercent;
            DamagePercent = damagePercent;
        }

        public bool Roll() => ChancePercent > 0f && UnityEngine.Random.value * 100f < ChancePercent;

        /// <summary>치명타 배율을 먹입니다. 배율이 100% 아래여도 평타보다 약해지지는 않습니다.</summary>
        public int Apply(int damage)
            => Mathf.Max(damage, Mathf.RoundToInt(damage * Mathf.Max(100f, DamagePercent) / 100f));
    }

    /// <summary>
    /// 전투력 산정 규칙입니다. 랭킹에 올라가는 점수는 반드시 여기를 거칩니다.
    ///
    /// 계산 순서
    /// 1) 레벨만으로 정해지는 기본 스탯을 깝니다(StatCatalog 권장 기본값 + 레벨 보정, 체력·마나는 실제 값).
    /// 2) 착용 중인 장비의 <see cref="StatModifier"/>를 얹습니다(Flat → 합연산% → 곱연산%).
    /// 3) 공격·생존·지원 세 지수를 만들어 가중치로 더하고, 레벨 보너스를 얹어 반올림합니다.
    ///
    /// 스탯의 기본값과 상·하한은 <see cref="StatCatalog.RecommendedDefinitions"/>를 그대로 씁니다.
    /// 치명타 확률 100%, 저항 75%, 쿨감 60% 같은 상한이 여기에 이미 들어 있어 따로 적지 않습니다.
    /// 밸런스는 아래 가중치 상수만 만지면 됩니다.
    ///
    /// 규칙을 바꾸면 <see cref="Version"/>을 올리세요. 랭킹 문서에 함께 기록되어
    /// 옛 규칙으로 찍힌 점수와 섞이지 않게 구분할 수 있습니다.
    /// </summary>
    public static class CombatPower
    {
        /// <summary>산정 규칙 판 번호입니다. 규칙을 바꾸면 올립니다.</summary>
        public const int Version = 1;

        // ------------------------------------------------------------------
        // 레벨 보정 — 장비가 하나도 없어도 레벨만으로 전투력이 오르게 합니다.
        // 체력·마나는 PlayerDataManager가 레벨업 때 이미 올려 두므로 여기서 건드리지 않습니다.
        // ------------------------------------------------------------------
        private const float AttackPowerPerLevel = 5f;
        private const float DefensePerLevel = 2f;
        private const float LevelBonusPerLevel = 100f;

        // ------------------------------------------------------------------
        // 지수 → 전투력 환산 가중치
        // ------------------------------------------------------------------

        /// <summary>공격 지수 1당 전투력입니다. 무기가 전투력을 가장 크게 움직이는 이유입니다.</summary>
        private const float OffenseWeight = 60f;

        /// <summary>생존 지수(≈유효 체력) 1당 전투력입니다.</summary>
        private const float SurvivalWeight = 0.2f;

        /// <summary>지원 지수 — 스탯 1포인트(%)당 전투력입니다.</summary>
        private const float ManaWeight = 0.1f;
        private const float ManaRegenerationWeight = 30f;
        private const float CooldownReductionWeight = 25f;
        private const float ResourceCostReductionWeight = 10f;
        private const float MovementSpeedWeight = 10f;
        private const float LifeStealWeight = 40f;
        private const float MagicFindWeight = 5f;

        /// <summary>생존 지수에서 초당 체력 재생 1을 체력 몇 점으로 볼지입니다.</summary>
        private const float HealthRegenerationWeight = 50f;

        /// <summary>방어 관통·저항은 곱연산으로 들어가므로 체감이 과하지 않게 절반만 반영합니다.</summary>
        private const float HalfEffectDivisor = 200f;

        private static Dictionary<StatType, StatDefinition> definitions;

        /// <summary>스탯 정의표입니다. 에셋을 읽지 않아 어느 씬에서든 같은 값이 나옵니다.</summary>
        private static Dictionary<StatType, StatDefinition> Definitions
        {
            get
            {
                if (definitions != null) return definitions;

                definitions = new Dictionary<StatType, StatDefinition>();
                foreach (var definition in StatCatalog.RecommendedDefinitions())
                    definitions[definition.type] = definition;
                return definitions;
            }
        }

        /// <summary>
        /// 지금 플레이어의 전투력입니다.
        /// <paramref name="catalog"/>가 null이면 장비를 빼고 레벨만으로 계산합니다.
        /// </summary>
        public static CombatPowerResult Evaluate(PlayerDataManager data, ItemCatalog catalog)
        {
            if (data == null) return default;
            return Score(BuildStats(data, catalog), data.Level);
        }

        /// <summary>
        /// 지금 장비까지 반영한 치명타 확률·피해입니다. 전투력과 같은 계산을 쓰므로
        /// 장비창에 보이는 수치와 실제로 터지는 치명타가 어긋나지 않습니다.
        /// </summary>
        public static CriticalProfile GetCritical(PlayerDataManager data, ItemCatalog catalog)
        {
            if (data == null)
                return new CriticalProfile(
                    DefaultOf(StatType.CriticalChance),
                    DefaultOf(StatType.CriticalDamage));

            var stats = BuildStats(data, catalog);
            return new CriticalProfile(
                Get(stats, StatType.CriticalChance),
                Get(stats, StatType.CriticalDamage));
        }

        /// <summary>
        /// 지금 장비까지 반영한 초당 마나 재생량입니다.
        ///
        /// 스탯표에는 예전부터 "마나 재생"이 있었지만 실제로 회복시키는 곳이 없어,
        /// 전투가 길어지면 마나가 바닥나 스킬이 아예 나가지 않았습니다.
        /// 전투 중 이 값으로 매 프레임 조금씩 되돌려 줍니다.
        /// </summary>
        public static float GetManaRegeneration(PlayerDataManager data, ItemCatalog catalog)
        {
            if (data == null) return DefaultOf(StatType.ManaRegeneration);
            return Get(BuildStats(data, catalog), StatType.ManaRegeneration);
        }

        /// <summary>장비를 하나 더 끼웠을 때의 전투력입니다. 비교 화살표(▲/▼) 표시에 씁니다.</summary>
        public static CombatPowerResult EvaluateWith(PlayerDataManager data, ItemCatalog catalog, ItemData extra)
        {
            if (data == null) return default;

            var stats = BuildStats(data, catalog);
            if (extra != null && extra.StatModifiers != null)
                foreach (var modifier in extra.StatModifiers)
                    stats.Add(Normalize(modifier));

            return Score(stats, data.Level);
        }

        // ------------------------------------------------------------------
        // 1~2단계 — 기본 스탯 + 장비
        // ------------------------------------------------------------------

        private static StatValueCollection BuildStats(PlayerDataManager data, ItemCatalog catalog)
        {
            var stats = new StatValueCollection();
            foreach (var definition in Definitions.Values)
                stats.SetBase(definition.type, definition.defaultValue);

            var levelSteps = Mathf.Max(0, data.Level - 1);
            stats.SetBase(StatType.AttackPower, DefaultOf(StatType.AttackPower) + AttackPowerPerLevel * levelSteps);
            stats.SetBase(StatType.Defense, DefaultOf(StatType.Defense) + DefensePerLevel * levelSteps);

            // 체력·마나만은 정의표 기본값이 아니라 실제 보유 수치를 씁니다.
            // 씬에서 정한 최대치와 레벨업 증가분이 모두 여기에 이미 반영되어 있습니다.
            stats.SetBase(StatType.MaxHealth, data.MaxHealth);
            stats.SetBase(StatType.MaxMana, data.MaxMana);

            if (catalog != null)
                foreach (var itemId in data.EquippedItems)
                {
                    if (!catalog.TryGet(itemId, out var item) || item.StatModifiers == null) continue;
                    foreach (var modifier in item.StatModifiers) stats.Add(Normalize(modifier));
                }

            return stats;
        }

        /// <summary>
        /// 장비 옵션 하나를 전투력 계산에 맞게 손봅니다.
        ///
        /// 공격 속도·치명타 확률처럼 값 자체가 %인 스탯은 기본값이 0이거나 아주 작습니다.
        /// 여기에 "가진 값의 +5%"(<see cref="StatModifierMode.AdditivePercent"/>)를 걸면
        /// 0 × 1.05 = 0 이라 아무 일도 일어나지 않습니다. 장비가 뜻한 바는 "+5%p"이므로
        /// 전투력에서는 더하기로 바꿔 셈합니다.
        ///
        /// 공격력·체력처럼 % 단위가 아닌 스탯은 원래 뜻대로 비율 증가로 둡니다.
        /// </summary>
        private static StatModifier Normalize(StatModifier modifier)
        {
            if (modifier.mode != StatModifierMode.AdditivePercent) return modifier;
            if (!Definitions.TryGetValue(modifier.type, out var definition) || !definition.appendPercent)
                return modifier;

            return new StatModifier(modifier.type, StatModifierMode.Flat, modifier.value);
        }

        // ------------------------------------------------------------------
        // 3단계 — 세 지수와 최종 점수
        // ------------------------------------------------------------------

        private static CombatPowerResult Score(StatValueCollection stats, int level)
        {
            // 치명타 기대 배수: 확률 50% · 피해 200%면 1 + 0.5 × 1.0 = 1.5배.
            var criticalMultiplier = 1f
                + Get(stats, StatType.CriticalChance) / 100f
                * Mathf.Max(0f, Get(stats, StatType.CriticalDamage) - 100f) / 100f;

            var offense = Get(stats, StatType.AttackPower)
                          * criticalMultiplier
                          * (1f + Get(stats, StatType.AttackSpeed) / 100f)
                          * (1f + Get(stats, StatType.SkillDamage) / 100f)
                          * (1f + Get(stats, StatType.ArmorPenetration) / HalfEffectDivisor);

            var averageResistance = (Get(stats, StatType.FireResistance)
                                     + Get(stats, StatType.ColdResistance)
                                     + Get(stats, StatType.LightningResistance)
                                     + Get(stats, StatType.ChaosResistance)) / 4f;

            var survival = Get(stats, StatType.MaxHealth)
                           * (1f + Get(stats, StatType.Defense) / 100f)
                           * (1f + averageResistance / HalfEffectDivisor)
                           + Get(stats, StatType.HealthRegeneration) * HealthRegenerationWeight;

            var support = Get(stats, StatType.MaxMana) * ManaWeight
                          + Get(stats, StatType.ManaRegeneration) * ManaRegenerationWeight
                          + Get(stats, StatType.CooldownReduction) * CooldownReductionWeight
                          + Get(stats, StatType.ResourceCostReduction) * ResourceCostReductionWeight
                          + Get(stats, StatType.MovementSpeed) * MovementSpeedWeight
                          + Get(stats, StatType.LifeSteal) * LifeStealWeight
                          + Get(stats, StatType.MagicFind) * MagicFindWeight;

            var offenseScore = offense * OffenseWeight;
            var survivalScore = survival * SurvivalWeight;
            var levelBonus = Mathf.Max(1, level) * LevelBonusPerLevel;

            // 이동 속도처럼 음수가 될 수 있는 스탯이 섞여 있어 총합이 0 아래로 내려가지 않게 막습니다.
            var total = (long)Math.Round(
                Math.Max(0d, (double)offenseScore + survivalScore + support + levelBonus),
                MidpointRounding.AwayFromZero);

            return new CombatPowerResult(total, offenseScore, survivalScore, support, levelBonus);
        }

        private static float Get(StatValueCollection stats, StatType type)
            => stats.Get(type, Definitions.TryGetValue(type, out var definition) ? definition : null);

        private static float DefaultOf(StatType type)
            => Definitions.TryGetValue(type, out var definition) ? definition.defaultValue : 0f;
    }
}
