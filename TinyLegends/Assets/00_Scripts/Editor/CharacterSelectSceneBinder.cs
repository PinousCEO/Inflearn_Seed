using System.Linq;
using IdleBattle;
using IdleBattle.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattleEditor
{
    /// <summary>
    /// Select 씬의 CharacterInfo · NameInput · 모험 시작 버튼을 CharacterSelectController에 연결하고,
    /// 카탈로그의 캐릭터 수만큼 버튼을 실제 씬 오브젝트로 만들어 줍니다.
    /// </summary>
    public static class CharacterSelectSceneBinder
    {
        private const string SelectSceneName = "Select";
        private const string ScenePath = "Assets/Scenes/Select.unity";
        private const string CatalogPath = "Assets/00_Data/Characters/CharacterCatalog.asset";
        private const string ContainerName = "CharacterButtons";
        private const float Spacing = 10f;

        private static readonly Vector2 ButtonSize = new Vector2(196f, 132f);

        [MenuItem("Tools/게임 데이터 관리/Select 씬 자동 연결", priority = 120)]
        public static void Bind()
        {
            if (!EnsureSelectScene(out var scene)) return;

            var controller = Object.FindFirstObjectByType<CharacterSelectController>();
            if (controller == null)
            {
                var host = new GameObject(nameof(CharacterSelectController));
                Undo.RegisterCreatedObjectUndo(host, "캐릭터 선택 컨트롤러 생성");
                controller = host.AddComponent<CharacterSelectController>();
            }

            var serialized = new SerializedObject(controller);
            AssignCatalog(serialized);

            var container = EnsureContainer();
            if (container != null)
            {
                var property = serialized.FindProperty("buttonContainer");
                if (property != null) property.objectReferenceValue = container;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 나머지 참조는 컨트롤러가 이름으로 찾아 채운다. 이미 연결된 값은 건드리지 않는다.
            Undo.RecordObject(controller, "Select 씬 자동 연결");
            controller.AutoBind();
            EditorUtility.SetDirty(controller);

            BuildButtonsInternal(container);

            EditorSceneManager.MarkSceneDirty(scene);
            Report(controller);
        }

        [MenuItem("Tools/게임 데이터 관리/Select 씬 캐릭터 버튼 다시 만들기", priority = 121)]
        public static void RebuildButtons()
        {
            if (!EnsureSelectScene(out var scene)) return;

            var container = EnsureContainer();
            if (container == null) return;

            var count = BuildButtonsInternal(container);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"캐릭터 버튼 {count}개를 씬에 만들었습니다.", container);
        }

        private static bool EnsureSelectScene(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (scene.name == SelectSceneName) return true;

            if (!EditorUtility.DisplayDialog(
                    "Select 씬",
                    $"현재 열린 씬은 '{scene.name}'입니다. Select 씬을 열까요?",
                    "열기", "취소"))
                return false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        /// <summary>카탈로그의 캐릭터마다 버튼 오브젝트를 하나씩 만듭니다.</summary>
        private static int BuildButtonsInternal(RectTransform container)
        {
            if (container == null) return 0;

            var catalog = LoadCatalog();
            if (catalog == null)
            {
                Debug.LogWarning($"CharacterCatalog을 찾지 못했습니다. 예상 경로: {CatalogPath}");
                return 0;
            }

            // 기존 버튼은 지우고 새로 만든다. 카탈로그 순서가 그대로 배치 순서가 된다.
            var existing = container.GetComponentsInChildren<CharacterSelectButton>(true);
            foreach (var button in existing)
                Undo.DestroyObjectImmediate(button.gameObject);

            var font = ResolveFont();
            var created = 0;
            for (var i = 0; i < catalog.Count; i++)
            {
                var data = catalog.Get(i);
                if (data == null) continue;

                var button = CharacterSelectButton.Create(container, data, font, ButtonSize);
                Undo.RegisterCreatedObjectUndo(button.gameObject, "캐릭터 버튼 생성");
                created++;
            }

            container.sizeDelta = new Vector2(
                created * ButtonSize.x + Mathf.Max(0, created - 1) * Spacing,
                ButtonSize.y);

            EditorUtility.SetDirty(container);
            return created;
        }

        /// <summary>씬에 이미 쓰이고 있는 TMP 폰트를 그대로 써야 한글이 제대로 나옵니다.</summary>
        private static TMP_FontAsset ResolveFont()
        {
            var sample = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(text => text != null && text.font != null);

            return sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        }

        private static CharacterCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            if (catalog != null) return catalog;

            return AssetDatabase.FindAssets("t:CharacterCatalog")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterCatalog>)
                .FirstOrDefault(value => value != null);
        }

        private static void AssignCatalog(SerializedObject serialized)
        {
            var property = serialized.FindProperty("catalog");
            if (property == null || property.objectReferenceValue != null) return;

            var catalog = LoadCatalog();
            if (catalog == null)
            {
                Debug.LogWarning($"CharacterCatalog을 찾지 못했습니다. 예상 경로: {CatalogPath}");
                return;
            }

            property.objectReferenceValue = catalog;
        }

        /// <summary>버튼이 들어갈 가로줄을 CharacterInfo 위쪽에 만듭니다.</summary>
        private static RectTransform EnsureContainer()
        {
            var existing = FindInScene(ContainerName);
            if (existing != null)
            {
                ApplyLayout(existing);
                return existing;
            }

            var info = FindInScene("CharacterInfo");
            if (info == null)
            {
                Debug.LogWarning("CharacterInfo를 찾지 못해 버튼 컨테이너를 만들지 못했습니다.");
                return null;
            }

            var host = new GameObject(ContainerName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(host, "캐릭터 버튼 자리 생성");

            var rect = (RectTransform)host.transform;
            rect.SetParent(info.parent, false);
            rect.SetSiblingIndex(info.GetSiblingIndex());
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // CharacterInfo 위쪽 여백에 놓는다. 정확한 위치는 씬에서 옮기면 된다.
            rect.anchoredPosition = new Vector2(
                info.anchoredPosition.x,
                info.anchoredPosition.y + info.rect.height * 0.5f + 110f);

            ApplyLayout(rect);
            return rect;
        }

        private static void ApplyLayout(RectTransform target)
        {
            if (!target.TryGetComponent(out HorizontalLayoutGroup layout))
                layout = Undo.AddComponent<HorizontalLayoutGroup>(target.gameObject);

            layout.spacing = Spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
        }

        private static void Report(CharacterSelectController controller)
        {
            var serialized = new SerializedObject(controller);
            var missing = new[]
                {
                    "catalog", "buttonContainer", "infoRoot", "characterNameText",
                    "roleText", "descriptionText", "skillText", "nameInput",
                    "nameHintText", "startButton"
                }
                .Where(name =>
                {
                    var property = serialized.FindProperty(name);
                    return property == null || property.objectReferenceValue == null;
                })
                .ToArray();

            if (missing.Length == 0)
            {
                Debug.Log("Select 씬 자동 연결을 마쳤습니다. 모든 참조가 채워졌습니다.", controller);
                return;
            }

            Debug.LogWarning(
                "Select 씬 자동 연결을 마쳤지만 다음 항목은 직접 지정해야 합니다: " +
                string.Join(", ", missing),
                controller);
        }

        private static RectTransform FindInScene(string objectName)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var match = FindChild(root.transform, objectName);
                if (match != null) return match as RectTransform;
            }

            return null;
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), objectName);
                if (match != null) return match;
            }

            return null;
        }
    }
}
