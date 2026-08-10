using System;
using System.Collections.Generic;
using System.Linq;
using IdleBattle.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.Editor
{
    /// <summary>
    /// 상점(Shop) 화면의 속을 채웁니다. Tools/Idle Battle/Build Shop Panel 로 실행합니다.
    ///
    /// 칸은 이미 씬에 있습니다(배너 한 장 · 무료 상품 줄 · 카드 여섯 장 · 아래 목록 네 줄).
    /// 여기서는 그 빈 칸 안에 그림 · 이름 · 설명 · 가격 버튼을 넣습니다.
    /// 모양은 <c>Assets/05_Resources/UI/BrightReferences/Shop_SevenKnights_Rebuilt.png</c>를 따릅니다.
    ///
    /// 글자는 새로 만들지 않고 상점에 이미 있는 글자(제목 · 초기화 라벨 · 타이머)를 복제해 씁니다.
    /// 그래야 글꼴 · 색 · 외곽선이 다른 화면과 저절로 같아집니다.
    ///
    /// 여러 번 돌려도 됩니다. 지난번에 넣은 것들은 <see cref="BuiltTag"/>가 붙어 있어 먼저 지우고 새로 넣습니다.
    /// </summary>
    public static class ShopPanelBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ShopPath = "Shop/BackGround/SafeArea";
        private const string SpriteFolder = "Assets/05_Resources/UI/BrightTheme/Recreated/";

        /// <summary>이 도구가 넣은 오브젝트에 붙는 꼬리표입니다. 다시 돌릴 때 이것만 지웁니다.</summary>
        private const string BuiltTag = "~";

        // 레퍼런스에서 가져온 색입니다.
        private static readonly Color Ink = new Color(.95f, .96f, .99f, 1f);
        private static readonly Color Dim = new Color(.62f, .68f, .78f, 1f);
        private static readonly Color Gold = new Color(.96f, .84f, .55f, 1f);
        private static readonly Color BadgeRed = new Color(.72f, .24f, .24f, 1f);

        private static Transform shop;
        private static TMP_Text headingStyle;
        private static TMP_Text bodyStyle;
        private static TMP_Text valueStyle;

        /// <summary>상점에 놓을 상품 한 줄입니다.</summary>
        private readonly struct Product
        {
            public readonly string Key;      // 로컬라이제이션 키의 뒷부분
            public readonly string Name;
            public readonly string Desc;
            public readonly string Sprite;   // Recreated 폴더 기준 경로
            public readonly bool Gem;        // 참이면 보석, 거짓이면 골드
            public readonly string Price;

            public Product(string key, string name, string desc, string sprite, bool gem, string price)
            {
                Key = key;
                Name = name;
                Desc = desc;
                Sprite = sprite;
                Gem = gem;
                Price = price;
            }
        }

        /// <summary>카드 여섯 장입니다. 순서가 곧 화면에 놓이는 순서입니다.</summary>
        private static readonly Product[] Cards =
        {
            new Product("equipment_chest", "장비 상자", "여러 등급의 장비를 얻습니다", "Shop/Product_EquipmentChest", false, "100,000"),
            new Product("gem_bundle", "보석 묶음", "보석을 한 번에 넉넉히 받습니다", "Shop/Product_GemBundle", true, "500"),
            new Product("growth_elixir", "성장의 비약", "경험치를 크게 올려 줍니다", "Shop/Product_GrowthElixir", true, "200"),
            new Product("skill_book", "스킬의 서", "스킬을 올릴 때 쓰는 재료입니다", "Shop/Product_SkillBook", false, "80,000"),
            new Product("dungeon_ticket", "던전 입장권", "던전에 한 번 더 들어갑니다", "Shop/Product_DungeonTicket", true, "100"),
            new Product("exp_booster", "경험치 부스터", "한동안 경험치가 더 들어옵니다", "Shop/Product_ExpBooster", true, "150"),
        };

        /// <summary>카드 아래 목록 네 줄입니다.</summary>
        private static readonly Product[] Rows =
        {
            new Product("gold_pouch", "골드 주머니", "골드를 넉넉히 채워 줍니다", "Main/Currency_Gold", true, "300"),
            new Product("summon_key", "소환의 열쇠", "소환을 열 번 돌릴 수 있습니다", "Shop/Product_DungeonTicket", true, "900"),
            new Product("upgrade_kit", "강화 재료 묶음", "장비를 올릴 때 쓰는 재료입니다", "Shop/Product_EquipmentChest", false, "50,000"),
            new Product("mana_set", "마나 물약 세트", "싸우는 동안 마나를 채워 줍니다", "Main/Potion_MP", true, "120"),
        };

        [MenuItem("Tools/Idle Battle/Build Shop Panel")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value.name == "Canvas" && value.transform.Find("Shop") != null);
            if (canvas == null) throw new InvalidOperationException("Main 씬에서 Shop을 가진 'Canvas'를 찾지 못했습니다.");

            shop = canvas.transform.Find(ShopPath);
            if (shop == null) throw new InvalidOperationException($"Canvas/{ShopPath} 를 찾지 못했습니다.");

            headingStyle = Find("DECO/Horizontal/Title")?.GetComponent<TMP_Text>();
            bodyStyle = Find("DECO/ResestCount/Title")?.GetComponent<TMP_Text>();
            valueStyle = Find("DECO/ResestCount/Timer")?.GetComponent<TMP_Text>();
            if (headingStyle == null || bodyStyle == null || valueStyle == null)
                throw new InvalidOperationException("글자 본보기(DECO의 제목 · 초기화 · 타이머)를 찾지 못했습니다.");

            ClearBuilt(shop);
            MoveTabsBelowTitle();

            var content = Find("Main/Scroll View/Viewport/Content");
            if (content == null) throw new InvalidOperationException("Scroll View의 Content를 찾지 못했습니다.");

            BuildBanner(Child(content, "Image", "Banner"));
            BuildDailyFree(Child(content, "Image (1)", "DailyFree"));

            var grid = content.Find("Grid");
            if (grid == null) throw new InvalidOperationException("Content 아래에서 Grid를 찾지 못했습니다.");

            for (var i = 0; i < Cards.Length; i++)
            {
                var slot = Child(grid, i == 0 ? "Image" : $"Image ({i})", "Card_" + Cards[i].Key);
                if (slot != null) BuildCard(slot, Cards[i]);
            }

            for (var i = 0; i < Rows.Length; i++)
            {
                var slot = Child(content, $"Image ({i + 2})", "Row_" + Rows[i].Key);
                if (slot != null) BuildRow(slot, Rows[i]);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Shop] 상점 화면을 레퍼런스대로 채웠습니다.");
        }

        // ------------------------------------------------------------------
        // 큰 덩어리
        // ------------------------------------------------------------------

        /// <summary>
        /// 탭 줄이 '상점' 제목과 초기화 타이머를 덮고 있습니다. 레퍼런스처럼
        /// 제목 → 초기화 → 탭 → 상품 차례가 되도록 탭 줄만 아래로 내립니다.
        /// </summary>
        private static void MoveTabsBelowTitle()
        {
            var top = Find("Main/TOP") as RectTransform;
            if (top == null) return;

            Undo.RecordObject(top, "상점 탭 자리 옮기기");
            top.anchoredPosition = new Vector2(top.anchoredPosition.x, -262f);
            EditorUtility.SetDirty(top);
        }

        /// <summary>맨 위 배너입니다. 왼쪽에 글, 가운데에 그림, 오른쪽에 값을 둡니다.</summary>
        private static void BuildBanner(Transform slot)
        {
            if (slot == null) return;

            var badge = Sprite(slot, "Badge", "Frames/Button_Primary", new Vector2(-402f, 100f), new Vector2(104f, 46f));
            badge.color = BadgeRed;
            Label(badge.transform, "Text", "추천", "shop.featured.badge", 24f, Ink,
                Vector2.zero, new Vector2(104f, 46f), TextAlignmentOptions.Center, headingStyle);

            Label(slot, "Title", "모험 준비 패키지", "shop.featured.title", 44f, Ink,
                new Vector2(-250f, 40f), new Vector2(460f, 56f), TextAlignmentOptions.MidlineLeft, headingStyle);
            Label(slot, "Desc", "모험에 필요한 것을 한 번에", "shop.featured.desc", 26f, Dim,
                new Vector2(-250f, -12f), new Vector2(460f, 38f), TextAlignmentOptions.MidlineLeft, bodyStyle);

            Sprite(slot, "Art", "Shop/Product_FeaturedBundle", new Vector2(120f, 0f), new Vector2(320f, 250f));

            Label(slot, "Limit", "1회 한정", "shop.featured.limit", 26f, Gold,
                new Vector2(400f, 62f), new Vector2(200f, 38f), TextAlignmentOptions.Center, bodyStyle);

            var price = Sprite(slot, "PriceBtn", "Frames/Button_Primary", new Vector2(400f, -30f), new Vector2(220f, 80f));
            Label(price.transform, "Text", "₩ 9,900", null, 34f, Ink,
                Vector2.zero, new Vector2(220f, 80f), TextAlignmentOptions.Center, valueStyle);
        }

        /// <summary>매일 한 번 받는 줄입니다. 선물 상자 · 글 · 받을 것 · 받기 버튼 차례입니다.</summary>
        private static void BuildDailyFree(Transform slot)
        {
            if (slot == null) return;

            Sprite(slot, "Icon", "Common/Icon_Gift", new Vector2(-460f, 0f), new Vector2(84f, 84f));

            Label(slot, "Title", "매일 무료 상품", "shop.daily.title", 34f, Ink,
                new Vector2(-220f, 22f), new Vector2(340f, 44f), TextAlignmentOptions.MidlineLeft, headingStyle);
            Label(slot, "Desc", "하루 한 번 무료로 받으세요", "shop.daily.desc", 24f, Dim,
                new Vector2(-200f, -24f), new Vector2(420f, 34f), TextAlignmentOptions.MidlineLeft, bodyStyle);

            var item = Sprite(slot, "Item", "Frames/Slot_Item", new Vector2(230f, 0f), new Vector2(96f, 96f));
            Sprite(item.transform, "Icon", "Main/Currency_Gem", Vector2.zero, new Vector2(58f, 58f));
            Label(item.transform, "Count", "50", null, 24f, Ink,
                new Vector2(0f, -32f), new Vector2(90f, 30f), TextAlignmentOptions.Center, valueStyle);

            var claim = Sprite(slot, "ClaimBtn", "Frames/Button_Secondary", new Vector2(400f, 0f), new Vector2(220f, 84f));
            Label(claim.transform, "Text", "무료 받기", "shop.daily.claim", 30f, Ink,
                Vector2.zero, new Vector2(220f, 84f), TextAlignmentOptions.Center, headingStyle);
            Sprite(claim.transform, "Dot", "Common/Badge_Notification", new Vector2(96f, 34f), new Vector2(26f, 26f));
        }

        /// <summary>카드 한 장입니다. 왼쪽에 그림, 오른쪽에 이름과 설명, 아래에 값입니다.</summary>
        private static void BuildCard(Transform slot, Product product)
        {
            Sprite(slot, "Icon", product.Sprite, new Vector2(-155f, 30f), new Vector2(175f, 175f));

            Label(slot, "Name", product.Name, "shop.item." + product.Key, 34f, Ink,
                new Vector2(80f, 112f), new Vector2(340f, 48f), TextAlignmentOptions.Center, headingStyle);
            // 설명은 두 줄까지 내려갈 수 있게 넉넉히 잡습니다. 좁으면 마지막 한 글자만 다음 줄로 떨어져 보기 나쁩니다.
            Label(slot, "Desc", product.Desc, "shop.item." + product.Key + ".desc", 23f, Dim,
                new Vector2(80f, 36f), new Vector2(344f, 86f), TextAlignmentOptions.Top, bodyStyle);

            BuildPrice(slot, product, new Vector2(0f, -120f), new Vector2(430f, 78f));
        }

        /// <summary>목록 한 줄입니다. 카드와 같은 내용을 옆으로 눕혀 놓은 모양입니다.</summary>
        private static void BuildRow(Transform slot, Product product)
        {
            Sprite(slot, "Icon", product.Sprite, new Vector2(-460f, 0f), new Vector2(96f, 96f));

            Label(slot, "Name", product.Name, "shop.item." + product.Key, 32f, Ink,
                new Vector2(-215f, 22f), new Vector2(380f, 44f), TextAlignmentOptions.MidlineLeft, headingStyle);
            Label(slot, "Desc", product.Desc, "shop.item." + product.Key + ".desc", 24f, Dim,
                new Vector2(-195f, -24f), new Vector2(420f, 34f), TextAlignmentOptions.MidlineLeft, bodyStyle);

            BuildPrice(slot, product, new Vector2(395f, 0f), new Vector2(230f, 80f));
        }

        /// <summary>값 버튼입니다. 재화 그림과 숫자를 한 덩어리로 가운데에 둡니다.</summary>
        private static void BuildPrice(Transform slot, Product product, Vector2 position, Vector2 size)
        {
            var button = Sprite(slot, "PriceBtn", "Frames/Button_Secondary", position, size);
            // 재화 그림과 숫자를 한 덩어리로 보이게, 버튼 폭에 비례해 자리를 잡습니다.
            // 좁은 버튼(목록 줄)에서 숫자가 그림에 달라붙지 않게 하려는 값입니다.
            Sprite(button.transform, "Icon", product.Gem ? "Main/Currency_Gem" : "Main/Currency_Gold",
                new Vector2(-size.x * .26f, 0f), new Vector2(40f, 40f));
            Label(button.transform, "Amount", product.Price, null, 32f, product.Gem ? Ink : Gold,
                new Vector2(size.x * .08f, 0f), new Vector2(size.x * .55f, 44f),
                TextAlignmentOptions.Center, valueStyle);
        }

        // ------------------------------------------------------------------
        // 조각 만들기
        // ------------------------------------------------------------------

        /// <summary>그림 한 장을 넣습니다. 자리는 부모 가운데를 기준으로 잡습니다.</summary>
        private static Image Sprite(Transform parent, string name, string spritePath, Vector2 position, Vector2 size)
        {
            var go = new GameObject(BuiltTag + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);

            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + spritePath + ".png");
            if (image.sprite == null) Debug.LogWarning($"[Shop] 그림을 찾지 못했습니다: {SpriteFolder}{spritePath}.png");
            // 9칸으로 늘어나는 그림(버튼 · 칸)은 테두리를 지키며 늘어나야 합니다.
            else if (image.sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            return image;
        }

        /// <summary>
        /// 글자 하나를 넣습니다. 본보기를 복제해 만들기 때문에 글꼴 · 외곽선 · 재질이 다른 화면과 같습니다.
        /// <paramref name="key"/>를 주면 언어를 바꿀 때 따라오도록 <see cref="LocalizedText"/>도 붙입니다.
        /// </summary>
        private static TMP_Text Label(Transform parent, string name, string text, string key, float size, Color color,
            Vector2 position, Vector2 rect, TextAlignmentOptions alignment, TMP_Text template)
        {
            var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = BuiltTag + name;
            go.SetActive(true);

            // 본보기에 딸려 온 것은 떼어 냅니다.
            // 특히 ContentSizeFitter가 따라오면 글자 길이대로 칸을 늘려서, 여기서 정한 자리와 크기가 무시됩니다.
            foreach (var localized in go.GetComponents<LocalizedText>())
                UnityEngine.Object.DestroyImmediate(localized);
            foreach (var fitter in go.GetComponents<ContentSizeFitter>())
                UnityEngine.Object.DestroyImmediate(fitter);
            foreach (var element in go.GetComponents<LayoutElement>())
                UnityEngine.Object.DestroyImmediate(element);
            for (var i = go.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);

            Place((RectTransform)go.transform, position, rect);

            var label = go.GetComponent<TMP_Text>();
            label.text = text;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            // 지금 크기를 위로 두고, 번역해서 길어지면 칸 안에서 알아서 줄어들게 합니다.
            label.enableAutoSizing = true;
            label.fontSizeMax = size;
            label.fontSizeMin = Mathf.Max(10f, size * .5f);
            label.fontSize = size;

            if (!string.IsNullOrEmpty(key))
                go.AddComponent<LocalizedText>().EditorBind(key, text);

            return label;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        // ------------------------------------------------------------------

        private static Transform Find(string path) => shop.Find(path);

        /// <summary>씬에 있던 빈 칸을 찾아 알아보기 쉬운 이름으로 바꿔 돌려줍니다.</summary>
        private static Transform Child(Transform parent, string sceneName, string newName)
        {
            var found = parent.Find(sceneName) ?? parent.Find(newName);
            if (found == null)
            {
                Debug.LogWarning($"[Shop] '{parent.name}' 아래에서 '{sceneName}'을(를) 찾지 못했습니다.");
                return null;
            }

            found.name = newName;
            return found;
        }

        /// <summary>지난번에 이 도구가 넣은 것만 지웁니다. 씬에서 손으로 만든 것은 건드리지 않습니다.</summary>
        private static void ClearBuilt(Transform root)
        {
            var doomed = new List<Transform>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && child.name.StartsWith(BuiltTag, StringComparison.Ordinal))
                    doomed.Add(child);

            foreach (var target in doomed)
                if (target != null) UnityEngine.Object.DestroyImmediate(target.gameObject);
        }
    }
}
