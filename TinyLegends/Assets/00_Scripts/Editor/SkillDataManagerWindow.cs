using System;
using System.Collections.Generic;
using System.Linq;
using IdleBattle;
using UnityEditor;
using UnityEngine;

namespace IdleBattleEditor
{
    public sealed class SkillDataManagerWindow : EditorWindow
    {
        private const string CharacterFolder = CharacterSkillSetFactory.SkillSetFolder;
        private const float InspectorWidth = 370f;
        private const float CardHeight = 178f;

        private readonly List<CharacterSkillSetData> characters = new List<CharacterSkillSetData>();
        private readonly List<CharacterData> characterDataList = new List<CharacterData>();
        private string[] characterDataLabels = Array.Empty<string>();
        private Action pendingAction;

        private CharacterSkillSetData selectedCharacter;
        private SerializedObject characterObject;
        private SkillData selectedSkill;
        private SerializedObject skillObject;
        private Vector2 boardScroll;
        private Vector2 inspectorScroll;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardMetaStyle;
        private GUIStyle sectionTitleStyle;

        [MenuItem("Tools/게임 데이터 관리/스킬 관리", priority = 110)]
        public static void Open()
        {
            var window = GetWindow<SkillDataManagerWindow>();
            window.titleContent = new GUIContent("캐릭터 스킬 보드");
            window.minSize = new Vector2(1050f, 650f);
            window.Show();
        }

        /// <summary>특정 캐릭터의 스킬 세트를 펼친 상태로 창을 엽니다.</summary>
        public static void Open(CharacterSkillSetData skillSet)
        {
            Open();
            var window = GetWindow<SkillDataManagerWindow>();
            window.RefreshAssets();
            if (skillSet != null)
                window.SelectCharacter(skillSet);
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("캐릭터 스킬 보드");
            Undo.undoRedoPerformed += HandleUndoRedo;
            RefreshAssets();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void OnProjectChange()
        {
            RefreshAssets();
            Repaint();
        }

        private void HandleUndoRedo()
        {
            characterObject?.Update();
            skillObject?.Update();
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawCharacterHeader();

            if (selectedCharacter == null || characterObject == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    "위에서 캐릭터를 고르면 그 캐릭터의 5구간 × 4스킬 보드가 열립니다.",
                    EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSkillBoard();
                    DrawDivider();
                    DrawSkillInspector();
                }
            }

            RunPendingAction();
        }

        /// <summary>
        /// 에셋을 만들거나 선택을 바꾸는 작업은 이번 프레임의 레이아웃이 끝난 뒤에 실행한다.
        /// 그리는 중에 SerializedObject를 갈아 끼우면 레이아웃이 어긋난다.
        /// </summary>
        private void RunPendingAction()
        {
            if (pendingAction == null) return;

            var action = pendingAction;
            pendingAction = null;
            action();
            Repaint();
        }

        private void DrawCharacterHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawCharacterDataPicker();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("스킬 세트", EditorStyles.boldLabel, GUILayout.Width(58f));

                    var names = characters.Select(CharacterLabel).ToArray();
                    var currentIndex = selectedCharacter == null
                        ? -1
                        : characters.IndexOf(selectedCharacter);
                    var popupIndex = EditorGUILayout.Popup(
                        Mathf.Max(0, currentIndex),
                        names.Length == 0 ? new[] { "등록된 캐릭터 없음" } : names,
                        GUILayout.MinWidth(220f));

                    if (names.Length > 0 && popupIndex != currentIndex)
                        SelectCharacter(characters[popupIndex]);

                    if (GUILayout.Button("+ 빈 세트", GUILayout.Width(78f), GUILayout.Height(24f)))
                        CreateCharacter();

                    if (selectedCharacter != null)
                    {
                        GUILayout.Space(8f);
                        GUILayout.Label("ID", EditorStyles.miniLabel, GUILayout.Width(18f));
                        characterObject.UpdateIfRequiredOrScript();
                        EditorGUILayout.PropertyField(
                            characterObject.FindProperty("characterId"),
                            GUIContent.none,
                            GUILayout.Width(125f));
                        EditorGUILayout.PropertyField(
                            characterObject.FindProperty("displayName"),
                            GUIContent.none,
                            GUILayout.MinWidth(140f));
                        EditorGUILayout.PropertyField(
                            characterObject.FindProperty("characterIcon"),
                            GUIContent.none,
                            GUILayout.Width(130f));

                        if (characterObject.ApplyModifiedProperties())
                            EditorUtility.SetDirty(selectedCharacter);

                        if (GUILayout.Button("에셋 찾기", GUILayout.Width(72f)))
                        {
                            Selection.activeObject = selectedCharacter;
                            EditorGUIUtility.PingObject(selectedCharacter);
                        }
                    }
                }

                if (selectedCharacter != null)
                {
                    DrawCharacterDataRow();
                    GUILayout.Label(
                        "5개 구간 · 구간마다 4개 선택지 · 총 20개 스킬",
                        EditorStyles.miniLabel);
                }
            }
        }

        /// <summary>
        /// 캐릭터 데이터를 골라 그 캐릭터의 스킬 세트로 바로 넘어간다.
        /// 세트가 아직 없으면 여기서 만들 수 있게 한다.
        /// </summary>
        private void DrawCharacterDataPicker()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("캐릭터", EditorStyles.boldLabel, GUILayout.Width(58f));

                if (characterDataList.Count == 0)
                {
                    GUILayout.Label(
                        "캐릭터 데이터가 없습니다. Assets/00_Data/Characters에서 먼저 만드세요.",
                        EditorStyles.miniLabel);
                    return;
                }

                var currentCharacter = selectedCharacter != null ? selectedCharacter.Character : null;
                var currentIndex = currentCharacter != null
                    ? characterDataList.IndexOf(currentCharacter)
                    : -1;
                var nextIndex = EditorGUILayout.Popup(
                    currentIndex,
                    characterDataLabels,
                    GUILayout.MinWidth(240f));

                if (nextIndex >= 0 && nextIndex != currentIndex)
                {
                    var picked = characterDataList[nextIndex];
                    pendingAction = () => SelectByCharacterData(picked);
                }

                var target = currentCharacter ??
                             characterDataList[Mathf.Clamp(nextIndex, 0, characterDataList.Count - 1)];

                if (GUILayout.Button("스킬셋 준비", GUILayout.Width(84f), GUILayout.Height(24f)))
                    pendingAction = () => PrepareSkillSet(target, false);

                if (GUILayout.Button(
                        $"스킬 {CharacterSkillSetFactory.TotalSlotCount}개 채우기",
                        GUILayout.Width(112f),
                        GUILayout.Height(24f)))
                    pendingAction = () => PrepareSkillSet(target, true);

                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>캐릭터 데이터로 스킬 세트를 찾아 선택한다. 없으면 만들지 물어본다.</summary>
        private void SelectByCharacterData(CharacterData data)
        {
            if (data == null) return;

            var skillSet = CharacterSkillSetFactory.FindSkillSet(data);
            if (skillSet == null)
            {
                if (!EditorUtility.DisplayDialog(
                        "스킬 세트 없음",
                        $"‘{DisplayName(data.DisplayName)}’에는 스킬 세트가 없습니다.\n" +
                        "5구간 × 4스킬 세트를 지금 만들까요?",
                        "만들기",
                        "취소"))
                    return;

                PrepareSkillSet(data, false);
                return;
            }

            CharacterSkillSetFactory.Link(data, skillSet);
            SelectCharacter(skillSet);
        }

        /// <summary>이 캐릭터의 스킬 세트를 만들거나 찾아서 선택하고, 필요하면 스킬까지 채운다.</summary>
        private void PrepareSkillSet(CharacterData data, bool fillSkills)
        {
            if (data == null) return;

            var skillSet = CharacterSkillSetFactory.EnsureSkillSet(data);
            if (skillSet == null) return;

            if (fillSkills)
            {
                var created = CharacterSkillSetFactory.FillEmptySlots(skillSet);
                Debug.Log($"{DisplayName(data.DisplayName)} · 스킬 {created}개를 새로 만들었습니다.");
            }

            RefreshAssets();
            SelectCharacter(skillSet);
        }

        /// <summary>스킬 세트를 캐릭터 데이터에 묶고, 그 캐릭터의 설정값을 한눈에 보여준다.</summary>
        private void DrawCharacterDataRow()
        {
            characterObject.UpdateIfRequiredOrScript();
            var characterProperty = characterObject.FindProperty("character");
            if (characterProperty == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("캐릭터 데이터", EditorStyles.miniBoldLabel, GUILayout.Width(84f));
                EditorGUILayout.PropertyField(
                    characterProperty,
                    GUIContent.none,
                    GUILayout.MinWidth(200f));

                var data = characterProperty.objectReferenceValue as CharacterData;
                if (data != null)
                {
                    GUILayout.Label(
                        $"난이도 {data.Difficulty}/5 · 공{data.Attack} 생{data.Survival} 방{data.Defense} " +
                        $"기{data.Mobility} 치{data.Critical} 광{data.AreaPower}",
                        EditorStyles.miniLabel);

                    if (GUILayout.Button("소속 일괄 지정", GUILayout.Width(96f)))
                        AssignOwnerToAllSkills(data);
                }
                else
                {
                    GUILayout.Label(
                        "캐릭터 데이터를 연결하면 스킬에 소속이 자동으로 채워집니다.",
                        EditorStyles.miniLabel);
                }
            }

            if (characterObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(selectedCharacter);

            // 캐릭터 데이터 쪽에서도 이 스킬 세트를 가리키도록 반대 방향 참조를 맞춰 준다.
            var linked = characterObject.FindProperty("character").objectReferenceValue as CharacterData;
            if (linked != null && linked.SkillSet != selectedCharacter)
            {
                Undo.RecordObject(linked, "스킬 세트 연결");
                linked.SetSkillSet(selectedCharacter);
                EditorUtility.SetDirty(linked);
            }
        }

        /// <summary>이 스킬 세트에 들어 있는 모든 스킬의 소속 캐릭터를 한 번에 맞춘다.</summary>
        private void AssignOwnerToAllSkills(CharacterData data)
        {
            if (selectedCharacter == null) return;

            var changed = 0;
            foreach (var section in selectedCharacter.Sections)
            {
                if (section == null) continue;
                foreach (var skill in section.Skills)
                {
                    if (skill == null || skill.Owner == data) continue;
                    Undo.RecordObject(skill, "소속 캐릭터 지정");
                    skill.SetOwner(data);
                    EditorUtility.SetDirty(skill);
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{data.DisplayName} 소속으로 스킬 {changed}개를 지정했습니다.");
        }

        private void DrawSkillBoard()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var filled = CharacterSkillSetFactory.CountFilledSlots(selectedCharacter);
                    var total = CharacterSkillSetFactory.TotalSlotCount;
                    GUILayout.Label($"20 스킬 보드  ({filled}/{total})", sectionTitleStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("카드를 클릭하면 오른쪽에서 상세 편집", EditorStyles.miniLabel);
                    GUILayout.Space(8f);

                    using (new EditorGUI.DisabledScope(filled >= total))
                    {
                        if (GUILayout.Button(
                                $"빈 슬롯 {total - filled}개 한 번에 생성",
                                GUILayout.Width(150f)))
                            pendingAction = () => FillSlots(-1);
                    }
                    GUILayout.Space(8f);
                }

                characterObject.UpdateIfRequiredOrScript();
                var sections = characterObject.FindProperty("sections");
                boardScroll = EditorGUILayout.BeginScrollView(boardScroll);

                for (var sectionIndex = 0;
                     sectionIndex < CharacterSkillSetData.SectionCount;
                     sectionIndex++)
                {
                    DrawSection(sections.GetArrayElementAtIndex(sectionIndex), sectionIndex);
                    EditorGUILayout.Space(8f);
                }

                EditorGUILayout.EndScrollView();
                if (characterObject.ApplyModifiedProperties())
                    EditorUtility.SetDirty(selectedCharacter);
            }
        }

        private void DrawSection(SerializedProperty section, int sectionIndex)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{sectionIndex + 1}구간", sectionTitleStyle, GUILayout.Width(62f));
                    EditorGUILayout.PropertyField(
                        section.FindPropertyRelative("sectionName"),
                        GUIContent.none,
                        GUILayout.Width(150f));
                    GUILayout.Label("해금", EditorStyles.miniLabel, GUILayout.Width(28f));
                    EditorGUILayout.PropertyField(
                        section.FindPropertyRelative("unlockLevel"),
                        GUIContent.none,
                        GUILayout.Width(58f));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("4개 중 1개 선택", EditorStyles.miniLabel);

                    var index = sectionIndex;
                    if (GUILayout.Button("이 구간 4개 채우기", GUILayout.Width(122f)))
                        pendingAction = () => FillSlots(index);
                }

                var slots = section.FindPropertyRelative("skills");
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var slotIndex = 0; slotIndex < SkillSection.SkillCount; slotIndex++)
                    {
                        DrawSkillCard(
                            slots.GetArrayElementAtIndex(slotIndex),
                            sectionIndex,
                            slotIndex);

                        if (slotIndex < SkillSection.SkillCount - 1)
                            GUILayout.Space(5f);
                    }
                }
            }
        }

        private void DrawSkillCard(
            SerializedProperty slot,
            int sectionIndex,
            int slotIndex)
        {
            var cardRect = GUILayoutUtility.GetRect(
                130f,
                CardHeight,
                GUILayout.MinWidth(130f),
                GUILayout.ExpandWidth(true));
            var skill = slot.objectReferenceValue as SkillData;
            var isSelected = skill != null && skill == selectedSkill;

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(
                    cardRect,
                    isSelected
                        ? new Color(0.18f, 0.48f, 0.78f, 0.55f)
                        : new Color(0.12f, 0.12f, 0.12f, 0.45f));
            }

            var padding = 7f;
            var content = new Rect(
                cardRect.x + padding,
                cardRect.y + padding,
                cardRect.width - padding * 2f,
                cardRect.height - padding * 2f);
            EditorGUI.LabelField(
                new Rect(content.x, content.y, content.width, 18f),
                $"선택지 {slotIndex + 1}",
                EditorStyles.miniBoldLabel);

            var iconSize = Mathf.Min(66f, content.width);
            var iconRect = new Rect(
                content.center.x - iconSize * 0.5f,
                content.y + 21f,
                iconSize,
                66f);
            DrawIcon(skill != null ? skill.Icon : null, iconRect);

            if (skill == null)
            {
                EditorGUI.LabelField(
                    new Rect(content.x, iconRect.yMax + 6f, content.width, 18f),
                    "빈 스킬 슬롯",
                    cardMetaStyle);

                var createRect = new Rect(
                    content.x,
                    cardRect.yMax - 34f,
                    content.width,
                    25f);
                if (GUI.Button(createRect, "+ 새 스킬 생성"))
                {
                    var section = sectionIndex;
                    var slotNumber = slotIndex;
                    pendingAction = () => CreateSkillForSlot(section, slotNumber);
                }

                HandleSkillDrop(cardRect, slot);
                return;
            }

            EditorGUI.LabelField(
                new Rect(content.x, iconRect.yMax + 5f, content.width, 20f),
                DisplayName(skill.DisplayName),
                cardTitleStyle);
            EditorGUI.LabelField(
                new Rect(content.x, iconRect.yMax + 25f, content.width, 18f),
                SkillTypeLabel(skill),
                cardMetaStyle);

            var objectRect = new Rect(
                content.x,
                cardRect.yMax - 30f,
                content.width,
                20f);
            EditorGUI.BeginChangeCheck();
            var replacement = (SkillData)EditorGUI.ObjectField(
                objectRect,
                skill,
                typeof(SkillData),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                slot.objectReferenceValue = replacement;
                if (replacement != null)
                    SelectSkill(replacement);
            }

            var clickRect = new Rect(
                cardRect.x,
                cardRect.y,
                cardRect.width,
                cardRect.height - 34f);
            if (GUI.Button(clickRect, GUIContent.none, GUIStyle.none))
                SelectSkill(skill);

            HandleSkillDrop(cardRect, slot);
        }

        private void DrawSkillInspector()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(InspectorWidth)))
            {
                if (selectedSkill == null || skillObject == null)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        "스킬 카드를 선택하세요.\n이미지와 기능을 여기서 편집할 수 있습니다.",
                        EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                    return;
                }

                skillObject.UpdateIfRequiredOrScript();
                inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

                GUILayout.Label("선택한 스킬 편집", sectionTitleStyle);
                var previewRect = GUILayoutUtility.GetRect(96f, 96f, GUILayout.Height(96f));
                DrawIcon(selectedSkill.Icon, new Rect(
                    previewRect.center.x - 48f,
                    previewRect.y,
                    96f,
                    96f));

                DrawProperty("owner", "소속 캐릭터");
                DrawProperty("skillId", "스킬 ID");
                DrawProperty("displayName", "스킬 이름");
                DrawProperty("icon", "스킬 이미지");
                DrawProperty("description", "스킬 설명");

                EditorGUILayout.Space(10f);
                GUILayout.Label("스킬 타입과 대상", EditorStyles.boldLabel);
                var types = skillObject.FindProperty("skillTypes");
                var nextTypes = (SkillType)EditorGUILayout.EnumFlagsField(
                    new GUIContent("스킬 타입"),
                    (SkillType)types.intValue);
                types.intValue = (int)nextTypes;
                DrawProperty("customTypeName", "직접 입력 타입");
                DrawProperty("targetType", "대상 지정");

                EditorGUILayout.Space(10f);
                GUILayout.Label("실행 기능", EditorStyles.boldLabel);
                DrawProperty("cooldown", "쿨타임");
                DrawProperty("resourceCost", "자원 소모");
                DrawProperty("castTime", "시전 시간");
                DrawProperty("duration", "지속 시간");
                DrawProperty("effectPrefab", "기능/이펙트 프리팹");

                EditorGUILayout.Space(10f);
                GUILayout.Label("능력 수치", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "damage, radius, projectileCount, summonCount처럼 필요한 기능 키를 자유롭게 추가하세요.",
                    MessageType.Info);
                DrawProperty("abilities", "기능 목록");

                EditorGUILayout.Space(10f);
                if (GUILayout.Button("스킬 에셋 찾기", GUILayout.Height(26f)))
                {
                    Selection.activeObject = selectedSkill;
                    EditorGUIUtility.PingObject(selectedSkill);
                }

                EditorGUILayout.EndScrollView();
                if (skillObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(selectedSkill);
                    Repaint();
                }
            }
        }

        private void DrawProperty(string propertyName, string label)
        {
            var property = skillObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        private static void DrawIcon(Sprite sprite, Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.3f));
            if (sprite == null)
            {
                GUI.Label(
                    rect,
                    EditorGUIUtility.IconContent("Sprite Icon"),
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            var preview = AssetPreview.GetAssetPreview(sprite) ??
                          AssetPreview.GetMiniThumbnail(sprite);
            if (preview != null)
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
        }

        private static void DrawDivider()
        {
            var rect = GUILayoutUtility.GetRect(
                1f,
                1f,
                GUILayout.Width(1f),
                GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.45f));
        }

        private void HandleSkillDrop(Rect rect, SerializedProperty slot)
        {
            var currentEvent = Event.current;
            if (!rect.Contains(currentEvent.mousePosition) ||
                (currentEvent.type != EventType.DragUpdated &&
                 currentEvent.type != EventType.DragPerform))
                return;

            var dropped = DragAndDrop.objectReferences
                .OfType<SkillData>()
                .FirstOrDefault();
            if (dropped == null)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                slot.objectReferenceValue = dropped;
                SelectSkill(dropped);
                currentEvent.Use();
            }
        }

        private void RefreshAssets()
        {
            var characterPath = selectedCharacter != null
                ? AssetDatabase.GetAssetPath(selectedCharacter)
                : string.Empty;
            var skillPath = selectedSkill != null
                ? AssetDatabase.GetAssetPath(selectedSkill)
                : string.Empty;

            characters.Clear();
            LoadAssets(characters);
            RefreshCharacterDataList();
            characters.Sort((left, right) =>
                string.Compare(
                    CharacterLabel(left),
                    CharacterLabel(right),
                    StringComparison.CurrentCultureIgnoreCase));

            var restoredCharacter = !string.IsNullOrEmpty(characterPath)
                ? AssetDatabase.LoadAssetAtPath<CharacterSkillSetData>(characterPath)
                : characters.FirstOrDefault();
            SelectCharacter(restoredCharacter);

            if (!string.IsNullOrEmpty(skillPath))
                SelectSkill(AssetDatabase.LoadAssetAtPath<SkillData>(skillPath));
        }

        private static void LoadAssets<T>(List<T> destination)
            where T : UnityEngine.Object
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    destination.Add(asset);
            }
        }

        /// <summary>카탈로그 순서대로 캐릭터 데이터와 그 캐릭터의 스킬 채움 상태를 준비한다.</summary>
        private void RefreshCharacterDataList()
        {
            characterDataList.Clear();
            characterDataList.AddRange(CharacterSkillSetFactory.CatalogOrderedCharacters());

            // 팝업 라벨은 '/'를 하위 메뉴로 해석하므로 슬래시 없이 표기한다.
            characterDataLabels = characterDataList
                .Select(data =>
                {
                    var name = DisplayName(data.DisplayName);
                    var set = FindLoadedSkillSet(data);
                    return set == null
                        ? $"{name}  ·  스킬셋 없음"
                        : $"{name}  ·  스킬 {CharacterSkillSetFactory.CountFilledSlots(set)}개";
                })
                .ToArray();
        }

        /// <summary>이미 읽어 둔 세트 목록에서 이 캐릭터의 세트를 찾는다. 에셋을 다시 검색하지 않는다.</summary>
        private CharacterSkillSetData FindLoadedSkillSet(CharacterData data)
        {
            if (data == null) return null;
            if (data.SkillSet != null) return data.SkillSet;

            var linked = characters.FirstOrDefault(set => set.Character == data);
            if (linked != null) return linked;

            if (string.IsNullOrWhiteSpace(data.CharacterId)) return null;
            return characters.FirstOrDefault(set =>
                set.Character == null &&
                string.Equals(set.CharacterId, data.CharacterId, StringComparison.Ordinal));
        }

        private void SelectCharacter(CharacterSkillSetData character)
        {
            selectedCharacter = character;
            characterObject = character != null ? new SerializedObject(character) : null;
            boardScroll = Vector2.zero;

            if (selectedSkill != null && !CharacterContainsSkill(character, selectedSkill))
                SelectSkill(null);
            Repaint();
        }

        private void SelectSkill(SkillData skill)
        {
            selectedSkill = skill;
            skillObject = skill != null ? new SerializedObject(skill) : null;
            inspectorScroll = Vector2.zero;
            Repaint();
        }

        private void CreateCharacter()
        {
            EnsureFolder(CharacterFolder);
            var character = CreateInstance<CharacterSkillSetData>();
            var id = GenerateId(
                "character",
                characters.Select(value => value.CharacterId));
            character.Initialize(id, $"새 캐릭터 {characters.Count + 1}");
            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{CharacterFolder}/{id}-skills.asset");
            AssetDatabase.CreateAsset(character, path);
            AssetDatabase.SaveAssets();
            RefreshAssets();
            SelectCharacter(character);
        }

        private void CreateSkillForSlot(int sectionIndex, int slotIndex)
        {
            var skill = CharacterSkillSetFactory.FillSlot(
                selectedCharacter,
                sectionIndex,
                slotIndex);
            if (skill == null) return;

            characterObject?.Update();
            SelectSkill(skill);
        }

        /// <summary>빈 슬롯에 스킬 에셋을 만들어 채운다. sectionIndex가 -1이면 5구간 전체다.</summary>
        private void FillSlots(int sectionIndex)
        {
            if (selectedCharacter == null) return;

            var created = CharacterSkillSetFactory.FillEmptySlots(selectedCharacter, sectionIndex);
            var scope = sectionIndex < 0 ? "전체 구간" : $"{sectionIndex + 1}구간";
            Debug.Log(
                $"{DisplayName(selectedCharacter.DisplayName)} {scope} · " +
                $"스킬 {created}개를 새로 만들었습니다.");

            var keep = selectedCharacter;
            RefreshAssets();
            SelectCharacter(keep);
        }

        private static bool CharacterContainsSkill(
            CharacterSkillSetData character,
            SkillData skill)
        {
            if (character == null || skill == null)
                return false;

            foreach (var section in character.Sections)
            {
                if (section != null && section.Skills.Contains(skill))
                    return true;
            }
            return false;
        }

        private static string GenerateId(
            string prefix,
            IEnumerable<string> existingIds)
        {
            var used = new HashSet<string>(
                existingIds.Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.OrdinalIgnoreCase);
            var number = 1;
            while (used.Contains($"{prefix}-{number:0000}"))
                number++;
            return $"{prefix}-{number:0000}";
        }

        private static string CharacterLabel(CharacterSkillSetData character)
        {
            return character == null
                ? "(없음)"
                : $"{DisplayName(character.DisplayName)}  [{character.CharacterId}]";
        }

        private static string SkillTypeLabel(SkillData skill)
        {
            if ((skill.Types & SkillType.Custom) != 0 &&
                !string.IsNullOrWhiteSpace(skill.CustomTypeName))
                return skill.CustomTypeName;
            return skill.Types == SkillType.None ? "타입 미설정" : skill.Types.ToString();
        }

        private static string DisplayName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(이름 없음)" : value;
        }

        private static void EnsureFolder(string folderPath)
        {
            CharacterSkillSetFactory.EnsureFolder(folderPath);
        }

        private void EnsureStyles()
        {
            cardTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            cardMetaStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            sectionTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };
        }
    }
}
