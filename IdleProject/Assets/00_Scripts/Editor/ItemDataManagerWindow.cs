using System;
using System.Collections.Generic;
using System.Linq;
using IdleBattle;
using UnityEditor;
using UnityEngine;

namespace IdleBattleEditor
{
    public sealed class ItemDataManagerWindow : EditorWindow
    {
        private const string ItemFolder = "Assets/00_Data/Items";
        private const float SidebarWidth = 300f;
        private const float ItemRowHeight = 58f;

        private readonly List<ItemData> allItems = new List<ItemData>();
        private readonly List<ItemData> filteredItems = new List<ItemData>();

        private ItemData selectedItem;
        private SerializedObject selectedObject;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private string searchText = string.Empty;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle emptyStateStyle;
        private GUIStyle itemNameStyle;
        private GUIStyle itemMetaStyle;

        [MenuItem("Tools/게임 데이터 관리/아이템 관리", priority = 100)]
        public static void Open()
        {
            var window = GetWindow<ItemDataManagerWindow>();
            window.titleContent = new GUIContent("아이템 관리");
            window.minSize = new Vector2(780f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("아이템 관리");
            Undo.undoRedoPerformed += HandleUndoRedo;
            RefreshItems();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void OnProjectChange()
        {
            RefreshItems();
            Repaint();
        }

        private void HandleUndoRedo()
        {
            RefreshItems();
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();

            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            DrawDivider();
            DrawDetails();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));
            DrawSidebarHeader();

            if (filteredItems.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    string.IsNullOrWhiteSpace(searchText)
                        ? "등록된 아이템이 없습니다.\n아래 버튼으로 첫 아이템을 추가해 보세요."
                        : "검색 결과가 없습니다.",
                    emptyStateStyle);
                GUILayout.FlexibleSpace();
            }
            else
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll);
                foreach (var item in filteredItems)
                    DrawItemRow(item);
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                if (GUILayout.Button("+ 아이템 추가", GUILayout.Height(34f)))
                    CreateItem();
                GUILayout.Space(8f);
            }
            EditorGUILayout.Space(8f);
            EditorGUILayout.EndVertical();
        }

        private void DrawSidebarHeader()
        {
            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10f);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("아이템", titleStyle);
                    GUILayout.Label($"전체 {allItems.Count}개", subtitleStyle);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh"), GUILayout.Width(30f), GUILayout.Height(26f)))
                    RefreshItems();
                GUILayout.Space(8f);
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                EditorGUI.BeginChangeCheck();
                searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                    ApplySearch();
                GUILayout.Space(8f);
            }
            EditorGUILayout.Space(6f);
        }

        private void DrawItemRow(ItemData item)
        {
            var rowRect = GUILayoutUtility.GetRect(SidebarWidth - 16f, ItemRowHeight);
            rowRect.x += 6f;
            rowRect.width -= 12f;

            var isSelected = item == selectedItem;
            if (Event.current.type == EventType.Repaint)
            {
                var background = isSelected
                    ? new Color(0.22f, 0.45f, 0.72f, 0.72f)
                    : new Color(1f, 1f, 1f, 0.035f);
                EditorGUI.DrawRect(rowRect, background);
            }

            var iconRect = new Rect(rowRect.x + 7f, rowRect.y + 7f, 44f, 44f);
            DrawIcon(item.Icon, iconRect);

            var textX = iconRect.xMax + 9f;
            var nameRect = new Rect(textX, rowRect.y + 9f, rowRect.xMax - textX - 6f, 20f);
            var metaRect = new Rect(textX, rowRect.y + 31f, rowRect.xMax - textX - 6f, 18f);
            EditorGUI.LabelField(nameRect, ItemName(item), itemNameStyle);
            EditorGUI.LabelField(
                metaRect,
                $"{item.ItemId}  ·  드랍 {FormatDropRate(item.DropRatePercent)}",
                itemMetaStyle);

            if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                SelectItem(item);
        }

        private static void DrawIcon(Sprite sprite, Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.18f));
            if (sprite != null)
            {
                var texture = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
                if (texture != null)
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(rect, EditorGUIUtility.IconContent("Sprite Icon"), new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter
                });
            }
        }

        private static void DrawDivider()
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.35f));
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical();
            if (selectedItem == null || selectedObject == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("목록에서 아이템을 선택하세요.", emptyStateStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            selectedObject.UpdateIfRequiredOrScript();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            DrawDetailHeader();
            DrawDetailFields();
            EditorGUILayout.EndScrollView();
            DrawDetailFooter();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailHeader()
        {
            EditorGUILayout.Space(18f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(22f);
                var previewRect = GUILayoutUtility.GetRect(112f, 112f, GUILayout.Width(112f), GUILayout.Height(112f));
                DrawIcon(selectedItem.Icon, previewRect);
                GUILayout.Space(18f);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(14f);
                    GUILayout.Label(ItemName(selectedItem), titleStyle);
                    GUILayout.Label(selectedItem.ItemId, subtitleStyle);
                    GUILayout.Space(8f);
                    GUILayout.Label($"{GetRarityLabel(selectedItem.Rarity)} · {GetTypeLabel(selectedItem.Type)}", EditorStyles.miniLabel);
                    GUILayout.Space(8f);
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Width(150f)))
                    {
                        GUILayout.Label("드랍률", EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(FormatDropRate(selectedItem.DropRatePercent), EditorStyles.boldLabel);
                    }
                    GUILayout.Space(4f);
                    if (GUILayout.Button("프로젝트에서 에셋 찾기", GUILayout.Width(150f), GUILayout.Height(25f)))
                    {
                        Selection.activeObject = selectedItem;
                        EditorGUIUtility.PingObject(selectedItem);
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.Space(22f);
            }
            EditorGUILayout.Space(16f);
        }

        private void DrawDetailFields()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(22f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("기본 정보", EditorStyles.boldLabel);
                    EditorGUILayout.Space(5f);
                    DrawProperty("itemId", "아이템 ID");
                    DrawProperty("displayName", "이름");
                    DrawProperty("description", "설명");
                    DrawProperty("icon", "아이콘");
                    EditorGUILayout.Space(10f);
                    GUILayout.Label("분류 및 설정", EditorStyles.boldLabel);
                    EditorGUILayout.Space(5f);
                    DrawProperty("itemType", "종류");
                    DrawProperty("rarity", "등급");
                    DrawProperty("buyPrice", "구매 가격");
                    DrawProperty("sellPrice", "판매 가격");
                    DrawProperty("maxStack", "최대 중첩 수");
                    DrawProperty("dropRatePercent", "드랍률 (%)");
                }
                GUILayout.Space(22f);
            }

            if (selectedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(selectedItem);
                ApplySearch();
                Repaint();
            }
        }

        private void DrawProperty(string propertyName, string label)
        {
            var property = selectedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        private void DrawDetailFooter()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(20f);
                GUILayout.Label("변경 사항은 에셋에 자동 저장됩니다.", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                var previousColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
                if (GUILayout.Button("아이템 삭제", GUILayout.Width(110f), GUILayout.Height(30f)))
                    DeleteSelectedItem();
                GUI.backgroundColor = previousColor;
                GUILayout.Space(20f);
            }
            EditorGUILayout.Space(10f);
        }

        private void RefreshItems()
        {
            var selectedPath = selectedItem != null ? AssetDatabase.GetAssetPath(selectedItem) : string.Empty;
            allItems.Clear();

            foreach (var guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null)
                    allItems.Add(item);
            }

            allItems.Sort((left, right) =>
                string.Compare(ItemName(left), ItemName(right), StringComparison.CurrentCultureIgnoreCase));
            ApplySearch();

            if (!string.IsNullOrEmpty(selectedPath))
                SelectItem(allItems.FirstOrDefault(item => AssetDatabase.GetAssetPath(item) == selectedPath));
            else if (selectedItem != null && !allItems.Contains(selectedItem))
                SelectItem(null);
        }

        private void ApplySearch()
        {
            filteredItems.Clear();
            var query = searchText.Trim();

            foreach (var item in allItems)
            {
                if (string.IsNullOrEmpty(query) ||
                    ItemName(item).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    filteredItems.Add(item);
                }
            }
        }

        private void SelectItem(ItemData item)
        {
            selectedItem = item;
            selectedObject = item != null ? new SerializedObject(item) : null;
            detailScroll = Vector2.zero;
            Repaint();
        }

        private void CreateItem()
        {
            EnsureFolder(ItemFolder);

            var item = CreateInstance<ItemData>();
            var id = GenerateNextId();
            item.Initialize(id, $"새 아이템 {allItems.Count + 1}");

            var path = AssetDatabase.GenerateUniqueAssetPath($"{ItemFolder}/{id}.asset");
            AssetDatabase.CreateAsset(item, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshItems();
            SelectItem(item);
            Selection.activeObject = item;
            EditorGUIUtility.PingObject(item);
        }

        private string GenerateNextId()
        {
            var usedIds = new HashSet<string>(
                allItems.Where(item => !string.IsNullOrWhiteSpace(item.ItemId)).Select(item => item.ItemId),
                StringComparer.OrdinalIgnoreCase);

            var number = 1;
            string id;
            do
            {
                id = $"item-{number:0000}";
                number++;
            } while (usedIds.Contains(id));

            return id;
        }

        private void DeleteSelectedItem()
        {
            if (selectedItem == null)
                return;

            var itemName = ItemName(selectedItem);
            if (!EditorUtility.DisplayDialog(
                    "아이템 삭제",
                    $"'{itemName}' 아이템을 삭제할까요?\n이 작업은 되돌릴 수 없습니다.",
                    "삭제",
                    "취소"))
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(selectedItem);
            SelectItem(null);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            RefreshItems();
        }

        private static void EnsureFolder(string folderPath)
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

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                wordWrap = true
            };
            subtitleStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11
            };
            emptyStateStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = true
            };
            itemNameStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                clipping = TextClipping.Clip
            };
            itemMetaStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip
            };
        }

        private static string ItemName(ItemData item)
        {
            return item == null || string.IsNullOrWhiteSpace(item.DisplayName)
                ? "(이름 없음)"
                : item.DisplayName;
        }

        private static string GetTypeLabel(ItemType type)
        {
            return type switch
            {
                ItemType.Equipment => "장비",
                ItemType.Consumable => "소모품",
                ItemType.Material => "재료",
                ItemType.Currency => "재화",
                ItemType.Quest => "퀘스트",
                _ => "기타"
            };
        }

        private static string GetRarityLabel(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => "일반",
                ItemRarity.Uncommon => "고급",
                ItemRarity.Rare => "희귀",
                ItemRarity.Epic => "영웅",
                ItemRarity.Legendary => "전설",
                _ => rarity.ToString()
            };
        }

        private static string FormatDropRate(float dropRatePercent)
        {
            return $"{dropRatePercent:0.##}%";
        }
    }
}
