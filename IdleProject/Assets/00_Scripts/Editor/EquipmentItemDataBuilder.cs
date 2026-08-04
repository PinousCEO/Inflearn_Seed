using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IdleBattle;
using UnityEditor;
using UnityEngine;

namespace IdleBattleEditor
{
    public static class EquipmentItemDataBuilder
    {
        private const string IconFolder = "Assets/05_Resources/UI/BrightTheme/Equipment/Items";
        private const string ItemFolder = "Assets/00_Data/Items/Equipment";
        private const string CatalogFolder = "Assets/Resources/Data";
        private const string CatalogPath = CatalogFolder + "/ItemCatalog.asset";
        private const string StateFolder = "Assets/05_Resources/UI/BrightTheme/Recreated/States";
        private const string FrameFolder = "Assets/05_Resources/UI/BrightTheme/Recreated/Equipment/Frames";
        // Lets the open editor rebuild the runtime catalog after a script reload.
        private const string RequestPath = "Temp/BuildEquipmentItemData.request";

        private static readonly Dictionary<string, string> KoreanNames = new()
        {
            ["MonsterEssence"] = "몬스터 정수", ["GreaterMonsterEssence"] = "상급 몬스터 정수",
            ["IronAxe"] = "무쇠 전투도끼", ["PlateHelmet"] = "강철 판금 투구",
            ["PlateArmor"] = "강철 판금 갑옷", ["PlatePauldrons"] = "강철 판금 견갑",
            ["PlateGauntlets"] = "강철 판금 건틀릿", ["PlateBelt"] = "강철 판금 허리띠",
            ["PlateBoots"] = "강철 판금 장화", ["RingOfStrength"] = "힘의 반지",
            ["RingOfDefense"] = "수호의 반지", ["RingOfLife"] = "생명의 반지",
            ["NecklaceOfRage"] = "격노의 목걸이", ["NecklaceOfGuarding"] = "철벽의 목걸이",
            ["NecklaceOfLife"] = "생명의 목걸이", ["LegendaryInfernoAxe"] = "전설의 지옥불 도끼",
            ["LegendaryFrostAxe"] = "전설의 서리 도끼", ["LegendaryLionguardHelmet"] = "전설의 사자수호 투구",
            ["LegendaryRavenHelmet"] = "전설의 까마귀 투구", ["LegendaryCrystalFortressArmor"] = "전설의 수정요새 갑옷",
            ["LegendaryBoneWardenArmor"] = "전설의 뼈감시자 갑옷", ["LegendaryStormPauldrons"] = "전설의 폭풍 견갑",
            ["LegendaryThornPauldrons"] = "전설의 가시 견갑", ["LegendaryMagmaGauntlets"] = "전설의 용암 건틀릿",
            ["LegendaryRuneGauntlets"] = "전설의 룬 건틀릿", ["LegendaryTitanBelt"] = "전설의 거신 허리띠",
            ["LegendarySerpentBelt"] = "전설의 뱀 허리띠", ["LegendaryWingedBoots"] = "전설의 비익 장화",
            ["LegendaryStonebreakerBoots"] = "전설의 바위분쇄 장화", ["LegendaryDragonEyeRing"] = "전설의 용안 반지",
            ["LegendaryVoidStarRing"] = "전설의 공허별 반지", ["LegendaryPhoenixNecklace"] = "전설의 불사조 목걸이",
            ["LegendaryLeviathanNecklace"] = "전설의 레비아탄 목걸이", ["MonsterCore"] = "몬스터 핵",
            ["CondensedMonsterCore"] = "응축된 몬스터 핵", ["MagicCrystal"] = "마력 수정",
            ["CorruptedMagicCrystal"] = "타락한 마력 수정", ["SoulDust"] = "영혼 가루",
            ["AncientSoulFragment"] = "고대 영혼 파편", ["LeatherHelmet"] = "가죽 두건",
            ["LeatherArmor"] = "가죽 갑옷", ["LeatherPauldrons"] = "가죽 견갑",
            ["LeatherGauntlets"] = "가죽 장갑", ["LeatherBelt"] = "가죽 허리띠",
            ["LeatherBoots"] = "가죽 장화", ["RobeHelmet"] = "비전 후드",
            ["RobeArmor"] = "비전 로브", ["RobePauldrons"] = "비전 어깨장식",
            ["RobeGauntlets"] = "비전 장갑", ["RobeBelt"] = "비전 허리띠",
            ["RobeBoots"] = "비전 장화", ["LegendaryNightfoxCap"] = "전설의 밤여우 모자",
            ["LegendaryThunderhawkCap"] = "전설의 뇌조 모자", ["LegendaryBloodthornJerkin"] = "전설의 피가시 조끼",
            ["LegendaryFrostbackJerkin"] = "전설의 서리등 조끼", ["LegendaryScorpionPauldrons"] = "전설의 전갈 견갑",
            ["LegendaryMoonfeatherPauldrons"] = "전설의 달깃털 견갑", ["LegendaryVenomclawGloves"] = "전설의 독발톱 장갑",
            ["LegendarySunshotGloves"] = "전설의 태양사격 장갑", ["LegendaryMimicBelt"] = "전설의 미믹 허리띠",
            ["LegendaryHourglassBelt"] = "전설의 모래시계 허리띠", ["LegendaryGhoststepBoots"] = "전설의 유령걸음 장화",
            ["LegendaryVolcanicTreadBoots"] = "전설의 화산길 장화", ["LegendaryAstralSeerHat"] = "전설의 성좌예언 모자",
            ["LegendaryMushroomSageHat"] = "전설의 버섯현자 모자", ["LegendaryChronomancerRobe"] = "전설의 시간술사 로브",
            ["LegendaryGravebloomRobe"] = "전설의 무덤꽃 로브", ["LegendaryPhoenixMantles"] = "전설의 불사조 망토",
            ["LegendaryVoidEyeMantles"] = "전설의 공허눈 망토", ["LegendaryStormcallerGloves"] = "전설의 폭풍소환 장갑",
            ["LegendaryAlchemistGloves"] = "전설의 연금술사 장갑", ["LegendaryCelestialSash"] = "전설의 천상 허리띠",
            ["LegendaryBookwyrmSash"] = "전설의 책룡 허리띠", ["LegendaryCloudwalkerShoes"] = "전설의 구름걸음 신발",
            ["LegendaryAbyssalShoes"] = "전설의 심연 신발"
        };

        [InitializeOnLoadMethod]
        private static void BuildWhenRequested()
        {
            if (!File.Exists(RequestPath)) return;
            EditorApplication.delayCall += () =>
            {
                Build();
                File.Delete(RequestPath);
            };
        }

        [MenuItem("Tools/Idle Battle/Build Equipment Item Data")]
        public static void Build()
        {
            EnsureFolder(ItemFolder);
            var icons = Directory.GetFiles(IconFolder, "Item_*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(GetIndex)
                .ToArray();

            foreach (var iconPathRaw in icons)
            {
                var iconPath = iconPathRaw.Replace('\\', '/');
                var fileName = Path.GetFileNameWithoutExtension(iconPath);
                var match = Regex.Match(fileName, @"^Item_(\d+)_(.+)$");
                if (!match.Success) continue;

                var index = int.Parse(match.Groups[1].Value);
                var key = match.Groups[2].Value;
                var assetPath = $"{ItemFolder}/equipment-{index:000}.asset";
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<ItemData>();
                    AssetDatabase.CreateAsset(item, assetPath);
                }

                Configure(item, index, key, AssetDatabase.LoadAssetAtPath<Sprite>(iconPath));
                EditorUtility.SetDirty(item);
            }

            AssetDatabase.SaveAssets();
            BuildCatalog();
            AssetDatabase.Refresh();
            Debug.Log($"Built {icons.Length} Equipment ItemData assets in {ItemFolder}.");
        }

        private static void BuildCatalog()
        {
            EnsureFolder(CatalogFolder);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var items = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/00_Data/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>);
            catalog.SetItems(items);
            catalog.SetUiSprites(
                new[]
                {
                    LoadSprite($"{StateFolder}/Rarity_Common.png"),
                    LoadSprite($"{StateFolder}/Rarity_Rare.png"),
                    LoadSprite($"{StateFolder}/Rarity_Epic.png"),
                    LoadSprite($"{StateFolder}/Rarity_Legendary.png"),
                    LoadSprite($"{StateFolder}/Rarity_Unique.png")
                },
                LoadSprite($"{FrameFolder}/Equipment_Tab_Normal.png"),
                LoadSprite($"{FrameFolder}/Equipment_Tab_Selected.png"));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void Configure(ItemData item, int index, string key, Sprite icon)
        {
            var type = IsMaterial(key) ? ItemType.Material : ItemType.Equipment;
            var rarity = GetRarity(index, key);
            var slot = type == ItemType.Equipment ? GetSlot(key) : EquipmentSlot.None;
            var name = KoreanNames.TryGetValue(key, out var localized) ? localized : SplitWords(key);
            var serialized = new SerializedObject(item);

            serialized.FindProperty("itemId").stringValue = $"equipment-{index:000}";
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("description").stringValue = BuildDescription(name, key, type, slot, rarity);
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("itemType").enumValueIndex = (int)type;
            serialized.FindProperty("rarity").enumValueIndex = (int)rarity;
            var tier = (int)rarity + 1;
            serialized.FindProperty("buyPrice").intValue = type == ItemType.Material ? 40 * tier : 180 * tier * tier;
            serialized.FindProperty("sellPrice").intValue = type == ItemType.Material ? 12 * tier : 55 * tier * tier;
            serialized.FindProperty("maxStack").intValue = type == ItemType.Material ? 999 : 1;
            serialized.FindProperty("dropRatePercent").floatValue = GetDropRate(rarity, type);
            serialized.FindProperty("equipmentSlot").enumValueIndex = (int)slot;

            var modifiers = serialized.FindProperty("statModifiers");
            modifiers.ClearArray();
            if (type == ItemType.Equipment)
            {
                var generated = BuildStats(slot, key, rarity);
                for (var i = 0; i < generated.Count; i++)
                {
                    modifiers.InsertArrayElementAtIndex(i);
                    var modifier = modifiers.GetArrayElementAtIndex(i);
                    modifier.FindPropertyRelative("type").enumValueIndex = (int)generated[i].type;
                    modifier.FindPropertyRelative("mode").enumValueIndex = (int)generated[i].mode;
                    modifier.FindPropertyRelative("value").floatValue = generated[i].value;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<StatModifier> BuildStats(EquipmentSlot slot, string key, ItemRarity rarity)
        {
            var tier = (int)rarity + 1;
            var result = new List<StatModifier>();
            switch (slot)
            {
                case EquipmentSlot.Weapon: result.Add(Flat(StatType.AttackPower, 7 * tier)); break;
                case EquipmentSlot.Helmet: result.Add(Flat(StatType.Defense, 4 * tier)); break;
                case EquipmentSlot.Armor: result.Add(Flat(StatType.MaxHealth, 18 * tier)); break;
                case EquipmentSlot.Belt: result.Add(Flat(StatType.MaxHealth, 12 * tier)); break;
                case EquipmentSlot.Gloves: result.Add(Percent(StatType.AttackSpeed, 2.5f * tier)); break;
                case EquipmentSlot.Boots: result.Add(Percent(StatType.MovementSpeed, 2.5f * tier)); break;
                case EquipmentSlot.Ring: result.Add(Percent(StatType.CriticalChance, 1.5f * tier)); break;
                case EquipmentSlot.Necklace: result.Add(Percent(StatType.SkillDamage, 3f * tier)); break;
            }

            if (rarity >= ItemRarity.Epic)
                result.Add(GetThemeStat(key, tier));
            return result;
        }

        private static StatModifier GetThemeStat(string key, int tier)
        {
            if (ContainsAny(key, "Inferno", "Magma", "Phoenix", "Volcanic", "Sun"))
                return Percent(StatType.FireResistance, 3f * tier);
            if (ContainsAny(key, "Frost", "Crystal", "Cloud"))
                return Percent(StatType.ColdResistance, 3f * tier);
            if (ContainsAny(key, "Storm", "Thunder"))
                return Percent(StatType.LightningResistance, 3f * tier);
            if (ContainsAny(key, "Void", "Abyss", "Grave", "Venom"))
                return Percent(StatType.ChaosResistance, 3f * tier);
            if (ContainsAny(key, "Winged", "Ghoststep", "Moonfeather"))
                return Percent(StatType.MovementSpeed, 2f * tier);
            if (ContainsAny(key, "Hourglass", "Chronomancer"))
                return Percent(StatType.CooldownReduction, 2f * tier);
            return Percent(StatType.MagicFind, 2f * tier);
        }

        private static string BuildDescription(string name, string key, ItemType type, EquipmentSlot slot, ItemRarity rarity)
        {
            if (type == ItemType.Material)
                return $"{name}. 몬스터에게서 얻을 수 있는 신비한 재료로, 장비 제작과 강화에 사용된다.";

            var slotText = slot switch
            {
                EquipmentSlot.Weapon => "강력한 일격을 위한 무기",
                EquipmentSlot.Helmet => "정신과 머리를 지켜 주는 방어구",
                EquipmentSlot.Armor => "치명적인 공격을 버텨 내는 방어구",
                EquipmentSlot.Belt => "전투 자세를 안정시키는 허리 장비",
                EquipmentSlot.Gloves => "민첩하고 정확한 공격을 돕는 손 장비",
                EquipmentSlot.Boots => "빠르고 안전한 이동을 돕는 신발",
                EquipmentSlot.Ring => "전투 감각을 끌어올리는 반지",
                EquipmentSlot.Necklace => "잠든 마력을 깨우는 목걸이",
                _ => "모험가를 위한 장비"
            };
            var legend = rarity == ItemRarity.Legendary ? " 전설 속 영웅의 힘이 아직도 선명하게 맥동한다." : string.Empty;
            return $"{name}. {slotText}다.{legend}";
        }

        private static ItemRarity GetRarity(int index, string key)
        {
            if (key.StartsWith("Legendary", StringComparison.Ordinal)) return ItemRarity.Legendary;
            if (index is 2 or 35 or 37 or 39) return ItemRarity.Epic;
            if (index is 15 or 34 or 36 or 38 || key.StartsWith("Robe")) return ItemRarity.Rare;
            if (index is 1 or 40 or 41 or 42 or 43 or 44 or 45) return ItemRarity.Common;
            return ItemRarity.Uncommon;
        }

        private static float GetDropRate(ItemRarity rarity, ItemType type)
        {
            var rate = rarity switch
            {
                ItemRarity.Common => 2.5f,
                ItemRarity.Uncommon => 1.2f,
                ItemRarity.Rare => .55f,
                ItemRarity.Epic => .18f,
                _ => .04f
            };
            return type == ItemType.Material ? rate * 2.25f : rate;
        }

        private static EquipmentSlot GetSlot(string key)
        {
            if (ContainsAny(key, "Axe")) return EquipmentSlot.Weapon;
            if (ContainsAny(key, "Helmet", "Cap", "Hat", "Hood")) return EquipmentSlot.Helmet;
            if (ContainsAny(key, "Armor", "Jerkin", "Robe", "Pauldrons", "Mantles")) return EquipmentSlot.Armor;
            if (ContainsAny(key, "Gauntlets", "Gloves")) return EquipmentSlot.Gloves;
            if (ContainsAny(key, "Belt", "Sash")) return EquipmentSlot.Belt;
            if (ContainsAny(key, "Boots", "Shoes")) return EquipmentSlot.Boots;
            if (key.Contains("Ring")) return EquipmentSlot.Ring;
            if (key.Contains("Necklace")) return EquipmentSlot.Necklace;
            return EquipmentSlot.None;
        }

        private static bool IsMaterial(string key) => ContainsAny(key, "Essence", "Core", "Crystal", "Dust", "Fragment");
        private static StatModifier Flat(StatType type, float value) => new(type, StatModifierMode.Flat, value);
        private static StatModifier Percent(StatType type, float value) => new(type, StatModifierMode.AdditivePercent, value);
        private static bool ContainsAny(string value, params string[] words) => words.Any(value.Contains);
        private static string SplitWords(string value) => Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");

        private static int GetIndex(string path)
        {
            var match = Regex.Match(Path.GetFileName(path), @"^Item_(\d+)_");
            return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
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
