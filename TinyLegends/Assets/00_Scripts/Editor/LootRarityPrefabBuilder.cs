using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleBattle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleBattleEditor
{
    public static class LootRarityPrefabBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string SourcePath = "Assets/Lana Studio/Casual RPG VFX/Prefabs/Loot/Loot_iddle.prefab";
        private const string OutputFolder = "Assets/05_Resources/VFX/Loot";
        private const string RequestPath = "Temp/BuildLootRarityPrefabs.request";

        private readonly struct Tier
        {
            public readonly ItemRarity Rarity;
            public readonly string Name;
            public readonly Color Color;
            public readonly float Height;
            public readonly float Width;
            public readonly float Density;
            public readonly float Lifetime;
            public readonly float Speed;

            public Tier(ItemRarity rarity, string name, string color, float height, float width, float density,
                float lifetime, float speed)
            {
                Rarity = rarity;
                Name = name;
                ColorUtility.TryParseHtmlString(color, out var parsedColor);
                Color = parsedColor;
                Height = height;
                Width = width;
                Density = density;
                Lifetime = lifetime;
                Speed = speed;
            }
        }

        private static readonly Tier[] Tiers =
        {
            // Matches the Equipment States frames: white, blue, yellow, purple, orange.
            new(ItemRarity.Common, "Common", "#E8E8E8", .94f, .96f, .52f, .90f, .92f),
            new(ItemRarity.Uncommon, "Uncommon", "#4A9FE8", .97f, .98f, .72f, .95f, .96f),
            new(ItemRarity.Rare, "Rare", "#F2C14E", 1f, 1f, 1f, 1f, 1f),
            new(ItemRarity.Epic, "Epic", "#A765D1", 1.04f, 1.02f, 1.35f, 1.05f, 1.04f),
            new(ItemRarity.Legendary, "Legendary", "#F07845", 1.08f, 1.04f, 1.70f, 1.10f, 1.08f),
        };

        [InitializeOnLoadMethod]
        private static void BuildWhenRequested()
        {
            if (!File.Exists(RequestPath)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Build();
                File.Delete(RequestPath);
            };
        }

        [MenuItem("Tools/Idle Battle/Build Loot Rarity Prefabs")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new MissingReferenceException($"Loot source prefab not found: {SourcePath}");
            EnsureFolder(OutputFolder);

            var prefabs = new List<GameObject>();
            foreach (var tier in Tiers)
                prefabs.Add(BuildTier(source, tier));

            ReplaceSceneObjects(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("Built five rarity loot prefabs and replaced the five Main scene Loot_iddle objects.");
        }

        private static GameObject BuildTier(GameObject source, Tier tier)
        {
            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate loot source prefab.");
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = $"Loot_{tier.Name}";

            var marker = instance.GetComponent<LootRarityVfx>() ?? instance.AddComponent<LootRarityVfx>();
            marker.Configure(tier.Rarity, tier.Color);

            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var effectName = particle.name.ToLowerInvariant();
                var isBeam = effectName.Contains("line") || effectName.Contains("vertical");
                var isSpiral = effectName.Contains("spiral");
                var isBokeh = effectName.Contains("boke");
                var isPoints = effectName.Contains("point");

                particle.gameObject.SetActive(IsEnabled(tier.Rarity, effectName));
                if (!particle.gameObject.activeSelf) continue;

                var main = particle.main;
                // Keep the frame hue clearly visible instead of washing it out to white.
                var bright = Color.Lerp(tier.Color, Color.white, .08f);
                var dim = new Color(tier.Color.r, tier.Color.g, tier.Color.b, .72f);
                main.startColor = new ParticleSystem.MinMaxGradient(dim, bright);
                main.maxParticles = Mathf.Max(8, Mathf.RoundToInt(main.maxParticles * tier.Density));
                // Rarity is expressed mainly through effect count/density, not footprint.
                main.startSizeMultiplier *= isBeam ? tier.Width : Mathf.Lerp(.96f, 1.06f, (float)tier.Rarity / 4f);
                main.startLifetimeMultiplier *= tier.Lifetime;
                main.startSpeedMultiplier *= tier.Speed;

                var emission = particle.emission;
                if (emission.enabled)
                {
                    emission.rateOverTimeMultiplier *= tier.Density;
                    emission.rateOverDistanceMultiplier *= tier.Density;
                }

                var localScale = particle.transform.localScale;
                if (isBeam)
                {
                    // Most source emitters point along local Z and are rotated
                    // -90 degrees; the static billboards point along local Y.
                    var rotatedEmitter = Mathf.Abs(Mathf.DeltaAngle(
                        particle.transform.localEulerAngles.x, 0f)) > 45f;
                    particle.transform.localScale = rotatedEmitter
                        ? new Vector3(localScale.x * tier.Width, localScale.y * tier.Width,
                            localScale.z * tier.Height)
                        : new Vector3(localScale.x * tier.Width, localScale.y * tier.Height,
                            localScale.z * tier.Width);
                    var localPosition = particle.transform.localPosition;
                    localPosition.y *= tier.Height;
                    particle.transform.localPosition = localPosition;
                }
                else if (isSpiral || isBokeh || isPoints)
                    particle.transform.localScale = localScale * Mathf.Lerp(.72f, 1.28f, (float)tier.Rarity / 4f);

                EditorUtility.SetDirty(particle);
            }

            var path = $"{OutputFolder}/Loot_{tier.Name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static bool IsEnabled(ItemRarity rarity, string effectName)
        {
            if (effectName.Contains("spiral2")) return (int)rarity >= (int)ItemRarity.Epic;
            if (effectName.Contains("spiral1")) return (int)rarity >= (int)ItemRarity.Rare;
            if (effectName.Contains("boke")) return (int)rarity >= (int)ItemRarity.Rare;
            if (effectName.Contains("point")) return (int)rarity >= (int)ItemRarity.Uncommon;
            return true;
        }

        private static void ReplaceSceneObjects(IReadOnlyList<GameObject> prefabs)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var oldObjects = scene.GetRootGameObjects()
                .Where(x => x.name.StartsWith("Loot_iddle", StringComparison.OrdinalIgnoreCase) ||
                            x.name.StartsWith("Loot_Idle", StringComparison.OrdinalIgnoreCase))
                .OrderBy(LootOrder)
                .Take(prefabs.Count)
                .ToArray();
            if (oldObjects.Length == 0)
            {
                var existing = scene.GetRootGameObjects().Count(x =>
                    Tiers.Any(tier => x.name == $"Loot_{tier.Name}"));
                if (existing == prefabs.Count) return;
            }
            if (oldObjects.Length != prefabs.Count)
                throw new InvalidOperationException($"Expected five Loot_iddle roots in Main.unity, found {oldObjects.Length}.");

            for (var i = 0; i < prefabs.Count; i++)
            {
                var old = oldObjects[i];
                var position = old.transform.position;
                var rotation = old.transform.rotation;
                var scale = old.transform.localScale;
                var active = old.activeSelf;
                var sibling = old.transform.GetSiblingIndex();
                UnityEngine.Object.DestroyImmediate(old);

                var replacement = PrefabUtility.InstantiatePrefab(prefabs[i], scene) as GameObject;
                replacement.transform.SetPositionAndRotation(position, rotation);
                replacement.transform.localScale = scale;
                replacement.transform.SetSiblingIndex(sibling);
                replacement.SetActive(active);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static int LootOrder(GameObject gameObject)
        {
            var open = gameObject.name.LastIndexOf('(');
            if (open < 0) return 0;
            var value = gameObject.name.Substring(open + 1).TrimEnd(')');
            return int.TryParse(value, out var parsed) ? parsed : 0;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
