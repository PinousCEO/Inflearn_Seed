using System.IO;
using IdleBattle.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.Editor
{
    public static class BottomNavigationSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string RequestPath = "Temp/SetupBottomNavigation.request";
        private const string StateRoot = "Assets/05_Resources/UI/BrightTheme/Recreated/States/";

        [InitializeOnLoadMethod]
        private static void SetupWhenRequested()
        {
            if (!File.Exists(RequestPath)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Setup();
                File.Delete(RequestPath);
            };
        }

        [MenuItem("Tools/Idle Battle/Setup Bottom Navigation")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null) throw new MissingReferenceException("Canvas was not found in Main.unity.");
            var main = canvas.transform.Find("Main");
            var equipment = canvas.transform.Find("Equipment");
            var bottom = canvas.transform.Find("Bottom");
            if (main == null || equipment == null || bottom == null)
                throw new MissingReferenceException("Main, Equipment, or Bottom root was not found under Canvas.");

            var controller = bottom.GetComponent<BottomNavigationController>();
            if (controller == null) controller = bottom.gameObject.AddComponent<BottomNavigationController>();
            bottom.gameObject.SetActive(true);
            bottom.SetAsLastSibling();

            // The Equipment builder used to create a second navigation bar
            // inside the screen. Keep the Canvas-level Bottom as the single
            // persistent navigation so two bars never overlap during a fade.
            var legacyEquipmentNavigation = equipment.Find("SafeArea/BottomNavigation");
            if (legacyEquipmentNavigation != null)
                legacyEquipmentNavigation.gameObject.SetActive(false);

            var normalNames = new[] { "Equipment", "Dungeon", "Main", "Skill", "Shop" };
            for (var i = 0; i < bottom.childCount && i < normalNames.Length; i++)
            {
                var item = bottom.GetChild(i) as RectTransform;
                if (item == null) continue;
                var iconFrame = item.Find("IconFrame")?.GetComponent<Image>();
                var button = item.GetComponent<Button>();
                if (button == null) button = item.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = iconFrame;

                var oldUnderline = item.Find("SelectedUnderline");
                if (oldUnderline != null) Object.DestroyImmediate(oldUnderline.gameObject);
            }

            var indicator = bottom.Find("SelectionIndicator") as RectTransform;
            if (indicator == null)
            {
                var indicatorObject = new GameObject("SelectionIndicator", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                indicatorObject.layer = 5;
                indicatorObject.transform.SetParent(bottom, false);
                indicator = indicatorObject.GetComponent<RectTransform>();
            }
            indicator.SetAsLastSibling();
            indicator.anchorMin = new Vector2(0.5f, 0f);
            indicator.anchorMax = new Vector2(0.5f, 0f);
            indicator.pivot = new Vector2(0.5f, 0.5f);
            indicator.anchoredPosition = new Vector2(0f, 13f);
            indicator.sizeDelta = new Vector2(98f, 12f);
            var indicatorLayout = indicator.GetComponent<LayoutElement>();
            indicatorLayout.ignoreLayout = true;
            var indicatorImage = indicator.GetComponent<Image>();
            indicatorImage.sprite = Load("Nav_SelectedUnderline");
            indicatorImage.preserveAspect = true;
            indicatorImage.raycastTarget = false;

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("mainScreen").objectReferenceValue = main.gameObject;
            serialized.FindProperty("equipmentScreen").objectReferenceValue = equipment.gameObject;
            serialized.FindProperty("normalFrame").objectReferenceValue = Load("Nav_Frame_Normal");
            serialized.FindProperty("selectedFrame").objectReferenceValue = Load("Nav_Frame_Selected");
            serialized.FindProperty("selectionIndicator").objectReferenceValue = indicatorImage;
            serialized.FindProperty("setupVersion").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(bottom.gameObject);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Bottom navigation configured: fade-only screens, moving selection bar, raised selected frame.");
        }

        private static Sprite Load(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{StateRoot}{name}.png");
            if (sprite == null) throw new MissingReferenceException($"Navigation sprite not found: {name}");
            return sprite;
        }
    }
}
