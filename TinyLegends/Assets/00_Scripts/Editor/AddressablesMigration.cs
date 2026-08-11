using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace IdleBattle.Editor
{
    /// <summary>Resources의 게임 콘텐츠를 Addressables 폴더와 기능별 그룹으로 이전합니다.</summary>
    [InitializeOnLoad]
    public static class AddressablesMigration
    {
        private const string SourceRoot = "Assets/Resources/";
        private const string TargetRoot = "Assets/AddressableContent/";

        private readonly struct Rule
        {
            public readonly string Source;
            public readonly string Group;
            public readonly string Label;
            public Rule(string source, string group, string label) { Source = source; Group = group; Label = label; }
        }

        private static readonly Rule[] Rules =
        {
            new("01_Prefabs/Character.prefab", "Content-Characters", "content.characters"),
            new("01_Prefabs/Enemy.prefab", "Content-Characters", "content.characters"),
            new("01_Prefabs/Effects", "Content-Effects", "content.effects"),
            new("01_Prefabs/UI", "Content-UI", "content.ui"),
            new("UI", "Content-UI", "content.ui"),
            new("Data/ItemCatalog.asset", "Content-Data", "content.items"),
            new("Data/StageData.asset", "Content-Data", "content.stage"),
            new("Data/Skills", "Content-Data", "content.skills"),
            new("Localization", "Content-Localization", "content.localization")
        };

        static AddressablesMigration()
        {
            EditorApplication.delayCall += RunOnceAfterImport;
        }

        private static void RunOnceAfterImport()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null && settings.FindGroup("Content-Data") == null) Run();
        }

        [MenuItem("Tools/Tiny Legends/Migrate Game Content to Addressables")]
        public static void Run()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("AddressableAssetSettings가 없습니다.");

            EnsureFolder("Assets", "AddressableContent");
            foreach (var rule in Rules) MigrateRule(settings, rule);
            RegisterExistingBgm(settings);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Addressables] 게임 콘텐츠 이전 및 그룹 정리가 완료되었습니다.");
        }

        public static void RunFromCommandLine() { Run(); }

        private static void MigrateRule(AddressableAssetSettings settings, Rule rule)
        {
            var source = SourceRoot + rule.Source;
            var target = TargetRoot + rule.Source;
            if (AssetDatabase.IsValidFolder(source))
            {
                EnsureParentFolder(target);
                MoveIfNeeded(source, target);
                foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { target }))
                    Register(settings, guid, AddressFor(AssetDatabase.GUIDToAssetPath(guid)), rule.Group, rule.Label);
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(source) != null)
            {
                EnsureParentFolder(target);
                MoveIfNeeded(source, target);
            }
            var path = AssetDatabase.LoadMainAssetAtPath(target) != null ? target : source;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                Register(settings, AssetDatabase.AssetPathToGUID(path), AddressFor(path), rule.Group, rule.Label);
        }

        private static void RegisterExistingBgm(AddressableAssetSettings settings)
        {
            const string root = "Assets/Resources_moved/Sounds";
            if (!AssetDatabase.IsValidFolder(root)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { root }))
                Register(settings, guid, "Sounds/" + Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)), "Content-Audio", "content.bgm");
        }

        private static void Register(AddressableAssetSettings settings, string guid, string address, string groupName, string label)
        {
            var group = settings.FindGroup(groupName) ?? settings.CreateGroup(groupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            settings.AddLabel(label);
            entry.SetLabel(label, true, true, false);
        }

        private static string AddressFor(string path)
        {
            var relative = path.Substring(TargetRoot.Length);
            var extension = Path.GetExtension(relative);
            return relative.Substring(0, relative.Length - extension.Length).Replace('\\', '/');
        }

        private static void MoveIfNeeded(string source, string target)
        {
            if (AssetDatabase.LoadMainAssetAtPath(target) != null || AssetDatabase.IsValidFolder(target)) return;
            var error = AssetDatabase.MoveAsset(source, target);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException($"{source} -> {target}: {error}");
        }

        private static void EnsureParentFolder(string assetPath)
        {
            var parts = Path.GetDirectoryName(assetPath)?.Replace('\\', '/').Split('/');
            if (parts == null || parts.Length == 0) return;
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
