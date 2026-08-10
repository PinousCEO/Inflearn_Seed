using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.Editor
{
    /// <summary>
    /// 우편함(Mailbox)을 기존 Equipment/Skill 패널과 같은 톤으로, 전체 화면 + 9-slice 스프라이트로 새로 만듭니다.
    /// Tools/Idle Battle/Build Mailbox Panel 메뉴로 실행하면 Main.unity의 기존 Mailbox를 같은 자리에서 교체합니다.
    /// (파일럿: 레이아웃·스타일만 구성하며 보상 수령 로직은 넣지 않습니다.)
    /// </summary>
    public static class MailboxPanelBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string SpriteBase = "Assets/05_Resources/UI/BrightTheme/";

        // Equipment 패널과 동일한 색 팔레트.
        private static readonly Color Background = Hex("08090C");
        private static readonly Color Gold = Hex("D5A94E");
        private static readonly Color TextPrimary = Hex("F2E7CE");
        private static readonly Color TextMuted = Hex("A69A83");
        private static readonly Color RewardGreen = Hex("B7D879");

        private static TMP_FontAsset referenceFont;

        // 파일럿 확인용 더미 메일 목록.
        private static readonly Mail[] Mails =
        {
            new Mail("출석 보상", "7일 연속 접속 감사합니다", "Recreated/Main/Currency_Gold", "골드 ×5,000"),
            new Mail("우편함 리뉴얼", "9-slice 프레임이 적용됐습니다", "Recreated/Dungeon/Reward_BlueGem", "젬 ×30"),
            new Mail("주간 레이드 정산", "레이드 참여 보상이 도착했습니다", "Recreated/Dungeon/Reward_Sword", "희귀 장비 1"),
            new Mail("운영자 메시지", "즐거운 모험 되세요!", "Recreated/Common/Icon_Gift", "선물 ×1"),
            new Mail("복귀 유저 선물", "돌아오신 것을 환영합니다", "Recreated/Dungeon/Reward_RedGem", "젬 ×10"),
            new Mail("이벤트 안내", "여름 대규모 업데이트 보상", "Recreated/Main/Currency_Gem", "젬 ×50")
        };

        private readonly struct Mail
        {
            public readonly string Title;
            public readonly string Body;
            public readonly string IconPath;
            public readonly string RewardLabel;

            public Mail(string title, string body, string iconPath, string rewardLabel)
            {
                Title = title;
                Body = body;
                IconPath = iconPath;
                RewardLabel = rewardLabel;
            }
        }

        [MenuItem("Tools/Idle Battle/Build Mailbox Panel")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value.name == "Canvas");
            if (canvas == null)
                throw new InvalidOperationException("Main 씬에서 'Canvas'를 찾지 못했습니다.");

            referenceFont = canvas.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Select(text => text.font)
                .FirstOrDefault(font => font != null);

            // 기존 Mailbox를 같은 부모/형제 위치에서 교체해 열기 버튼 등 이름 참조를 최대한 유지한다.
            var existing = FindDeep(canvas.transform, "Mailbox");
            Transform parent = canvas.transform;
            var siblingIndex = -1;
            if (existing != null)
            {
                parent = existing.parent;
                siblingIndex = existing.GetSiblingIndex();
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var root = UI("Mailbox", parent);
            Stretch(root);
            if (siblingIndex >= 0) root.transform.SetSiblingIndex(siblingIndex);

            // 불투명 전체 화면 배경.
            var background = Image("Background", root.transform, Background);
            Stretch(background.rectTransform);
            background.raycastTarget = true; // 뒤 화면 클릭 차단.

            var safeArea = UI("SafeArea", root.transform);
            var safeRect = safeArea.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = new Vector2(28f, 42f);
            safeRect.offsetMax = new Vector2(-28f, -42f);

            BuildHeader(safeArea.transform);
            BuildList(safeArea.transform);
            BuildFooter(safeArea.transform);

            root.SetActive(false); // Equipment와 동일하게 기본은 꺼 둔다. 미리보기하려면 하이러키에서 켜세요.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("우편함(Mailbox) 패널을 전체 화면 + 9-slice로 새로 만들어 Main.unity에 저장했습니다. 하이러키에서 Mailbox를 켜면 미리볼 수 있습니다.");
        }

        // ------------------------------------------------------------------

        private static void BuildHeader(Transform parent)
        {
            var header = Frame("Header", parent, "Recreated/Frames/Panel_Dark", Color.white,
                new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(1010f, 168f));
            StretchHorizontal(header.rectTransform, -92f, 168f, true);

            var mailIcon = Image("Icon", header.transform, Gold, LoadSprite("Recreated/Common/Icon_Mail"));
            SetRect(mailIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(70f, 0f), new Vector2(74f, 74f));
            mailIcon.preserveAspect = true;

            Text("Title", header.transform, "우편함", null, 44f, FontStyles.Bold,
                TextAlignmentOptions.Left, TextPrimary,
                new Vector2(0f, 0.5f), new Vector2(300f, 0f), new Vector2(360f, 72f));

            Text("Count", header.transform, $"{Mails.Length}개의 새 우편", null, 24f, FontStyles.Normal,
                TextAlignmentOptions.Right, TextMuted,
                new Vector2(1f, 0.5f), new Vector2(-165f, 0f), new Vector2(320f, 46f));

            ButtonFrame("CloseButton", header.transform, "×", "Recreated/Frames/Button_Secondary",
                new Vector2(1f, 0.5f), new Vector2(-62f, 0f), new Vector2(84f, 84f), 46f, Gold);
        }

        private static void BuildList(Transform parent)
        {
            // 스크롤 영역을 은은하게 묶어 주는 배경(장식 프레임 없이, 게임의 다크 톤에 맞춘 근-검정).
            var panel = Image("MailList", parent, Hex("0A0B0D"));
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.offsetMin = new Vector2(0f, 168f);   // 푸터 위
            panelRect.offsetMax = new Vector2(0f, -278f);  // 헤더(높이 168, top -92) 아래로 완전히 내려 겹침 방지

            var scrollObject = UI("ScrollView", panel.transform);
            var scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(22f, 22f);
            scrollRect.offsetMax = new Vector2(-22f, -22f);
            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 46f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = UI("Viewport", scrollObject.transform);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = viewportRect;

            var content = UI("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            scroll.content = contentRect;

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (var i = 0; i < Mails.Length; i++)
                BuildMailRow(content.transform, Mails[i], i);
        }

        private static void BuildMailRow(Transform parent, Mail mail, int index)
        {
            var row = Frame($"Mail_{index + 1:00}", parent, "Recreated/Frames/Panel_Dark", Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 172f));
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 172f;
            element.minHeight = 172f;

            // 보상 아이콘 슬롯.
            var slot = Image("RewardSlot", row.transform, Color.white, LoadSprite("Recreated/Frames/Slot_Reward"));
            slot.type = UnityEngine.UI.Image.Type.Sliced;
            SetRect(slot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(96f, 0f), new Vector2(128f, 128f));
            var icon = Image("Icon", slot.transform, Color.white, LoadSprite(mail.IconPath));
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(92f, 92f));
            icon.preserveAspect = true;

            // 제목 · 본문 · 보상.
            Text("Title", row.transform, mail.Title, null, 28f, FontStyles.Bold,
                TextAlignmentOptions.Left, TextPrimary,
                new Vector2(0f, 1f), new Vector2(320f, -34f), new Vector2(430f, 40f));
            Text("Body", row.transform, mail.Body, null, 21f, FontStyles.Normal,
                TextAlignmentOptions.Left, TextMuted,
                new Vector2(0f, 1f), new Vector2(320f, -78f), new Vector2(460f, 36f));
            Text("Reward", row.transform, mail.RewardLabel, null, 22f, FontStyles.Bold,
                TextAlignmentOptions.Left, RewardGreen,
                new Vector2(0f, 0f), new Vector2(320f, 30f), new Vector2(440f, 36f));

            // 받기 버튼(게임의 다크+골드 톤에 맞춘 보조 스타일).
            ButtonFrame("ClaimButton", row.transform, "받기", "Recreated/Frames/Button_Secondary",
                new Vector2(1f, 0.5f), new Vector2(-118f, 0f), new Vector2(180f, 96f), 30f, Gold);
        }

        private static void BuildFooter(Transform parent)
        {
            var footer = UI("Footer", parent);
            SetRect(footer, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 82f), new Vector2(1010f, 120f));
            StretchHorizontal(footer.GetComponent<RectTransform>(), 82f, 120f, false);

            // 단 하나의 강조 CTA. 게임에 붉은 필드 버튼은 없지만 HP 오브 등에 쓰인 레드를 액센트로 재사용해
            // "모두 받기"를 가장 눈에 띄는 주 액션으로 세운다(테두리·톤은 골드 계열과 이어짐).
            ButtonFrame("ClaimAllButton", footer.transform, "모두 받기", "Recreated/Frames/Button_Primary",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 112f), 32f, TextPrimary);
        }

        // ------------------------------------------------------------------
        // 헬퍼 (EquipmentPanelBuilder와 동일한 규칙)
        // ------------------------------------------------------------------

        private static Image Frame(
            string name, Transform parent, string spritePath, Color tint,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            var image = Image(name, parent, tint, LoadSprite(spritePath));
            image.type = UnityEngine.UI.Image.Type.Sliced;
            SetRect(image.rectTransform, anchor, anchor, position, size);
            return image;
        }

        private static GameObject ButtonFrame(
            string name, Transform parent, string label, string spritePath,
            Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color labelColor)
        {
            var image = Image(name, parent, Color.white, LoadSprite(spritePath));
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.raycastTarget = true;
            SetRect(image.rectTransform, anchor, anchor, position, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text("Label", image.transform, label, null, fontSize, FontStyles.Bold,
                TextAlignmentOptions.Center, labelColor,
                new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(20f, 12f));
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
            string name, Transform parent, string value, TMP_FontAsset font, float size,
            FontStyles style, TextAlignmentOptions alignment, Color color,
            Vector2 anchor, Vector2 position, Vector2 dimensions)
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

        private static Sprite LoadSprite(string relativePath)
        {
            var path = SpriteBase + relativePath + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[Mailbox] 스프라이트를 찾지 못했습니다: {path}");
            return sprite;
        }

        private static GameObject UI(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = 5; // UI
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(GameObject gameObject) => Stretch(gameObject.GetComponent<RectTransform>());

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void StretchHorizontal(RectTransform rectTransform, float verticalPosition, float height, bool anchoredFromTop)
        {
            var verticalAnchor = anchoredFromTop ? 1f : 0f;
            rectTransform.anchorMin = new Vector2(0f, verticalAnchor);
            rectTransform.anchorMax = new Vector2(1f, verticalAnchor);
            rectTransform.pivot = new Vector2(0.5f, verticalAnchor);
            rectTransform.anchoredPosition = new Vector2(0f, verticalPosition);
            rectTransform.sizeDelta = new Vector2(0f, height);
            rectTransform.localScale = Vector3.one;
        }

        private static void SetRect(GameObject gameObject, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
            => SetRect(gameObject.GetComponent<RectTransform>(), anchorMin, anchorMax, position, size);

        private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = anchorMin;
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            rectTransform.localScale = Vector3.one;
        }

        private static Color Hex(string value)
            => ColorUtility.TryParseHtmlString($"#{value}", out var color) ? color : Color.white;

        /// <summary>이름으로 자식을 재귀 탐색합니다(비활성 포함).</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindDeep(root.GetChild(i), name);
                if (match != null) return match;
            }

            return null;
        }
    }
}
