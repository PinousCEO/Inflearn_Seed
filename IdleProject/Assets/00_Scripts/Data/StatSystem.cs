using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleBattle
{
    public enum StatCategory { Core, Offense, Defense, Resource, Utility }

    public enum StatType
    {
        AttackPower, Defense, MaxHealth, MaxMana,
        CriticalChance, CriticalDamage, AttackSpeed, CooldownReduction,
        SkillDamage, ArmorPenetration,
        FireResistance, ColdResistance, LightningResistance, ChaosResistance,
        HealthRegeneration, ManaRegeneration, ResourceCostReduction,
        MovementSpeed, MagicFind, LifeSteal
    }

    public enum StatModifierMode { Flat, AdditivePercent, MultiplicativePercent }

    [Serializable]
    public sealed class StatDefinition
    {
        public StatType type;
        public string displayName;
        public StatCategory category;
        [TextArea(2, 4)] public string description;
        public string valueFormat = "N0";
        public bool appendPercent;
        public float defaultValue;
        public float minimumValue;
        public float maximumValue = 999999f;
        public bool showInEquipmentSummary = true;
    }

    [Serializable]
    public struct StatValue
    {
        public StatType type;
        public float value;

        public StatValue(StatType type, float value)
        {
            this.type = type;
            this.value = value;
        }
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType type;
        public StatModifierMode mode;
        public float value;

        public StatModifier(StatType type, StatModifierMode mode, float value)
        {
            this.type = type;
            this.mode = mode;
            this.value = value;
        }
    }

    [CreateAssetMenu(fileName = "StatCatalog", menuName = "Idle Battle/Stats/Stat Catalog")]
    public sealed class StatCatalog : ScriptableObject
    {
        [SerializeField] private List<StatDefinition> definitions = new();
        public IReadOnlyList<StatDefinition> Definitions => definitions;

        public StatDefinition Find(StatType type) => definitions.Find(x => x.type == type);

        public void ResetToRecommendedDefaults()
        {
            definitions = RecommendedDefinitions();
        }

        public static List<StatDefinition> RecommendedDefinitions() => new()
        {
            Def(StatType.AttackPower, "공격력", StatCategory.Core, "기본 공격과 공격력 계수를 사용하는 스킬의 기준 피해.", "N0", false, 10, 0, true),
            Def(StatType.Defense, "방어력", StatCategory.Core, "물리 피해 감소 계산에 사용.", "N0", false, 0, 0, true),
            Def(StatType.MaxHealth, "최대 체력", StatCategory.Core, "생존 가능한 최대 체력.", "N0", false, 100, 1, true),
            Def(StatType.MaxMana, "최대 마나", StatCategory.Core, "스킬 사용에 쓰는 최대 자원.", "N0", false, 100, 0, true),
            Def(StatType.CriticalChance, "치명타 확률", StatCategory.Offense, "공격과 스킬이 치명타가 될 확률.", "0.#", true, 5, 0, true, 100),
            Def(StatType.CriticalDamage, "치명타 피해", StatCategory.Offense, "치명타 발생 시 적용되는 총 피해 배율.", "0.#", true, 150, 100, true),
            Def(StatType.AttackSpeed, "공격 속도", StatCategory.Offense, "기본 공격 동작 속도 증가율.", "0.#", true, 0, -80, true),
            Def(StatType.CooldownReduction, "재사용 대기시간 감소", StatCategory.Offense, "스킬 재사용 대기시간 감소율. 권장 상한 60%.", "0.#", true, 0, 0, true, 60),
            Def(StatType.SkillDamage, "스킬 피해", StatCategory.Offense, "모든 스킬 피해 증가율.", "0.#", true, 0, -100, false),
            Def(StatType.ArmorPenetration, "방어 관통", StatCategory.Offense, "적의 물리 방어력을 무시하는 비율.", "0.#", true, 0, 0, false, 80),
            Def(StatType.FireResistance, "화염 저항", StatCategory.Defense, "받는 화염 피해 감소. 권장 상한 75%.", "0.#", true, 0, -100, false, 75),
            Def(StatType.ColdResistance, "냉기 저항", StatCategory.Defense, "받는 냉기 피해 감소. 권장 상한 75%.", "0.#", true, 0, -100, false, 75),
            Def(StatType.LightningResistance, "번개 저항", StatCategory.Defense, "받는 번개 피해 감소. 권장 상한 75%.", "0.#", true, 0, -100, false, 75),
            Def(StatType.ChaosResistance, "혼돈 저항", StatCategory.Defense, "독·저주·혼돈 계열 피해 감소. 권장 상한 75%.", "0.#", true, 0, -100, false, 75),
            Def(StatType.HealthRegeneration, "체력 재생", StatCategory.Resource, "초당 회복하는 체력.", "0.#", false, 0, 0, false),
            // 마나는 스킬을 쓸수록 계속 줄어드는 것이 정상이고(초당 33 남짓 소모),
            // 되채우는 일은 3초마다 자동으로 먹는 마나 포션이 맡습니다(MainBattleUI).
            // 그래서 기본 재생은 거의 없는 것이나 마찬가지로 두고, 장비의 "마나 재생"만 여기에 더해집니다.
            Def(StatType.ManaRegeneration, "마나 재생", StatCategory.Resource, "초당 회복하는 마나.", "0.#", false, 1, 0, false),
            Def(StatType.ResourceCostReduction, "자원 소모 감소", StatCategory.Resource, "스킬 마나 소모 감소율.", "0.#", true, 0, 0, false, 60),
            Def(StatType.MovementSpeed, "이동 속도", StatCategory.Utility, "전투 중 이동 속도 증가율.", "0.#", true, 0, -80, false),
            Def(StatType.MagicFind, "아이템 발견", StatCategory.Utility, "희귀 등급 장비가 등장할 상대 가중치를 증가시킴.", "0.#", true, 0, 0, true),
            Def(StatType.LifeSteal, "생명력 흡수", StatCategory.Utility, "가한 피해 일부를 체력으로 회복.", "0.#", true, 0, 0, false, 20)
        };

        private static StatDefinition Def(StatType type, string name, StatCategory category,
            string description, string format, bool percent, float defaultValue, float min,
            bool summary, float max = 999999f) => new()
        {
            type = type, displayName = name, category = category, description = description,
            valueFormat = format, appendPercent = percent, defaultValue = defaultValue,
            minimumValue = min, maximumValue = max, showInEquipmentSummary = summary
        };

        private void OnValidate()
        {
            var used = new HashSet<StatType>();
            definitions.RemoveAll(x => x == null || !used.Add(x.type));
            foreach (var definition in definitions)
                definition.maximumValue = Mathf.Max(definition.minimumValue, definition.maximumValue);
        }
    }

    public sealed class StatValueCollection
    {
        private readonly Dictionary<StatType, float> baseValues = new();
        private readonly List<StatModifier> modifiers = new();

        public void SetBase(StatType type, float value) => baseValues[type] = value;
        public void Add(StatModifier modifier) => modifiers.Add(modifier);
        public void ClearModifiers() => modifiers.Clear();

        public float Get(StatType type, StatDefinition definition = null)
        {
            var value = baseValues.TryGetValue(type, out var baseValue)
                ? baseValue : definition?.defaultValue ?? 0f;
            var flat = 0f;
            var additive = 0f;
            var multiplier = 1f;
            foreach (var modifier in modifiers)
            {
                if (modifier.type != type) continue;
                switch (modifier.mode)
                {
                    case StatModifierMode.Flat: flat += modifier.value; break;
                    case StatModifierMode.AdditivePercent: additive += modifier.value; break;
                    case StatModifierMode.MultiplicativePercent: multiplier *= 1f + modifier.value / 100f; break;
                }
            }
            value = (value + flat) * (1f + additive / 100f) * multiplier;
            return definition == null ? value : Mathf.Clamp(value, definition.minimumValue, definition.maximumValue);
        }
    }
}
