using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.Editor
{
    public static class EquipmentScrollbarSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string RequestPath = "Temp/SetupEquipmentScrollbar.request";
        private const string SpriteRoot =
            "Assets/05_Resources/UI/BrightTheme/Recreated/Equipment/Scrollbar/";

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

        [MenuItem("Tools/Idle Battle/Style Equipment Scrollbars")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var equipment = canvas != null ? canvas.transform.Find("Equipment") : null;
            if (equipment == null)
                throw new MissingReferenceException("Canvas/Equipment was not found in Main.unity.");

            var trackSprite = Load("Scrollbar_Track");
            var handleSprite = Load("Scrollbar_Handle");
            var gripSprite = Load("Scrollbar_Grip");
            var scrollRects = equipment.GetComponentsInChildren<ScrollRect>(true);
            if (scrollRects.Length == 0)
                throw new MissingReferenceException("No ScrollRect was found below Canvas/Equipment.");

            var styledCount = 0;
            foreach (var scrollRect in scrollRects)
            {
                if (IsBelowNamedParent(scrollRect.transform, "Inventory"))
                {
                    RemoveVisibleScrollbar(scrollRect);
                    continue;
                }

                Style(scrollRect, trackSprite, handleSprite, gripSprite);
                styledCount++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Styled {styledCount} Equipment detail scrollbar(s); inventory scrollbar remains hidden.");
        }

        private static bool IsBelowNamedParent(Transform current, string parentName)
        {
            while (current != null)
            {
                if (current.name == parentName) return true;
                current = current.parent;
            }
            return false;
        }

        private static void RemoveVisibleScrollbar(ScrollRect scrollRect)
        {
            var scrollbar = scrollRect.verticalScrollbar;
            scrollRect.verticalScrollbar = null;
            scrollRect.verticalScrollbarSpacing = 0f;
            if (scrollbar != null)
                Object.DestroyImmediate(scrollbar.gameObject);
            EditorUtility.SetDirty(scrollRect);
        }

        private static void Style(ScrollRect scrollRect, Sprite trackSprite, Sprite handleSprite, Sprite gripSprite)
        {
            var scrollbar = scrollRect.verticalScrollbar;
            if (scrollbar == null)
                scrollbar = CreateScrollbar(scrollRect.transform);

            var barRect = scrollbar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.anchoredPosition = new Vector2(-4f, 0f);
            barRect.sizeDelta = new Vector2(32f, -10f);

            var track = scrollbar.GetComponent<Image>();
            if (track == null) track = scrollbar.gameObject.AddComponent<Image>();
            track.sprite = trackSprite;
            track.type = Image.Type.Sliced;
            track.color = Color.white;
            track.raycastTarget = true;

            var handleRect = scrollbar.handleRect;
            if (handleRect == null)
            {
                var sliding = CreateRect("Sliding Area", scrollbar.transform);
                Stretch(sliding, 4f);
                handleRect = CreateRect("Handle", sliding);
                Stretch(handleRect, 0f);
                scrollbar.handleRect = handleRect;
            }
            else
            {
                var sliding = handleRect.parent as RectTransform;
                if (sliding != null) Stretch(sliding, 4f);
            }

            var handle = handleRect.GetComponent<Image>();
            if (handle == null) handle = handleRect.gameObject.AddComponent<Image>();
            handle.sprite = handleSprite;
            handle.type = Image.Type.Sliced;
            handle.color = Color.white;
            handle.raycastTarget = true;

            var grip = handleRect.Find("Grip") as RectTransform;
            if (grip == null) grip = CreateRect("Grip", handleRect);
            grip.anchorMin = grip.anchorMax = grip.pivot = new Vector2(0.5f, 0.5f);
            grip.anchoredPosition = Vector2.zero;
            grip.sizeDelta = new Vector2(18f, 22f);
            var gripImage = grip.GetComponent<Image>();
            if (gripImage == null) gripImage = grip.gameObject.AddComponent<Image>();
            gripImage.sprite = gripSprite;
            gripImage.preserveAspect = true;
            gripImage.raycastTarget = false;

            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.transition = Selectable.Transition.ColorTint;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = 4f;
            scrollRect.scrollSensitivity = Mathf.Max(scrollRect.scrollSensitivity, 30f);

            EditorUtility.SetDirty(scrollRect);
            EditorUtility.SetDirty(scrollbar);
        }

        private static Scrollbar CreateScrollbar(Transform parent)
        {
            var rect = CreateRect("Scrollbar Vertical", parent);
            var image = rect.gameObject.AddComponent<Image>();
            var scrollbar = rect.gameObject.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = image;
            return scrollbar;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Sprite Load(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}{name}.png");
            if (sprite == null) throw new MissingReferenceException($"Scrollbar sprite not found: {name}");
            return sprite;
        }
    }
}
