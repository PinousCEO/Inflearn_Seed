using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleBattle
{
    [Flags]
    public enum SkillType
    {
        None = 0,
        Summon = 1 << 0,
        Manifestation = 1 << 1,
        Projectile = 1 << 2,
        Shout = 1 << 3,
        Melee = 1 << 4,
        Area = 1 << 5,
        Buff = 1 << 6,
        Passive = 1 << 7,
        Custom = 1 << 8
    }

    public enum SkillTargetType
    {
        Self,
        SingleEnemy,
        MultipleEnemies,
        GroundPosition,
        Direction,
        Automatic
    }

    [Serializable]
    public sealed class SkillAbility
    {
        [SerializeField] private string key = "damage";
        [SerializeField] private string displayName = "피해량";
        [SerializeField] private float value;
        [SerializeField] private string unit;

        public string Key => key;
        public string DisplayName => displayName;
        public float Value => value;
        public string Unit => unit;

        internal void Validate()
        {
            key = key?.Trim() ?? string.Empty;
            displayName = displayName?.Trim() ?? string.Empty;
            unit = unit?.Trim() ?? string.Empty;
        }
    }

    [CreateAssetMenu(
        fileName = "SkillData",
        menuName = "Idle Battle/Skill Data")]
    public sealed class SkillData : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string skillId;
        [SerializeField] private string displayName = "새 스킬";
        [SerializeField, TextArea(3, 8)] private string description;
        [SerializeField] private Sprite icon;

        [Header("분류")]
        [SerializeField] private SkillType skillTypes = SkillType.Projectile;
        [Tooltip("'사용자 정의' 타입을 선택했을 때 사용할 분류명입니다.")]
        [SerializeField] private string customTypeName;
        [SerializeField] private SkillTargetType targetType = SkillTargetType.SingleEnemy;

        [Header("실행 설정")]
        [SerializeField, Min(0f)] private float cooldown;
        [SerializeField, Min(0f)] private float resourceCost;
        [SerializeField, Min(0f)] private float castTime;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField] private GameObject effectPrefab;

        [Header("스킬 능력")]
        [Tooltip("damage, radius, projectileCount처럼 런타임에서 사용할 키와 수치를 자유롭게 추가합니다.")]
        [SerializeField] private List<SkillAbility> abilities = new List<SkillAbility>();

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public SkillType Types => skillTypes;
        public string CustomTypeName => customTypeName;
        public SkillTargetType TargetType => targetType;
        public float Cooldown => cooldown;
        public float ResourceCost => resourceCost;
        public float CastTime => castTime;
        public float Duration => duration;
        public GameObject EffectPrefab => effectPrefab;
        public IReadOnlyList<SkillAbility> Abilities => abilities;

        public bool HasType(SkillType type)
        {
            return (skillTypes & type) != 0;
        }

        public bool TryGetAbility(string key, out float value)
        {
            foreach (var ability in abilities)
            {
                if (ability != null &&
                    string.Equals(ability.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = ability.Value;
                    return true;
                }
            }

            value = 0f;
            return false;
        }

        public void Initialize(string id, string initialName)
        {
            skillId = id;
            displayName = initialName;
            description = string.Empty;
            icon = null;
            skillTypes = SkillType.Projectile;
            customTypeName = string.Empty;
            targetType = SkillTargetType.SingleEnemy;
            cooldown = 0f;
            resourceCost = 0f;
            castTime = 0f;
            duration = 0f;
            effectPrefab = null;
            abilities = new List<SkillAbility>();
        }

        private void OnValidate()
        {
            skillId = skillId?.Trim() ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
            customTypeName = customTypeName?.Trim() ?? string.Empty;
            cooldown = Mathf.Max(0f, cooldown);
            resourceCost = Mathf.Max(0f, resourceCost);
            castTime = Mathf.Max(0f, castTime);
            duration = Mathf.Max(0f, duration);
            abilities ??= new List<SkillAbility>();

            foreach (var ability in abilities)
                ability?.Validate();
        }
    }
}
