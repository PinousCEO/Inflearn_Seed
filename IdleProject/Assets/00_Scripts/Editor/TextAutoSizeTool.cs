using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleBattle.EditorTools
{
    /// <summary>
    /// 씬과 프리팹에 있는 TMP 글자를 모두 AutoSize로 바꿉니다.
    /// Tools/Idle Battle/Text/모든 글자 AutoSize 켜기 로 실행합니다.
    ///
    /// 지금 크기를 <b>Max</b>로 박아 둡니다. 그래서 글자가 지금보다 커지는 일은 없고, 칸을 넘칠 때만 줄어듭니다.
    /// 언어를 바꾸면 같은 뜻이라도 길이가 달라지는데(일본어·영어가 한국어보다 깁니다),
    /// 이렇게 두면 칸 밖으로 삐져나오지 않고 알아서 줄어듭니다.
    ///
    /// Min은 Max의 <see cref="MinRatio"/>배입니다. 이보다 더 줄어들면 읽기 어려워서 차라리 넘치는 편이 낫습니다.
    /// 이미 AutoSize가 켜져 있는 글자는 손대지 않습니다. 누군가 일부러 맞춰 둔 값을 덮지 않기 위해서입니다.
    /// </summary>
    public static class TextAutoSizeTool
    {
        /// <summary>Min은 Max의 이 비율입니다. 절반까지 줄어들면 웬만한 번역문은 다 들어갑니다.</summary>
        private const float MinRatio = .5f;

        /// <summary>이 아래로는 줄이지 않습니다. 더 작아지면 화면에서 읽히지 않습니다.</summary>
        private const float MinFloor = 10f;

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Title.unity",
            "Assets/Scenes/Select.unity",
            "Assets/Scenes/Main.unity",
        };

        /// <summary>우리가 만든 프리팹만 봅니다. 에셋스토어에서 받은 것은 건드리지 않습니다.</summary>
        private static readonly string[] PrefabFolders =
        {
            "Assets/01_Prefabs",
            "Assets/Resources",
        };

        [MenuItem("Tools/Idle Battle/Text/모든 글자 AutoSize 켜기", priority = 300)]
        private static void RunAll()
        {
            if (!EditorUtility.DisplayDialog("AutoSize",
                    "씬 3개와 프리팹의 TMP 글자를 모두 AutoSize로 바꿉니다.\n" +
                    "지금 크기가 Max가 되고, 칸을 넘칠 때만 줄어듭니다.\n\n" +
                    "이미 AutoSize가 켜진 글자는 그대로 둡니다.", "실행", "그만두기"))
                return;

            var (changed, skipped) = ApplyEverywhere();
            EditorUtility.DisplayDialog("AutoSize",
                $"바꾼 글자 {changed}개\n이미 켜져 있어 그대로 둔 글자 {skipped}개\n\n자세한 내용은 Console에 있습니다.", "확인");
        }

        /// <summary>씬 3개와 우리 프리팹을 모두 손봅니다. 물어보지 않고 바로 저장합니다.</summary>
        public static (int changed, int skipped) ApplyEverywhere()
        {
            int changed = 0, skipped = 0;
            var log = new List<string>();

            // 씬 세 개를 차례로 여는데, 여는 순간 저장하지 않은 손질은 사라집니다. 먼저 저장해 둡니다.
            if (EditorSceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveOpenScenes();

            var openScene = EditorSceneManager.GetActiveScene().path;

            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var (c, s) = ApplyToScene(scene);
                changed += c;
                skipped += s;
                log.Add($"  {scene.name} 씬: 바꿈 {c} · 그대로 {s}");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", PrefabFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                var (c, s) = Apply(root.GetComponentsInChildren<TMP_Text>(true), record: false);
                if (c > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add($"  {path}: 바꿈 {c}");
                }

                PrefabUtility.UnloadPrefabContents(root);
                changed += c;
                skipped += s;
            }

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(openScene)) EditorSceneManager.OpenScene(openScene, OpenSceneMode.Single);

            Debug.Log($"[AutoSize] 바꾼 글자 {changed}개 · 이미 켜져 있어 그대로 둔 글자 {skipped}개\n" +
                      string.Join("\n", log));
            return (changed, skipped);
        }

        [MenuItem("Tools/Idle Battle/Text/현재 씬만 AutoSize 켜기", priority = 301)]
        private static void RunCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            var (changed, skipped) = ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[AutoSize] '{scene.name}' 씬: 바꿈 {changed} · 그대로 {skipped}");
            EditorUtility.DisplayDialog("AutoSize",
                $"'{scene.name}' 씬\n바꾼 글자 {changed}개 · 그대로 둔 글자 {skipped}개\n\n씬을 저장해야 반영됩니다.", "확인");
        }

        private static (int changed, int skipped) ApplyToScene(Scene scene)
        {
            var labels = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true));
            return Apply(labels, record: true);
        }

        private static (int changed, int skipped) Apply(IEnumerable<TMP_Text> labels, bool record)
        {
            int changed = 0, skipped = 0;

            foreach (var label in labels)
            {
                if (label == null) continue;
                if (label.enableAutoSizing)
                {
                    skipped++;
                    continue;
                }

                // 켜는 순간 fontSize가 계산값으로 덮이므로, 지금 크기를 먼저 붙잡아 둡니다.
                var current = label.fontSize;
                if (current <= 0f)
                {
                    skipped++;
                    continue;
                }

                if (record) Undo.RecordObject(label, "AutoSize 켜기");

                label.fontSizeMax = current;
                label.fontSizeMin = Mathf.Max(MinFloor > current ? current : MinFloor, current * MinRatio);
                label.enableAutoSizing = true;

                EditorUtility.SetDirty(label);
                changed++;
            }

            return (changed, skipped);
        }
    }
}
