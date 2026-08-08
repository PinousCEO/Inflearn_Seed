using System.Collections.Generic;
using System.Linq;
using IdleBattle;
using UnityEditor;
using UnityEngine;

namespace IdleBattleEditor
{
    /// <summary>
    /// 캐릭터 데이터를 기준으로 스킬 세트(5구간 × 4스킬 = 20개)와 스킬 에셋을 만들어 주는 공용 도구입니다.
    /// 스킬 관리 창과 캐릭터 데이터 인스펙터가 같은 규칙으로 만들도록 생성 로직을 여기 모아 둡니다.
    /// </summary>
    public static class CharacterSkillSetFactory
    {
        public const string SkillSetFolder = "Assets/00_Data/CharacterSkills";
        public const string SkillFolder = "Assets/00_Data/Skills";

        public const int TotalSlotCount =
            CharacterSkillSetData.SectionCount * SkillSection.SkillCount;

        /// <summary>구간별 기본 해금 레벨입니다. 선택 화면 안내 문구와 같은 값을 씁니다.</summary>
        private static readonly int[] DefaultUnlockLevels = { 1, 5, 10, 15, 20 };

        // ------------------------------------------------------------------
        // 스킬 세트
        // ------------------------------------------------------------------

        /// <summary>이 캐릭터의 스킬 세트를 찾습니다. 캐릭터 데이터의 참조 → 역참조 → ID 순으로 봅니다.</summary>
        public static CharacterSkillSetData FindSkillSet(CharacterData character)
        {
            if (character == null) return null;
            if (character.SkillSet != null) return character.SkillSet;

            var candidates = LoadAll<CharacterSkillSetData>();
            var linked = candidates.FirstOrDefault(set => set.Character == character);
            if (linked != null) return linked;

            // 캐릭터 데이터가 없던 시절에 만든 세트라면 ID로 주인을 찾아 준다.
            if (string.IsNullOrWhiteSpace(character.CharacterId)) return null;
            return candidates.FirstOrDefault(set =>
                set.Character == null &&
                string.Equals(set.CharacterId, character.CharacterId, System.StringComparison.Ordinal));
        }

        /// <summary>스킬 세트를 찾거나 새로 만들고, 캐릭터 데이터와 양방향으로 연결합니다.</summary>
        public static CharacterSkillSetData EnsureSkillSet(CharacterData character)
        {
            if (character == null) return null;

            var skillSet = FindSkillSet(character);
            if (skillSet == null)
            {
                EnsureFolder(SkillSetFolder);
                skillSet = ScriptableObject.CreateInstance<CharacterSkillSetData>();
                skillSet.Initialize(
                    character.CharacterId,
                    character.DisplayName,
                    character);

                var fileName = string.IsNullOrWhiteSpace(character.CharacterId)
                    ? character.name
                    : character.CharacterId;
                var path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{SkillSetFolder}/{fileName}-skills.asset");
                AssetDatabase.CreateAsset(skillSet, path);
                StampSectionDefaults(skillSet);
            }

            Link(character, skillSet);
            AssetDatabase.SaveAssets();
            return skillSet;
        }

        /// <summary>캐릭터 데이터와 스킬 세트가 서로를 가리키게 맞춥니다.</summary>
        public static void Link(CharacterData character, CharacterSkillSetData skillSet)
        {
            if (character == null || skillSet == null) return;

            if (character.SkillSet != skillSet)
            {
                Undo.RecordObject(character, "스킬 세트 연결");
                character.SetSkillSet(skillSet);
                EditorUtility.SetDirty(character);
            }

            if (skillSet.Character != character)
            {
                Undo.RecordObject(skillSet, "스킬 세트 연결");
                skillSet.SetCharacter(character);
                EditorUtility.SetDirty(skillSet);
            }
        }

        // ------------------------------------------------------------------
        // 스킬 20개
        // ------------------------------------------------------------------

        /// <summary>
        /// 비어 있는 슬롯마다 스킬 에셋을 만들어 꽂습니다. 이미 채워진 슬롯은 그대로 둡니다.
        /// 스킬은 <c>Assets/00_Data/Skills/{캐릭터ID}</c> 아래에 캐릭터별로 나눠 저장합니다.
        /// </summary>
        /// <param name="skillSet">채울 스킬 세트입니다.</param>
        /// <param name="onlySection">한 구간만 채우려면 그 구간 번호(0~4), 전체면 -1입니다.</param>
        /// <returns>새로 만든 스킬 개수입니다.</returns>
        public static int FillEmptySlots(CharacterSkillSetData skillSet, int onlySection = -1)
        {
            if (skillSet == null) return 0;

            var owner = skillSet.Character;
            var idBase = ResolveIdBase(skillSet);
            var folder = $"{SkillFolder}/{idBase}";
            var created = 0;

            var serialized = new SerializedObject(skillSet);
            var sections = serialized.FindProperty("sections");

            // 최대 20개라 StartAssetEditing으로 묶지 않는다.
            // 배치 중에는 방금 만든 에셋이 아직 임포트되지 않아 참조로 꽂을 때 어긋날 수 있다.
            EnsureFolder(folder);

            for (var sectionIndex = 0;
                 sectionIndex < CharacterSkillSetData.SectionCount;
                 sectionIndex++)
            {
                if (onlySection >= 0 && sectionIndex != onlySection) continue;

                var slots = sections
                    .GetArrayElementAtIndex(sectionIndex)
                    .FindPropertyRelative("skills");

                for (var slotIndex = 0; slotIndex < SkillSection.SkillCount; slotIndex++)
                {
                    var slot = slots.GetArrayElementAtIndex(slotIndex);
                    if (slot.objectReferenceValue != null) continue;

                    slot.objectReferenceValue = CreateSkill(
                        folder,
                        idBase,
                        skillSet.DisplayName,
                        owner,
                        sectionIndex,
                        slotIndex);
                    created++;
                }
            }

            if (created > 0)
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(skillSet);
                AssetDatabase.SaveAssets();
            }

            return created;
        }

        /// <summary>슬롯 하나에 스킬을 만들어 꽂습니다. 이미 스킬이 있으면 그것을 그대로 돌려줍니다.</summary>
        public static SkillData FillSlot(
            CharacterSkillSetData skillSet,
            int sectionIndex,
            int slotIndex)
        {
            if (skillSet == null ||
                sectionIndex < 0 || sectionIndex >= CharacterSkillSetData.SectionCount ||
                slotIndex < 0 || slotIndex >= SkillSection.SkillCount)
                return null;

            var serialized = new SerializedObject(skillSet);
            var slot = serialized
                .FindProperty("sections")
                .GetArrayElementAtIndex(sectionIndex)
                .FindPropertyRelative("skills")
                .GetArrayElementAtIndex(slotIndex);

            if (slot.objectReferenceValue is SkillData existing)
                return existing;

            var idBase = ResolveIdBase(skillSet);
            var folder = $"{SkillFolder}/{idBase}";
            EnsureFolder(folder);

            var skill = CreateSkill(
                folder,
                idBase,
                skillSet.DisplayName,
                skillSet.Character,
                sectionIndex,
                slotIndex);

            slot.objectReferenceValue = skill;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skillSet);
            AssetDatabase.SaveAssets();
            return skill;
        }

        /// <summary>이 스킬 세트에서 스킬이 꽂혀 있는 슬롯 수입니다. (최대 20)</summary>
        public static int CountFilledSlots(CharacterSkillSetData skillSet)
        {
            if (skillSet == null) return 0;

            var filled = 0;
            foreach (var section in skillSet.Sections)
            {
                if (section == null) continue;
                foreach (var skill in section.Skills)
                    if (skill != null) filled++;
            }
            return filled;
        }

        // ------------------------------------------------------------------
        // 일괄 생성 메뉴
        // ------------------------------------------------------------------

        [MenuItem("Tools/게임 데이터 관리/캐릭터 스킬셋 일괄 생성", priority = 111)]
        private static void CreateForAllCharacters()
        {
            var characters = CatalogOrderedCharacters();
            if (characters.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "캐릭터 스킬셋 일괄 생성",
                    "프로젝트에 캐릭터 데이터가 없습니다.",
                    "확인");
                return;
            }

            var fillSkills = EditorUtility.DisplayDialogComplex(
                "캐릭터 스킬셋 일괄 생성",
                $"캐릭터 {characters.Count}명에게 스킬 세트(5구간 × 4스킬)를 만듭니다.\n" +
                "빈 슬롯에 스킬 에셋까지 함께 만들까요?",
                $"스킬까지 생성 (최대 {characters.Count * TotalSlotCount}개)",
                "취소",
                "세트만 생성");
            if (fillSkills == 1) return;

            var createdSkills = 0;
            foreach (var character in characters)
            {
                var skillSet = EnsureSkillSet(character);
                if (fillSkills == 0)
                    createdSkills += FillEmptySlots(skillSet);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"캐릭터 {characters.Count}명의 스킬 세트를 준비했습니다. " +
                $"새로 만든 스킬 {createdSkills}개.");
        }

        /// <summary>카탈로그 순서를 우선하고, 카탈로그에 없는 캐릭터는 뒤에 붙입니다.</summary>
        public static List<CharacterData> CatalogOrderedCharacters()
        {
            var all = LoadAll<CharacterData>();
            var ordered = new List<CharacterData>();

            foreach (var catalog in LoadAll<CharacterCatalog>())
            {
                foreach (var character in catalog.Characters)
                    if (character != null && !ordered.Contains(character))
                        ordered.Add(character);
            }

            foreach (var character in all)
                if (!ordered.Contains(character))
                    ordered.Add(character);

            return ordered;
        }

        public static void EnsureFolder(string folderPath)
        {
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // ------------------------------------------------------------------
        // 내부 구현
        // ------------------------------------------------------------------

        private static SkillData CreateSkill(
            string folder,
            string idBase,
            string characterName,
            CharacterData owner,
            int sectionIndex,
            int slotIndex)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            var id = $"{idBase}-s{sectionIndex + 1}{slotIndex + 1}";
            var displayName = string.IsNullOrWhiteSpace(characterName)
                ? $"{sectionIndex + 1}-{slotIndex + 1} 스킬"
                : $"{characterName} {sectionIndex + 1}-{slotIndex + 1} 스킬";

            skill.Initialize(id, displayName, owner);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{id}.asset");
            AssetDatabase.CreateAsset(skill, path);
            return skill;
        }

        /// <summary>새로 만든 세트의 구간 이름과 해금 레벨을 기본값으로 찍어 둡니다.</summary>
        private static void StampSectionDefaults(CharacterSkillSetData skillSet)
        {
            var serialized = new SerializedObject(skillSet);
            var sections = serialized.FindProperty("sections");

            for (var i = 0; i < CharacterSkillSetData.SectionCount; i++)
            {
                var section = sections.GetArrayElementAtIndex(i);
                section.FindPropertyRelative("sectionName").stringValue = $"{i + 1}구간";
                section.FindPropertyRelative("unlockLevel").intValue =
                    i < DefaultUnlockLevels.Length ? DefaultUnlockLevels[i] : i + 1;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skillSet);
        }

        private static string ResolveIdBase(CharacterSkillSetData skillSet)
        {
            if (skillSet.Character != null &&
                !string.IsNullOrWhiteSpace(skillSet.Character.CharacterId))
                return skillSet.Character.CharacterId;
            if (!string.IsNullOrWhiteSpace(skillSet.CharacterId))
                return skillSet.CharacterId;
            return skillSet.name;
        }

        private static List<T> LoadAll<T>()
            where T : Object
        {
            var results = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    results.Add(asset);
            }
            return results;
        }
    }
}
