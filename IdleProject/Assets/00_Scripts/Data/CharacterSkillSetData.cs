using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleBattle
{
    [Serializable]
    public sealed class SkillSection
    {
        public const int SkillCount = 4;

        [SerializeField] private string sectionName;
        [SerializeField, Min(1)] private int unlockLevel = 1;
        [SerializeField] private SkillData[] skills = new SkillData[SkillCount];

        public string SectionName => sectionName;
        public int UnlockLevel => unlockLevel;
        public IReadOnlyList<SkillData> Skills => skills;

        public SkillData GetSkill(int index)
        {
            if (index < 0 || index >= SkillCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            return skills[index];
        }

        internal void Initialize(int index)
        {
            sectionName = $"{index + 1}구간";
            unlockLevel = index + 1;
            EnsureSkillCount();
        }

        internal void Validate(int index)
        {
            sectionName = string.IsNullOrWhiteSpace(sectionName)
                ? $"{index + 1}구간"
                : sectionName.Trim();
            unlockLevel = Mathf.Max(1, unlockLevel);
            EnsureSkillCount();
        }

        private void EnsureSkillCount()
        {
            if (skills != null && skills.Length == SkillCount)
                return;

            var resized = new SkillData[SkillCount];
            if (skills != null)
                Array.Copy(skills, resized, Mathf.Min(skills.Length, resized.Length));
            skills = resized;
        }
    }

    [CreateAssetMenu(
        fileName = "CharacterSkillSetData",
        menuName = "Idle Battle/Character Skill Set Data")]
    public sealed class CharacterSkillSetData : ScriptableObject
    {
        public const int SectionCount = 5;

        [SerializeField] private string characterId;
        [SerializeField] private string displayName = "새 캐릭터";
        [SerializeField] private Sprite characterIcon;
        [SerializeField] private GameObject characterPrefab;
        [SerializeField] private SkillSection[] sections = new SkillSection[SectionCount];

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public Sprite CharacterIcon => characterIcon;
        public GameObject CharacterPrefab => characterPrefab;
        public IReadOnlyList<SkillSection> Sections => sections;

        public SkillSection GetSection(int index)
        {
            if (index < 0 || index >= SectionCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            return sections[index];
        }

        public void Initialize(string id, string initialName)
        {
            characterId = id;
            displayName = initialName;
            characterIcon = null;
            characterPrefab = null;
            sections = new SkillSection[SectionCount];
            EnsureLayout();
        }

        private void OnEnable()
        {
            EnsureLayout();
        }

        private void OnValidate()
        {
            characterId = characterId?.Trim() ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
            EnsureLayout();
        }

        private void EnsureLayout()
        {
            if (sections == null || sections.Length != SectionCount)
            {
                var resized = new SkillSection[SectionCount];
                if (sections != null)
                    Array.Copy(sections, resized, Mathf.Min(sections.Length, resized.Length));
                sections = resized;
            }

            for (var i = 0; i < sections.Length; i++)
            {
                if (sections[i] == null)
                {
                    sections[i] = new SkillSection();
                    sections[i].Initialize(i);
                }
                else
                {
                    sections[i].Validate(i);
                }
            }
        }
    }
}
