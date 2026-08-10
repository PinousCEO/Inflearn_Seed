using IdleBattle;
using UnityEditor;
using UnityEngine;

namespace IdleBattleEditor
{
    /// <summary>
    /// 캐릭터 데이터 인스펙터입니다. 이 캐릭터의 스킬 세트(5구간 × 4스킬)를
    /// 여기서 바로 만들고 스킬 보드로 넘어갈 수 있게 버튼을 붙입니다.
    /// </summary>
    [CustomEditor(typeof(CharacterData))]
    [CanEditMultipleObjects]
    public sealed class CharacterDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1)
            {
                DrawMultiSelection();
                return;
            }

            var character = (CharacterData)target;
            EditorGUILayout.Space(10f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("스킬 세트 (5구간 × 4스킬)", EditorStyles.boldLabel);

                var skillSet = character.SkillSet;
                if (skillSet == null)
                {
                    // 캐릭터 데이터에는 아직 없지만 예전에 만든 세트가 굴러다닐 수 있어 한 번 찾아 준다.
                    var found = CharacterSkillSetFactory.FindSkillSet(character);
                    if (found != null)
                    {
                        EditorGUILayout.HelpBox(
                            $"연결되지 않은 스킬 세트 ‘{found.name}’를 찾았습니다.",
                            MessageType.Warning);
                        if (GUILayout.Button("이 스킬 세트 연결", GUILayout.Height(24f)))
                        {
                            CharacterSkillSetFactory.Link(character, found);
                            AssetDatabase.SaveAssets();
                        }
                        return;
                    }

                    EditorGUILayout.HelpBox(
                        "스킬 세트가 없습니다. 만들면 5구간 × 4선택지 슬롯이 함께 생깁니다.",
                        MessageType.Info);

                    if (GUILayout.Button("스킬 세트 만들기", GUILayout.Height(26f)))
                        CreateSkillSet(character, false);

                    if (GUILayout.Button(
                            $"스킬 세트 + 스킬 {CharacterSkillSetFactory.TotalSlotCount}개 만들기",
                            GUILayout.Height(26f)))
                        CreateSkillSet(character, true);
                    return;
                }

                var filled = CharacterSkillSetFactory.CountFilledSlots(skillSet);
                var total = CharacterSkillSetFactory.TotalSlotCount;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{skillSet.name}", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"스킬 {filled} / {total}", EditorStyles.miniLabel);
                }

                var bar = GUILayoutUtility.GetRect(1f, 6f);
                EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.35f));
                EditorGUI.DrawRect(
                    new Rect(bar.x, bar.y, bar.width * filled / total, bar.height),
                    new Color(0.35f, 0.72f, 0.45f, 0.9f));
                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("스킬 보드 열기", GUILayout.Height(24f)))
                        SkillDataManagerWindow.Open(skillSet);

                    using (new EditorGUI.DisabledScope(filled >= total))
                    {
                        if (GUILayout.Button(
                                $"빈 슬롯 {total - filled}개 스킬 생성",
                                GUILayout.Height(24f)))
                            FillSlots(skillSet);
                    }

                    if (GUILayout.Button("세트 에셋 찾기", GUILayout.Width(96f), GUILayout.Height(24f)))
                    {
                        Selection.activeObject = skillSet;
                        EditorGUIUtility.PingObject(skillSet);
                    }
                }

                if (skillSet.Character != character)
                {
                    EditorGUILayout.HelpBox(
                        "스킬 세트가 이 캐릭터를 가리키지 않습니다.",
                        MessageType.Warning);
                    if (GUILayout.Button("양방향 연결 고치기"))
                    {
                        CharacterSkillSetFactory.Link(character, skillSet);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            DrawResourceChecklist(character);
        }

        private void DrawMultiSelection()
        {
            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(
                    $"캐릭터 {targets.Length}명 선택됨",
                    EditorStyles.boldLabel);

                if (GUILayout.Button("선택한 캐릭터 전부 스킬 세트 만들기", GUILayout.Height(26f)))
                {
                    foreach (var item in targets)
                        if (item is CharacterData character)
                            CharacterSkillSetFactory.EnsureSkillSet(character);
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button(
                        $"선택한 캐릭터 전부 스킬 {CharacterSkillSetFactory.TotalSlotCount}개까지 채우기",
                        GUILayout.Height(26f)))
                {
                    var created = 0;
                    foreach (var item in targets)
                        if (item is CharacterData character)
                            created += CharacterSkillSetFactory.FillEmptySlots(
                                CharacterSkillSetFactory.EnsureSkillSet(character));
                    AssetDatabase.SaveAssets();
                    Debug.Log($"스킬 {created}개를 새로 만들었습니다.");
                }
            }
        }

        /// <summary>선택 화면이 실제로 쓰는 연출 리소스가 비었는지 한눈에 보여 준다.</summary>
        private static void DrawResourceChecklist(CharacterData character)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("연출 리소스", EditorStyles.boldLabel);
                Row("아이콘", "선택 버튼 썸네일", character.Icon != null);
                Row("초상화", "정보 패널 큰 그림", character.Portrait != null);
                Row("프리팹", "전투에 소환할 모델", character.CharacterPrefab != null);
                GUILayout.Label(
                    "대표 색상은 버튼 강조와 빈 스킬 슬롯 색으로 쓰입니다.",
                    EditorStyles.miniLabel);
            }
        }

        private static void Row(string label, string usage, bool filled)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(filled ? "●" : "○", GUILayout.Width(14f));
                GUILayout.Label(label, EditorStyles.miniBoldLabel, GUILayout.Width(52f));
                GUILayout.Label(
                    filled ? usage : $"{usage} · 비어 있음",
                    EditorStyles.miniLabel);
            }
        }

        private static void CreateSkillSet(CharacterData character, bool fillSkills)
        {
            var skillSet = CharacterSkillSetFactory.EnsureSkillSet(character);
            if (skillSet == null) return;

            if (fillSkills)
            {
                var created = CharacterSkillSetFactory.FillEmptySlots(skillSet);
                Debug.Log($"{character.DisplayName} 스킬 세트 생성 · 스킬 {created}개를 만들었습니다.");
            }

            SkillDataManagerWindow.Open(skillSet);
        }

        private static void FillSlots(CharacterSkillSetData skillSet)
        {
            var created = CharacterSkillSetFactory.FillEmptySlots(skillSet);
            Debug.Log($"{skillSet.DisplayName} · 스킬 {created}개를 새로 만들었습니다.");
        }
    }
}
