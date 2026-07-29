using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.Editor
{
    public static class EquipmentPanelBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string EquipmentAssetPath = "Assets/05_Resources/UI/Equipments";
        // A request file is used so an already-open Unity editor can rebuild once after domain reload.
        private const string AutoBuildRequestPath = "Temp/BuildEquipmentPanel.request";

        private static readonly Color Background = Hex("08090C");
        private static readonly Color Panel = Hex("121318");
        private static readonly Color PanelRaised = Hex("1A1B21");
        private static readonly Color Gold = Hex("D5A94E");
        private static readonly Color TextPrimary = Hex("F2E7CE");
        private static readonly Color TextMuted = Hex("A69A83");
        private static TMP_FontAsset referenceFont;

        [InitializeOnLoadMethod]
        private static void BuildWhenRequested()
        {
            if (!File.Exists(AutoBuildRequestPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode ||
                    EditorSceneManager.GetActiveScene().path != ScenePath)
                {
                    Debug.LogWarning(
                        "Equipment panel build request is waiting because Main.unity is not the active edit-mode scene.");
                    return;
                }

                Build();
                File.Delete(AutoBuildRequestPath);
            };
        }

        [MenuItem("Tools/Idle Battle/Build Equipment Panel")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value => value.name == "Canvas");

            if (canvas == null)
            {
                throw new InvalidOperationException("Main scene Canvas was not found.");
            }

            var old = canvas.transform.Find("Equipment");
            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            referenceFont = canvas.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Select(text => text.font)
                .FirstOrDefault(font => font != null);

            var root = UI("Equipment", canvas.transform);
            Stretch(root);

            var background = Image("Background", root.transform, Background);
            Stretch(background.rectTransform);

            var safeArea = UI("SafeArea", root.transform);
            var safeRect = safeArea.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = new Vector2(25f, 40f);
            safeRect.offsetMax = new Vector2(-25f, -40f);
            safeRect.pivot = new Vector2(0.5f, 0.5f);

            BuildHeader(safeArea.transform, referenceFont);
            BuildCharacterOverview(safeArea.transform, referenceFont);
            BuildStats(safeArea.transform, referenceFont);
            BuildInventory(safeArea.transform, referenceFont);
            BuildBottomNavigation(safeArea.transform, referenceFont);

            root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Equipment panel hierarchy was created under Canvas and saved to Main.unity.");
        }

        private static void BuildHeader(Transform parent, TMP_FontAsset font)
        {
            var header = UI("Header", parent);
            SetRect(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(1010f, 190f));
            StretchHorizontal(header.GetComponent<RectTransform>(), -100f, 190f, true);

            var profile = PanelObject("ProfileCard", header.transform, Hex("0C0D10"),
                new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(440f, 126f));
            AddGoldOutline(profile);
            Text("ClassIcon", profile.transform, "⚔", font, 46f, FontStyles.Bold,
                TextAlignmentOptions.Center, Gold,
                new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(94f, 94f));
            Text("CharacterName", profile.transform, "그림자 사냥꾼", font, 27f,
                FontStyles.Bold, TextAlignmentOptions.Left, TextPrimary,
                new Vector2(0f, 1f), new Vector2(150f, -34f), new Vector2(230f, 40f));
            Text("Level", profile.transform, "Lv.53", font, 23f,
                FontStyles.Bold, TextAlignmentOptions.Right, Gold,
                new Vector2(1f, 1f), new Vector2(-58f, -34f), new Vector2(100f, 38f));
            Text("Experience", profile.transform, "EXP 53.4%", font, 20f,
                FontStyles.Bold, TextAlignmentOptions.Left, Hex("B7D879"),
                new Vector2(0f, 0f), new Vector2(160f, 25f), new Vector2(235f, 34f));
            var expTrack = Image("ExpTrack", profile.transform, Hex("29251D"));
            SetRect(expTrack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(150f, 62f), new Vector2(245f, 12f));
            var expFill = Image("ExpFill", expTrack.transform, Hex("459548"));
            expFill.rectTransform.anchorMin = Vector2.zero;
            expFill.rectTransform.anchorMax = new Vector2(0.534f, 1f);
            expFill.rectTransform.offsetMin = new Vector2(2f, 2f);
            expFill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            var currency = UI("CurrencyGroup", header.transform);
            SetRect(currency, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-190f, -32f), new Vector2(380f, 68f));
            BuildCurrency("Gold", currency.transform, "●  1,302,325", Gold, font, -98f);
            BuildCurrency("Gem", currency.transform, "◆  1,302,325", Hex("4AA7FF"), font, 98f);

            var utility = UI("UtilityButtons", header.transform);
            SetRect(utility, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-145f, 35f), new Vector2(300f, 70f));
            var utilityLabels = new[] { "✉", "▣", "⚙" };
            for (var i = 0; i < utilityLabels.Length; i++)
            {
                ButtonObject($"Utility_{i + 1:00}", utility.transform, utilityLabels[i], font,
                    new Vector2(0f, 0.5f), new Vector2(34f + i * 100f, 0f), new Vector2(78f, 64f));
            }

            Text("Title", parent, "장비", font, 45f, FontStyles.Bold,
                TextAlignmentOptions.Center, TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(260f, 72f));
        }

        private static void BuildCurrency(
            string name,
            Transform parent,
            string value,
            Color accent,
            TMP_FontAsset font,
            float x)
        {
            var item = PanelObject(name, parent, Hex("0E0F13"), new Vector2(0.5f, 0.5f),
                new Vector2(x, 0f), new Vector2(170f, 64f));
            var outline = item.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            outline.effectDistance = new Vector2(1f, -1f);
            Text("Value", item.transform, value, font, 21f, FontStyles.Bold,
                TextAlignmentOptions.Center, TextPrimary,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(158f, 54f));
        }

        private static void BuildCharacterOverview(Transform parent, TMP_FontAsset font)
        {
            var overview = PanelObject("CharacterOverview", parent, Panel,
                new Vector2(0.5f, 1f), new Vector2(0f, -455f), new Vector2(1010f, 440f));
            StretchHorizontal(overview.GetComponent<RectTransform>(), -455f, 440f, true);
            AddGoldOutline(overview);

            var power = PanelObject("CombatPower", parent, Hex("0D0E11"),
                new Vector2(1f, 1f), new Vector2(-175f, -205f), new Vector2(330f, 58f));
            AddGoldOutline(power);
            Text("Label", power.transform, "전투력", font, 20f, FontStyles.Normal,
                TextAlignmentOptions.Left, TextMuted,
                new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(95f, 44f));
            Text("Value", power.transform, "137,420", font, 27f, FontStyles.Bold,
                TextAlignmentOptions.Right, TextPrimary,
                new Vector2(1f, 0.5f), new Vector2(-92f, 0f), new Vector2(165f, 44f));

            var preview = PanelObject("CharacterPreview", overview.transform, Hex("0A0B0D"),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(610f, 408f));
            var previewOutline = preview.AddComponent<Outline>();
            previewOutline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.42f);
            previewOutline.effectDistance = new Vector2(2f, -2f);
            Text("Placeholder", preview.transform, "CHARACTER\nPREVIEW", font, 24f,
                FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.5f, 0.46f, 0.38f, 0.65f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 150f));

            var equipmentSprites = LoadItemSprites();
            BuildEquippedColumn("LeftEquipmentSlots", overview.transform, -421f,
                new[] { equipmentSprites.ElementAtOrDefault(2), equipmentSprites.ElementAtOrDefault(3), equipmentSprites.ElementAtOrDefault(4) });
            BuildEquippedColumn("RightEquipmentSlots", overview.transform, 421f,
                new[] { equipmentSprites.ElementAtOrDefault(5), equipmentSprites.ElementAtOrDefault(6), equipmentSprites.ElementAtOrDefault(8) });
        }

        private static void BuildEquippedColumn(string name, Transform parent, float x, Sprite[] icons)
        {
            var column = UI(name, parent);
            SetRect(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 0f), new Vector2(140f, 410f));

            for (var i = 0; i < icons.Length; i++)
            {
                var slot = CreateItemSlot($"EquippedSlot_{i + 1:00}", column.transform,
                    icons[i], i == 0 ? RarityColor(4) : RarityColor(2), true, $"+{8 - i}");
                SetRect(slot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -69f - i * 134f), new Vector2(120f, 120f));
            }
        }

        private static void BuildStats(Transform parent, TMP_FontAsset font)
        {
            var stats = PanelObject("Stats", parent, Hex("0D0E11"),
                new Vector2(0.5f, 1f), new Vector2(0f, -880f), new Vector2(1010f, 340f));
            StretchHorizontal(stats.GetComponent<RectTransform>(), -880f, 340f, true);
            AddGoldOutline(stats);

            Text("Title", stats.transform, "능력치", font, 30f, FontStyles.Bold,
                TextAlignmentOptions.Left, Gold,
                new Vector2(0f, 1f), new Vector2(88f, -36f), new Vector2(180f, 48f));
            ButtonObject("ViewAllButton", stats.transform, "전체 보기", font,
                new Vector2(1f, 1f), new Vector2(-94f, -36f), new Vector2(170f, 48f));

            var summaries = new[]
            {
                ("공격력", "142,880", "⚔"),
                ("방어력", "38,240", "▣"),
                ("최대 체력", "1.24M", "♥"),
                ("치명타", "64.2%", "✦")
            };
            for (var i = 0; i < summaries.Length; i++)
            {
                var card = PanelObject($"Summary_{i + 1:00}_{summaries[i].Item1}", stats.transform,
                    Panel, new Vector2(0f, 1f), new Vector2(120f + i * 245f, -105f),
                    new Vector2(225f, 88f));
                Text("Icon", card.transform, summaries[i].Item3, font, 28f, FontStyles.Bold,
                    TextAlignmentOptions.Center, TextMuted,
                    new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(50f, 55f));
                Text("Label", card.transform, summaries[i].Item1, font, 18f, FontStyles.Normal,
                    TextAlignmentOptions.Left, TextMuted,
                    new Vector2(0f, 1f), new Vector2(80f, -23f), new Vector2(125f, 30f));
                Text("Value", card.transform, summaries[i].Item2, font, 25f, FontStyles.Bold,
                    TextAlignmentOptions.Left, TextPrimary,
                    new Vector2(0f, 0f), new Vector2(82f, 24f), new Vector2(125f, 34f));
            }

            var tabs = UI("StatTabs", stats.transform);
            SetRect(tabs, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -176f), new Vector2(950f, 54f));
            var labels = new[] { "공격", "방어", "특수" };
            for (var i = 0; i < labels.Length; i++)
            {
                var tab = ButtonObject($"Tab_{labels[i]}", tabs.transform, labels[i], font,
                    new Vector2(0f, 0.5f), new Vector2(158f + i * 317f, 0f), new Vector2(305f, 50f));
                tab.GetComponent<Image>().color = i == 0 ? Hex("382710") : Hex("17171A");
            }

            var detail = PanelObject("StatDetails", stats.transform, Hex("0A0B0D"),
                new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(950f, 112f));
            var detailRows = new[]
            {
                ("공격력", "142,880", "주문력", "96,410"),
                ("공격 속도", "+38.4%", "치명타 확률", "61.2%"),
                ("치명 피해", "+286%", "방어 관통", "22.4%")
            };
            for (var i = 0; i < detailRows.Length; i++)
            {
                Text($"LeftLabel_{i}", detail.transform, detailRows[i].Item1, font, 17f,
                    FontStyles.Normal, TextAlignmentOptions.Left, TextMuted,
                    new Vector2(0f, 1f), new Vector2(85f, -22f - i * 34f), new Vector2(130f, 26f));
                Text($"LeftValue_{i}", detail.transform, detailRows[i].Item2, font, 18f,
                    FontStyles.Normal, TextAlignmentOptions.Right, Hex("72CF69"),
                    new Vector2(0.5f, 1f), new Vector2(-70f, -22f - i * 34f), new Vector2(150f, 26f));
                Text($"RightLabel_{i}", detail.transform, detailRows[i].Item3, font, 17f,
                    FontStyles.Normal, TextAlignmentOptions.Left, TextMuted,
                    new Vector2(0.5f, 1f), new Vector2(70f, -22f - i * 34f), new Vector2(140f, 26f));
                Text($"RightValue_{i}", detail.transform, detailRows[i].Item4, font, 18f,
                    FontStyles.Normal, TextAlignmentOptions.Right, Hex("72CF69"),
                    new Vector2(1f, 1f), new Vector2(-90f, -22f - i * 34f), new Vector2(160f, 26f));
            }
        }

        private static void BuildInventory(Transform parent, TMP_FontAsset font)
        {
            var inventory = PanelObject("Inventory", parent, Panel,
                new Vector2(0.5f, 0f), new Vector2(0f, 212f), new Vector2(1010f, 440f));
            StretchHorizontal(inventory.GetComponent<RectTransform>(), 212f, 440f, false);
            AddGoldOutline(inventory);

            var tabs = UI("CategoryTabs", inventory.transform);
            SetRect(tabs, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(950f, 58f));
            var tabLabels = new[] { "전체", "무기", "방어구", "장신구" };
            for (var i = 0; i < tabLabels.Length; i++)
            {
                var button = ButtonObject($"Tab_{tabLabels[i]}", tabs.transform, tabLabels[i], font,
                    new Vector2(0f, 0.5f), new Vector2(120f + i * 238f, 0f), new Vector2(226f, 50f));
                button.GetComponent<Image>().color = i == 0 ? Hex("4A3518") : Hex("18191E");
            }

            var header = UI("InventoryHeader", inventory.transform);
            SetRect(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -94f), new Vector2(950f, 58f));
            Text("Title", header.transform, "보유 18 / 21", font, 25f, FontStyles.Bold,
                TextAlignmentOptions.Left, TextPrimary,
                new Vector2(0f, 0.5f), new Vector2(125f, 0f), new Vector2(250f, 50f));
            ButtonObject("SortButton", header.transform, "정렬", font,
                new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(132f, 48f));
            ButtonObject("BulkUpgradeButton", header.transform, "일괄 강화", font,
                new Vector2(1f, 0.5f), new Vector2(-158f, 0f), new Vector2(142f, 48f));
            ButtonObject("AutoEquipButton", header.transform, "자동 장착", font,
                new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(148f, 48f));

            var scrollView = UI("ScrollView", inventory.transform);
            SetRect(scrollView, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(950f, 270f));
            var scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 42f;

            var viewport = PanelObject("Viewport", scrollView.transform, Hex("0C0D10"),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(950f, 270f));
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            var content = UI("Content", viewport.transform);
            SetRect(content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -225f), new Vector2(930f, 450f));
            scrollRect.content = content.GetComponent<RectTransform>();

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(119f, 119f);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(13, 13, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 7;
            grid.childAlignment = TextAnchor.UpperCenter;

            var sprites = LoadItemSprites();
            for (var i = 0; i < Math.Min(21, sprites.Count); i++)
            {
                var rarity = i < 4 ? 0 : i < 10 ? 1 : i < 17 ? 2 : i < 21 ? 3 : 4;
                CreateItemSlot($"ItemSlot_{i + 1:00}", content.transform, sprites[i],
                    RarityColor(rarity), i % 7 == 0, $"+{(i % 9) + 1}");
            }
        }

        private static void BuildBottomNavigation(Transform parent, TMP_FontAsset font)
        {
            var navigation = PanelObject("BottomNavigation", parent, Hex("0D0E12"),
                new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(1010f, 132f));
            StretchHorizontal(navigation.GetComponent<RectTransform>(), 76f, 132f, false);
            AddGoldOutline(navigation);
            var labels = new[] { "장비", "던전", "메인", "스킬", "상점" };
            for (var i = 0; i < labels.Length; i++)
            {
                var button = ButtonObject($"Nav_{labels[i]}", navigation.transform, labels[i], font,
                    new Vector2(0f, 0.5f), new Vector2(101f + i * 202f, 0f), new Vector2(194f, 112f));
                button.GetComponent<Image>().color = i == 0 ? Hex("4A3518") : Hex("15161B");
            }
        }

        private static GameObject CreateItemSlot(
            string name,
            Transform parent,
            Sprite icon,
            Color rarity,
            bool equipped,
            string level)
        {
            var slot = PanelObject(name, parent, Hex("101115"),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(138f, 138f));
            var outline = slot.AddComponent<Outline>();
            outline.effectColor = rarity;
            outline.effectDistance = new Vector2(3f, -3f);

            var iconImage = Image("Icon", slot.transform, Color.white, icon);
            SetRect(iconImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(112f, 112f));
            iconImage.preserveAspect = true;

            var rarityMark = Image("RarityMark", slot.transform, rarity);
            SetRect(rarityMark.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10f, -10f), new Vector2(12f, 12f));

            var levelText = Text("Level", slot.transform, level, null, 19f, FontStyles.Bold,
                TextAlignmentOptions.BottomRight, TextPrimary,
                new Vector2(1f, 0f), new Vector2(-38f, 20f), new Vector2(62f, 30f));
            levelText.enableAutoSizing = true;
            levelText.fontSizeMin = 13f;
            levelText.fontSizeMax = 19f;

            var badge = Text("EquippedBadge", slot.transform, equipped ? "E" : string.Empty,
                null, 18f, FontStyles.Bold, TextAlignmentOptions.TopLeft, Hex("85D56A"),
                new Vector2(0f, 1f), new Vector2(18f, -16f), new Vector2(30f, 30f));
            badge.gameObject.SetActive(equipped);
            return slot;
        }

        private static List<Sprite> LoadItemSprites()
        {
            return Directory.GetFiles(EquipmentAssetPath, "Item_*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/')))
                .Where(sprite => sprite != null)
                .ToList();
        }

        private static Color RarityColor(int rarity)
        {
            return rarity switch
            {
                0 => Hex("A8A8A8"),
                1 => Hex("3D8FE8"),
                2 => Hex("A64FE3"),
                3 => Hex("E4A52E"),
                _ => Hex("E74B43")
            };
        }

        private static void AddGoldOutline(GameObject gameObject)
        {
            var outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.48f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static GameObject ButtonObject(
            string name,
            Transform parent,
            string label,
            TMP_FontAsset font,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            var buttonObject = PanelObject(name, parent, PanelRaised, anchor, position, size);
            buttonObject.AddComponent<Button>();
            Text("Label", buttonObject.transform, label, font, 22f, FontStyles.Bold,
                TextAlignmentOptions.Center, TextPrimary,
                new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(12f, 8f));
            return buttonObject;
        }

        private static GameObject PanelObject(
            string name,
            Transform parent,
            Color color,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            var image = Image(name, parent, color);
            SetRect(image.rectTransform, anchor, anchor, position, size);
            return image.gameObject;
        }

        private static Image Image(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var gameObject = UI(name, parent);
            var image = gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color,
            Vector2 anchor,
            Vector2 position,
            Vector2 dimensions)
        {
            var gameObject = UI(name, parent);
            SetRect(gameObject, anchor, anchor, position, dimensions);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font != null ? font : referenceFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static GameObject UI(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = 5;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(GameObject gameObject)
        {
            Stretch(gameObject.GetComponent<RectTransform>());
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void StretchHorizontal(
            RectTransform rectTransform,
            float verticalPosition,
            float height,
            bool anchoredFromTop)
        {
            var verticalAnchor = anchoredFromTop ? 1f : 0f;
            rectTransform.anchorMin = new Vector2(0f, verticalAnchor);
            rectTransform.anchorMax = new Vector2(1f, verticalAnchor);
            rectTransform.pivot = new Vector2(0.5f, verticalAnchor);
            rectTransform.anchoredPosition = new Vector2(0f, verticalPosition);
            rectTransform.sizeDelta = new Vector2(0f, height);
            rectTransform.localScale = Vector3.one;
        }

        private static void SetRect(
            GameObject gameObject,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size)
        {
            SetRect(gameObject.GetComponent<RectTransform>(), anchorMin, anchorMax, position, size);
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = anchorMin;
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            rectTransform.localScale = Vector3.one;
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString($"#{value}", out var color)
                ? color
                : Color.white;
        }
    }
}
