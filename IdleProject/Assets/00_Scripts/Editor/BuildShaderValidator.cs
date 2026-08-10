using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleBattle.Editor
{
    /// <summary>Stops a player build before packaging assets with missing or unsupported shaders.</summary>
    public sealed class BuildShaderValidator : IPreprocessBuildWithReport
    {
        private const string RuntimeLitPath = "Assets/Resources/Runtime/RuntimeLit.mat";
        private static readonly string[] RequiredResourcePrefabs =
        {
            "Character", "Enemy", "UI/Damage", "UI/ItemDes",
            "Effects/Monsterzone", "Effects/Stun", "Effects/Hit", "Effects/Row",
            "Effects/Tornado_sand", "Effects/Tornado_snow", "Effects/EarthQuake",
            "Effects/Aura_slowdown", "Effects/8", "Effects/9", "Effects/Loot_pick_up",
            "Effects/Loot_Common", "Effects/Loot_Uncommon", "Effects/Loot_Rare",
            "Effects/Loot_Epic", "Effects/Loot_Legendary"
        };

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var failures = new List<string>();
            ValidateRuntimeLit(failures);
            ValidateRequiredResources(failures);
            ValidateMaterials(failures);
            ValidateAndroidGlesShaders(report.summary.platform, failures);

            if (failures.Count > 0)
            {
                throw new BuildFailedException(
                    "Shader validation failed before build:\n- " + string.Join("\n- ", failures));
            }

            Debug.Log("[BuildShaderValidator] Shader validation passed.");
        }

        private static void ValidateRequiredResources(List<string> failures)
        {
            const string root = "Assets/Resources/01_Prefabs/";
            foreach (string relativePath in RequiredResourcePrefabs)
            {
                string path = root + relativePath + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    failures.Add($"Required runtime prefab is missing: {path}");
                }
            }
        }

        private static void ValidateRuntimeLit(List<string> failures)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RuntimeLitPath);
            if (material == null || material.shader == null)
            {
                failures.Add($"Runtime Lit material is missing or invalid: {RuntimeLitPath}");
                return;
            }

            if (!material.shader.isSupported)
            {
                failures.Add($"Runtime shader is unsupported for the active build target: {material.shader.name}");
            }
        }

        private static void ValidateMaterials(List<string> failures)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;

                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                {
                    failures.Add($"Missing/error shader on material: {path}");
                }
                else if (!material.shader.isSupported)
                {
                    failures.Add($"Unsupported shader '{material.shader.name}' on material: {path}");
                }
            }
        }

        private static void ValidateAndroidGlesShaders(BuildTarget platform, List<string> failures)
        {
            if (platform != BuildTarget.Android) return;

            bool usesGles3 = false;
            foreach (GraphicsDeviceType api in PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))
            {
                if (api == GraphicsDeviceType.OpenGLES3)
                {
                    usesGles3 = true;
                    break;
                }
            }

            if (!usesGles3) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string source = File.ReadAllText(path);
                if (source.Contains("#pragma target 4.5") &&
                    source.Contains("For WebGL2/GLES3, please set your shader target to 3.5"))
                {
                    failures.Add($"GLES3-incompatible Shader Model 4.5 target: {path}");
                }
            }
        }
    }
}
