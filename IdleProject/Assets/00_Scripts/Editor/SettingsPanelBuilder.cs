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
    /// 설정(Settings) 판넬을 만듭니다. Tools/Idle Battle/Build Settings Panel 로 실행합니다.
    ///
    /// 새로 그리지 않습니다. 씬에 이미 있는 것을 복제해서 씁니다.
    /// - 창 전체 : Canvas/OfflineReward (Dim · SafeArea · Window · DECO 제목 · CloseBtn · 하단 버튼)
    /// - 설정 줄 : OfflineReward의 TimeCard (Row_Stat)
    /// - 슬라이더 : Equipment 화면 방어 스탯 줄의 Slider
    /// 그래서 다른 창과 톤·여백·폰트가 저절로 같아집니다.
    ///
    /// 이름(Row_Bgm/Slider 등)은 <see cref="IdleBattle.UI.SettingsController"/>가 찾는 이름입니다.
    /// </summary>
    public static class SettingsPanelBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string TemplatePanel = "OfflineReward";
        private const string SliderPath = "Equipment/UserInfo/InfoPanel/Scroll View/Viewport/Content/Defense/Grid/Stat/Slider";

        // 꺼져 있는 화면 안에서도 찾아야 하므로 GameObject.Find 대신 Canvas에서 내려갑니다.
        private static Transform canvasRoot;

        // OfflineReward 창의 값을 그대로 따릅니다.
        private const float RowWidth = 760f;
        private const float RowHeight = 96f;
        private const float RowStep = 116f;
        private const float FirstRowTop = -300f;
        private const float SideMargin = 40f;
        // 줄 4개 + 제목 + 하단 버튼이 딱 들어가는 높이입니다.
        private const float WindowHeight = 1020f;

        // 드롭다운 목록 색 — 창(Panel_Dark)과 같은 톤.
        private static readonly Color PanelDark = new Color(.09f, .12f, .16f, 1f);
        private static readonly Color Highlight = new Color(.95f, .82f, .53f, 1f);

        private static TMP_FontAsset referenceFont;

        [MenuItem("Tools/Idle Battle/Build Settings Panel")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value.name == "Canvas");
            if (canvas == null) throw new InvalidOperationException("Main 씬에서 'Canvas'를 찾지 못했습니다.");
            canvasRoot = canvas.transform;
            // 새로 만드는 글자(드롭다운)에 씬에서 쓰는 글꼴을 그대로 물려줍니다.
            referenceFont = canvas.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Select(text => text.font)
                .FirstOrDefault(font => font != null);

            var template = FindChild(canvas.transform, TemplatePanel);
            if (template == null)
                throw new InvalidOperationException($"Canvas 아래에서 '{TemplatePanel}' 판넬을 찾지 못했습니다. 이 판넬을 틀로 씁니다.");

            var existing = FindChild(canvas.transform, "Settings");
            var siblingIndex = existing != null ? existing.GetSiblingIndex() : -1;
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var wasActive = template.gameObject.activeSelf;
            template.gameObject.SetActive(true);
            var clone = UnityEngine.Object.Instantiate(template.gameObject, template.parent);
            template.gameObject.SetActive(wasActive);

            clone.name = "Settings";
            if (siblingIndex >= 0) clone.transform.SetSiblingIndex(siblingIndex);
            if (clone.GetComponent<CanvasGroup>() == null) clone.AddComponent<CanvasGroup>();

            // 오프라인 보상 전용 컴포넌트가 딸려 왔으면 떼어 냅니다.
            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Name.Contains("OfflineReward"))
                    UnityEngine.Object.DestroyImmediate(behaviour);

            var window = FindChild(clone.transform, "Window");
            if (window == null) throw new InvalidOperationException("복제한 판넬에서 Window를 찾지 못했습니다.");

            var rowTemplate = FindChild(window, "TimeCard");
            if (rowTemplate == null) throw new InvalidOperationException("TimeCard(설정 줄의 틀)를 찾지 못했습니다.");

            SetText(FindChild(window, "DECO/Horizontal/Title"), "설정");
            SetText(FindChild(window, "Desc"), "소리와 화면 효과를 조절합니다");

            // 오프라인 보상에만 필요한 것들을 정리합니다.
            Remove(FindChild(window, "Art"));
            Remove(FindChild(window, "Rewards"));
            Remove(FindChild(window, "Cap"));

            // 켜짐/꺼짐 버튼은 보조 버튼(광고 보고 2배 받기)의 모양을 씁니다.
            var toggleTemplate = FindChild(window, "DoubleBtn");
            var row = 0;
            BuildVolumeRow(window, rowTemplate, toggleTemplate, "Row_Bgm", "배경음악", RowY(row++));
            BuildVolumeRow(window, rowTemplate, toggleTemplate, "Row_Sfx", "효과음", RowY(row++));
            BuildLanguageRow(window, rowTemplate, toggleTemplate, "Row_Language", "언어", RowY(row++));
            BuildToggleRow(window, rowTemplate, toggleTemplate, "Row_Shake", "화면 흔들림", RowY(row));

            BuildVersionLabel(window, rowTemplate);
            BuildLanguagePopup(clone.transform, window, toggleTemplate, FindChild(window, "Desc"));

            // 줄이 네 개뿐이라 오프라인 보상 창 높이 그대로면 아래가 휑합니다.
            // 위쪽(제목·줄)과 아래쪽(닫기·버전) 사이 여백만 남도록 창을 줄입니다.
            var windowRect = (RectTransform)window;
            windowRect.sizeDelta = new Vector2(windowRect.sizeDelta.x, WindowHeight);

            // 틀로 쓴 것들은 지웁니다.
            Remove(rowTemplate);
            Remove(toggleTemplate);

            // 하단 버튼은 그대로 두고 이름과 글자만 바꿉니다.
            var closeButton = FindChild(window, "ClaimBtn");
            if (closeButton != null)
            {
                closeButton.name = "CloseButton";
                SetText(FindChild(closeButton, "Text"), "닫기");
            }

            clone.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("설정(Settings) 판넬을 OfflineReward 창을 복제해 만들었습니다.");
        }

        private static float RowY(int index) => FirstRowTop - index * RowStep;

        // ------------------------------------------------------------------

        private static Transform NewRow(
            Transform window, Transform rowTemplate, string name, string label, float y, float labelWidth = 260f)
        {
            var row = UnityEngine.Object.Instantiate(rowTemplate.gameObject, window).transform;
            row.name = name;
            row.SetSiblingIndex(rowTemplate.GetSiblingIndex() + 1);

            var rect = (RectTransform)row;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(RowWidth, RowHeight);

            // TimeCard의 시계 아이콘은 설정 줄에 필요 없습니다.
            Remove(FindChild(row, "Icon"));

            // 가운데 정렬이던 Value를 왼쪽 제목으로 바꿔 씁니다.
            var value = FindChild(row, "Value");
            if (value == null) return row;
            value.name = "Label";
            var text = value.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = label;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.fontSize = 30f;
            }
            PlaceLeft((RectTransform)value, labelWidth, 50f);
            return row;
        }

        /// <summary>
        /// 소리 줄입니다. 한 줄 안에 [제목] [음소거] [슬라이더] [%]가 나란히 들어갑니다.
        /// 음소거를 따로 떼어 놓지 않고 그 소리 옆에 두어야 무엇을 끄는지 바로 보입니다.
        /// </summary>
        private static void BuildVolumeRow(
            Transform window, Transform rowTemplate, Transform toggleTemplate, string name, string label, float y)
        {
            var row = NewRow(window, rowTemplate, name, label, y, 160f);
            var labelTransform = FindChild(row, "Label");

            // 값(%)은 제목과 같은 글자를 복제해 오른쪽 끝에 둡니다.
            var value = UnityEngine.Object.Instantiate(labelTransform.gameObject, row).transform;
            value.name = "Value";
            var valueText = value.GetComponent<TMP_Text>();
            valueText.text = "100%";
            valueText.alignment = TextAlignmentOptions.MidlineRight;
            valueText.fontSize = 28f;
            PlaceRight((RectTransform)value, 90f, 48f, 0f);

            if (toggleTemplate != null)
            {
                var mute = UnityEngine.Object.Instantiate(toggleTemplate.gameObject, row).transform;
                mute.name = "Mute";
                PlaceRight((RectTransform)mute, 120f, 60f, 382f);
                var muteText = FindChild(mute, "Text");
                if (muteText != null)
                {
                    muteText.name = "Label";
                    var text = muteText.GetComponent<TMP_Text>();
                    text.text = "켜짐";
                    text.fontSize = 22f;
                    FillParent((RectTransform)muteText, 8f);
                }
            }

            var sliderSource = canvasRoot != null ? canvasRoot.Find(SliderPath) : null;
            if (sliderSource == null)
            {
                Debug.LogWarning($"[Settings] 슬라이더 틀을 찾지 못했습니다: Canvas/{SliderPath}");
                return;
            }

            var slider = UnityEngine.Object.Instantiate(sliderSource.gameObject, row).transform;
            slider.name = "Slider";
            PlaceRight((RectTransform)slider, 260f, 26f, 106f);

            var component = slider.GetComponent<Slider>();
            component.minValue = 0f;
            component.maxValue = 1f;
            component.wholeNumbers = false;
            component.value = 1f;
            component.interactable = true;
            // 손가락이 닿는 곳이 배경이므로, 배경이 입력을 받아야 끌어서 조절됩니다.
            foreach (var graphic in slider.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = true;
        }

        private static void BuildToggleRow(
            Transform window, Transform rowTemplate, Transform buttonTemplate, string name, string label, float y)
        {
            var row = NewRow(window, rowTemplate, name, label, y);
            if (buttonTemplate == null) return;

            var toggle = UnityEngine.Object.Instantiate(buttonTemplate.gameObject, row).transform;
            toggle.name = "Toggle";
            PlaceRight((RectTransform)toggle, 200f, 68f, 0f);

            var toggleText = FindChild(toggle, "Text");
            if (toggleText != null)
            {
                toggleText.name = "Label";
                var text = toggleText.GetComponent<TMP_Text>();
                text.text = "켜짐";
                text.fontSize = 28f;
                FillParent((RectTransform)toggleText, 10f);
            }
        }

        /// <summary>
        /// 언어 줄입니다. 누르면 언어를 고르는 작은 창이 뜹니다.
        /// 버튼 모양은 다른 줄의 켜짐/꺼짐 버튼과 같습니다.
        /// </summary>
        private static void BuildLanguageRow(
            Transform window, Transform rowTemplate, Transform buttonTemplate, string name, string label, float y)
        {
            var row = NewRow(window, rowTemplate, name, label, y);
            if (buttonTemplate == null) return;

            var select = UnityEngine.Object.Instantiate(buttonTemplate.gameObject, row).transform;
            select.name = "Select";
            PlaceRight((RectTransform)select, 260f, 68f, 0f);

            var selectText = FindChild(select, "Text");
            if (selectText == null) return;
            selectText.name = "Label";
            var text = selectText.GetComponent<TMP_Text>();
            text.text = GameSettings.LanguageNames[0];
            text.fontSize = 28f;
            FillParent((RectTransform)selectText, 10f);
        }

        /// <summary>
        /// 언어를 고르는 작은 창입니다. 설정창 위에 겹쳐 뜹니다.
        /// 배경·버튼 모두 설정창에서 쓰던 것을 복제해 톤을 맞춥니다.
        /// </summary>
        private static void BuildLanguagePopup(
            Transform root, Transform settingsWindow, Transform buttonTemplate, Transform titleSource)
        {
            var popup = new GameObject("LanguagePopup", typeof(RectTransform));
            popup.layer = root.gameObject.layer;
            popup.transform.SetParent(root, false);
            Stretch((RectTransform)popup.transform);

            // 뒤(설정창)를 눌러도 닫히지 않게 막아 주는 판입니다.
            var dimSource = root.Find("Dim");
            if (dimSource != null)
            {
                var dim = UnityEngine.Object.Instantiate(dimSource.gameObject, popup.transform).transform;
                dim.name = "Dim";
                Stretch((RectTransform)dim);
            }

            // 설정창을 복제해 속을 비우고 작게 줄여 씁니다.
            var window = UnityEngine.Object.Instantiate(settingsWindow.gameObject, popup.transform).transform;
            window.name = "Window";
            for (var i = window.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(window.GetChild(i).gameObject);

            var windowRect = (RectTransform)window;
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.sizeDelta = new Vector2(660f, 620f);

            if (titleSource != null)
            {
                var title = UnityEngine.Object.Instantiate(titleSource.gameObject, window).transform;
                title.name = "Title";
                var titleText = title.GetComponent<TMP_Text>();
                titleText.text = "언어 선택";
                titleText.fontSize = 36f;
                titleText.alignment = TextAlignmentOptions.Center;
                var titleRect = (RectTransform)title;
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.sizeDelta = new Vector2(520f, 60f);
                titleRect.anchoredPosition = new Vector2(0f, -70f);
            }

            if (buttonTemplate == null) return;

            for (var i = 0; i < GameSettings.LanguageCodes.Length; i++)
            {
                var option = UnityEngine.Object.Instantiate(buttonTemplate.gameObject, window).transform;
                option.name = "Option_" + GameSettings.LanguageCodes[i];
                var rect = (RectTransform)option;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(520f, 96f);
                rect.anchoredPosition = new Vector2(0f, -180f - i * 116f);

                var optionText = FindChild(option, "Text");
                if (optionText == null) continue;
                optionText.name = "Label";
                var text = optionText.GetComponent<TMP_Text>();
                text.text = GameSettings.LanguageNames[i];
                text.fontSize = 30f;
                FillParent((RectTransform)optionText, 12f);
            }

            var close = UnityEngine.Object.Instantiate(buttonTemplate.gameObject, window).transform;
            close.name = "CloseButton";
            var closeRect = (RectTransform)close;
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.sizeDelta = new Vector2(520f, 92f);
            closeRect.anchoredPosition = new Vector2(0f, 74f);

            var closeText = FindChild(close, "Text");
            if (closeText != null)
            {
                closeText.name = "Label";
                var text = closeText.GetComponent<TMP_Text>();
                text.text = "닫기";
                text.fontSize = 28f;
                FillParent((RectTransform)closeText, 12f);
            }

            popup.SetActive(false);
        }

        /// <summary>버전은 줄로 두지 않고 창 오른쪽 아래에 작게만 적습니다.</summary>
        private static void BuildVersionLabel(Transform window, Transform rowTemplate)
        {
            var source = FindChild(rowTemplate, "Value") ?? FindChild(rowTemplate, "Label");
            if (source == null) return;

            var version = UnityEngine.Object.Instantiate(source.gameObject, window).transform;
            version.name = "Version";
            var text = version.GetComponent<TMP_Text>();
            text.text = "v0.0.0";
            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.color = new Color(text.color.r, text.color.g, text.color.b, .5f);

            var rect = (RectTransform)version;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(240f, 32f);
            rect.anchoredPosition = new Vector2(-32f, 26f);
            rect.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------
        // 줄 안의 자리 잡기 — 왼쪽 끝/오른쪽 끝에서 여백만큼 띄웁니다.
        // ------------------------------------------------------------------

        /// <summary>부모를 가득 채웁니다. 판넬을 덮는 Dim 같은 것에 씁니다.</summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        /// <summary>부모 안을 여백만큼 남기고 가득 채웁니다. 버튼 글자에 씁니다.</summary>
        private static void FillParent(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            rect.localScale = Vector3.one;
        }

        private static void PlaceLeft(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(SideMargin + width * .5f, 0f);
            rect.localScale = Vector3.one;
        }

        private static void PlaceRight(RectTransform rect, float width, float height, float extraOffset)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(-(SideMargin + extraOffset + width * .5f), 0f);
            rect.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------

        private static void SetText(Transform target, string value)
        {
            if (target == null) return;
            var text = target.GetComponent<TMP_Text>();
            if (text != null) text.text = value;
        }

        private static void Remove(Transform target)
        {
            if (target != null) UnityEngine.Object.DestroyImmediate(target.gameObject);
        }

        /// <summary>이름 또는 'A/B/C' 경로로 자식을 찾습니다(비활성 포함).</summary>
        private static Transform FindChild(Transform root, string path)
        {
            var direct = root.Find(path);
            if (direct != null) return direct;
            if (path.Contains("/")) return null;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == path) return child;
                var deep = FindChild(child, path);
                if (deep != null) return deep;
            }

            return null;
        }
    }
}
