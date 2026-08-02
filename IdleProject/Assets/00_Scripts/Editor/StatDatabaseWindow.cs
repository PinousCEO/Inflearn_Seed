using System.Linq;
using IdleBattle;
using UnityEditor;
using UnityEngine;

namespace IdleBattleEditor
{
    public sealed class StatDatabaseWindow : EditorWindow
    {
        private const string CatalogPath = "Assets/00_Data/Stats/StatCatalog.asset";
        private StatCatalog catalog;
        private Vector2 scroll;

        [MenuItem("Tools/게임 데이터 관리/능력치 정의", priority = 90)]
        public static void Open() => GetWindow<StatDatabaseWindow>("능력치 정의");

        [InitializeOnLoadMethod]
        private static void EnsureRecommendedCatalog()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<StatCatalog>(CatalogPath) != null) return;
                CreateCatalogAsset();
            };
        }

        private void OnEnable() => catalog = AssetDatabase.LoadAssetAtPath<StatCatalog>(CatalogPath);

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("능력치 데이터베이스", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("정의는 한 곳에서 관리하고, 캐릭터와 장비는 StatType + 값만 저장합니다. Flat은 고정값, AdditivePercent는 합연산 %, MultiplicativePercent는 독립 곱연산 %입니다.", MessageType.Info);

            if (catalog == null)
            {
                if (GUILayout.Button("추천 기본 능력치 카탈로그 생성", GUILayout.Height(36))) CreateCatalog();
                return;
            }

            EditorGUILayout.ObjectField("카탈로그", catalog, typeof(StatCatalog), false);
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                foreach (StatCategory category in System.Enum.GetValues(typeof(StatCategory)))
                {
                    var rows = catalog.Definitions.Where(x => x.category == category).ToArray();
                    if (rows.Length == 0) continue;
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField(CategoryName(category), EditorStyles.boldLabel);
                    foreach (var row in rows)
                    {
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                        {
                            EditorGUILayout.LabelField(row.displayName, GUILayout.Width(145));
                            EditorGUILayout.LabelField(row.type.ToString(), GUILayout.Width(170));
                            EditorGUILayout.LabelField(row.appendPercent ? "%" : "수치", GUILayout.Width(35));
                            EditorGUILayout.LabelField($"기본 {row.defaultValue:0.##}", GUILayout.Width(75));
                            EditorGUILayout.LabelField(row.showInEquipmentSummary ? "요약 표시" : "상세 표시");
                        }
                    }
                }
            }

            if (GUILayout.Button("카탈로그 Inspector에서 편집")) Selection.activeObject = catalog;
        }

        private void CreateCatalog()
        {
            catalog = CreateCatalogAsset();
            Selection.activeObject = catalog;
        }

        private static StatCatalog CreateCatalogAsset()
        {
            System.IO.Directory.CreateDirectory("Assets/00_Data/Stats");
            var created = CreateInstance<StatCatalog>();
            created.ResetToRecommendedDefaults();
            AssetDatabase.CreateAsset(created, CatalogPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        private static string CategoryName(StatCategory category) => category switch
        {
            StatCategory.Core => "핵심", StatCategory.Offense => "공격",
            StatCategory.Defense => "방어 및 저항", StatCategory.Resource => "자원",
            _ => "유틸리티"
        };
    }
}
