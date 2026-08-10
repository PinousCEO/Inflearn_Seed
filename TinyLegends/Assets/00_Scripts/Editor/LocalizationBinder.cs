using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleBattle.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleBattle.EditorTools
{
    /// <summary>
    /// 씬에 박아 둔 글자에 <see cref="LocalizedText"/>를 붙여 주는 도구입니다.
    ///
    /// 어디에 어떤 키를 붙일지는 <c>Assets/00_Scripts/Editor/SceneTextKeys.csv</c>에 적혀 있습니다.
    /// 열은 <c>scene,path,key,ko_at_build</c>이고, path는 씬 루트부터의 오브젝트 경로입니다.
    /// ko_at_build는 표를 만들 때 씬에 있던 한국어라서, 그 뒤로 씬 글자가 바뀌었는지 확인하는 데 씁니다.
    ///
    /// 지금 열려 있는 씬만 손봅니다. Title · Select · Main을 각각 열고 한 번씩 실행하세요.
    /// </summary>
    public static class LocalizationBinder
    {
        private const string MapAssetPath = "Assets/00_Scripts/Editor/SceneTextKeys.csv";

        [MenuItem("Tools/Idle Battle/Localization/현재 씬 글자에 키 붙이기", priority = 200)]
        private static void BindCurrentSceneMenu()
        {
            var summary = Bind(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("로컬라이제이션",
                summary + "\n\n자세한 내용은 Console을 확인하세요.\n씬을 저장해야 반영됩니다.", "확인");
        }

        /// <summary>씬 하나에 키를 붙입니다. 결과를 한 줄로 돌려주고, 자세한 내용은 Console에 남깁니다.</summary>
        public static string Bind(Scene scene)
        {
            var map = LoadMap();
            if (map == null) return $"{MapAssetPath} 를 찾지 못했습니다.";

            if (!map.TryGetValue(scene.name, out var entries))
                return $"'{scene.name}' 씬에 대한 항목이 {MapAssetPath}에 없습니다. " +
                       "Title · Select · Main 중 하나를 열고 실행하세요.";

            var log = new StringBuilder();
            int bound = 0, updated = 0, missing = 0, drifted = 0;

            foreach (var entry in entries)
            {
                var target = FindByPath(scene, entry.Path, entry.KoAtBuild);
                if (target == null)
                {
                    missing++;
                    log.AppendLine($"  [없음] {entry.Path}");
                    continue;
                }

                if (!target.TryGetComponent(out TMP_Text label))
                {
                    missing++;
                    log.AppendLine($"  [TMP 아님] {entry.Path}");
                    continue;
                }

                // 표를 만든 뒤 씬 글자가 바뀌었으면 키가 어긋났을 수 있어 알려 줍니다.
                if (!string.IsNullOrEmpty(entry.KoAtBuild) && label.text != entry.KoAtBuild)
                {
                    drifted++;
                    log.AppendLine($"  [글자 바뀜] {entry.Path}\n      씬='{label.text}'\n      표='{entry.KoAtBuild}'");
                }

                var localized = target.GetComponent<LocalizedText>();
                if (localized == null)
                {
                    localized = Undo.AddComponent<LocalizedText>(target);
                    bound++;
                }
                else
                {
                    Undo.RecordObject(localized, "Localization 키 지정");
                    updated++;
                }

                localized.EditorBind(entry.Key, label.text);
                EditorUtility.SetDirty(localized);
            }

            EditorSceneManager.MarkSceneDirty(scene);

            var summary = $"'{scene.name}' 씬: 새로 붙임 {bound} · 키만 갱신 {updated} · " +
                          $"못 찾음 {missing} · 글자 바뀜 {drifted}";
            Debug.Log("[Localization] " + summary + (log.Length > 0 ? "\n" + log : string.Empty));
            return summary;
        }

        [MenuItem("Tools/Idle Battle/Localization/현재 씬에서 키 떼기", priority = 201)]
        private static void UnbindCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            var removed = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var localized in root.GetComponentsInChildren<LocalizedText>(true))
                {
                    Undo.DestroyObjectImmediate(localized);
                    removed++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("로컬라이제이션",
                $"'{scene.name}' 씬에서 LocalizedText {removed}개를 떼었습니다.", "확인");
        }

        [MenuItem("Tools/Idle Battle/Localization/빠진 키 찾기", priority = 220)]
        private static void ReportMissingKeys()
        {
            var table = LoadLocalizationKeys();
            var map = LoadMap();
            if (map == null) return;

            var log = new StringBuilder();
            var missing = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var entries in map.Values)
                foreach (var entry in entries)
                    if (!table.Contains(entry.Key)) missing.Add(entry.Key);

            if (missing.Count == 0)
                log.AppendLine("SceneTextKeys.csv의 모든 키가 Localization.csv에 있습니다.");
            else
                foreach (var key in missing) log.AppendLine("  없는 키: " + key);

            // 씬에 붙어 있는데 표에 없는 키도 함께 봅니다.
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var localized in root.GetComponentsInChildren<LocalizedText>(true))
                    if (!string.IsNullOrEmpty(localized.Key) && !table.Contains(localized.Key))
                        log.AppendLine($"  씬에 붙었지만 표에 없는 키: {localized.Key} ({localized.name})");

            Debug.Log("[Localization] 키 점검\n" + log);
            EditorUtility.DisplayDialog("로컬라이제이션", "결과를 Console에 적었습니다.", "확인");
        }

        // ------------------------------------------------------------------
        // 표 읽기
        // ------------------------------------------------------------------

        private readonly struct Entry
        {
            public readonly string Path;
            public readonly string Key;
            public readonly string KoAtBuild;

            public Entry(string path, string key, string koAtBuild)
            {
                Path = path;
                Key = key;
                KoAtBuild = koAtBuild;
            }
        }

        private static Dictionary<string, List<Entry>> LoadMap()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(MapAssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("로컬라이제이션",
                    $"{MapAssetPath} 를 찾지 못했습니다.", "확인");
                return null;
            }

            var result = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
            var rows = ParseCsv(asset.text);

            for (var i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Count < 3) continue;

                var scene = row[0].Trim();
                var path = row[1].Trim();
                var key = row[2].Trim();
                if (scene.Length == 0 || path.Length == 0 || key.Length == 0) continue;

                var ko = row.Count > 3 ? row[3].Replace("\\n", "\n") : string.Empty;

                if (!result.TryGetValue(scene, out var list))
                {
                    list = new List<Entry>();
                    result[scene] = list;
                }

                list.Add(new Entry(path, key, ko));
            }

            return result;
        }

        private static HashSet<string> LoadLocalizationKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/" + Localization.ResourcePath + ".csv");
            if (asset == null) return keys;

            var rows = ParseCsv(asset.text);
            for (var i = 1; i < rows.Count; i++)
                if (rows[i].Count > 0 && rows[i][0].Trim().Length > 0)
                    keys.Add(rows[i][0].Trim());

            return keys;
        }

        /// <summary>
        /// 씬 루트부터의 경로로 오브젝트를 찾습니다. 꺼져 있는 오브젝트도 찾습니다.
        ///
        /// 씬에는 이름이 같은 형제가 있습니다(예: ItemInfo/Info 아래 'Horizontal'이 두 개).
        /// 그래서 경로 하나가 여러 오브젝트를 가리킬 수 있어, 이름이 맞는 가지를 모두 따라간 뒤
        /// 표에 적어 둔 글자와 같은 것을 고릅니다. 글자로도 못 가리면 첫 번째를 씁니다.
        /// </summary>
        private static GameObject FindByPath(Scene scene, string path, string expectedText)
        {
            var parts = path.Split('/');
            if (parts.Length == 0) return null;

            var level = scene.GetRootGameObjects()
                .Where(go => go.name == parts[0])
                .Select(go => go.transform)
                .ToList();

            for (var i = 1; i < parts.Length && level.Count > 0; i++)
            {
                var next = new List<Transform>();
                foreach (var parent in level)
                    for (var c = 0; c < parent.childCount; c++)
                        if (parent.GetChild(c).name == parts[i])
                            next.Add(parent.GetChild(c));

                level = next;
            }

            if (level.Count == 0) return null;
            if (level.Count == 1) return level[0].gameObject;

            if (!string.IsNullOrEmpty(expectedText))
                foreach (var candidate in level)
                    if (candidate.TryGetComponent(out TMP_Text text) && text.text == expectedText)
                        return candidate.gameObject;

            Debug.LogWarning($"[Localization] '{path}' 가 오브젝트 {level.Count}개를 가리킵니다. 첫 번째에 붙입니다.");
            return level[0].gameObject;
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return rows;

            var row = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            var i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;

            for (; i < text.Length; i++)
            {
                var c = text[i];

                if (quoted)
                {
                    if (c != '"')
                    {
                        field.Append(c);
                    }
                    else if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        field.Clear();
                        if (row.Any(cell => cell.Trim().Length > 0)) rows.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                if (row.Any(cell => cell.Trim().Length > 0)) rows.Add(row);
            }

            return rows;
        }
    }
}
