using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>
    /// 선택 화면에 늘어서는 캐릭터 버튼 하나입니다.
    /// 씬에 실제 오브젝트로 존재하며, 에디터 메뉴로 만들어 두면 플레이 전에도 눌러 볼 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CharacterSelectButton : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] private CharacterData data;

        [Header("구성 요소")]
        [SerializeField] private Button button;
        [SerializeField] private Image border;
        [SerializeField] private Image fill;
        [SerializeField] private Image accent;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text roleLabel;
        [SerializeField] private GameObject lockedBadge;

        [Header("색상")]
        [SerializeField] private Color borderIdle = new Color(0.85f, 0.73f, 0.43f, 0.28f);
        [SerializeField] private Color borderSelected = new Color(0.94f, 0.84f, 0.63f, 1f);
        [SerializeField] private Color fillIdle = new Color(0.09f, 0.15f, 0.24f, 0.95f);
        [SerializeField] private Color fillSelected = new Color(0.22f, 0.17f, 0.09f, 0.97f);
        [SerializeField] private Color fillLocked = new Color(0.05f, 0.09f, 0.15f, 0.95f);
        [SerializeField] private Color nameIdle = new Color(0.91f, 0.86f, 0.75f, 1f);
        [SerializeField] private Color nameSelected = new Color(1f, 0.91f, 0.72f, 1f);
        [SerializeField] private Color nameLocked = new Color(0.56f, 0.63f, 0.72f, 1f);

        [Header("연출")]
        [SerializeField, Min(1f)] private float selectedScale = 1.06f;
        [SerializeField, Min(0f)] private float scaleDuration = 0.18f;

        private Coroutine scaleRoutine;
        private bool isSelected;

        public CharacterData Data => data;
        public Button Button => button;
        public RectTransform Root => (RectTransform)transform;
        public bool IsLocked => data == null || !data.IsPlayable;

        private void Reset()
        {
            CollectComponents();
        }

        private void OnValidate()
        {
            CollectComponents();
            ApplyData();
            ApplyState(isSelected);
        }

        /// <summary>이 버튼이 대변할 캐릭터를 지정합니다.</summary>
        public void Bind(CharacterData value)
        {
            data = value;
            CollectComponents();
            ApplyData();
            SetSelected(false, animate: false);
        }

        public void SetSelected(bool selected, bool animate)
        {
            isSelected = selected;
            ApplyState(selected);

            var target = Vector3.one * (selected ? selectedScale : 1f);
            // 버튼이 꺼져 있으면 코루틴을 시작할 수 없으므로 크기만 바로 맞춘다.
            if (!animate || !Application.isPlaying || !isActiveAndEnabled)
            {
                if (scaleRoutine != null) StopCoroutine(scaleRoutine);
                scaleRoutine = null;
                transform.localScale = target;
                return;
            }

            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(ScaleRoutine(target));
        }

        /// <summary>잠긴 캐릭터를 눌렀을 때의 좌우 흔들림입니다.</summary>
        public IEnumerator ShakeRoutine()
        {
            var rect = Root;
            var origin = rect.anchoredPosition;
            const float duration = 0.34f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = elapsed / duration;
                rect.anchoredPosition = origin +
                    new Vector2(Mathf.Sin(t * Mathf.PI * 6f) * 10f * (1f - t), 0f);
                yield return null;
            }

            rect.anchoredPosition = origin;
        }

        private IEnumerator ScaleRoutine(Vector3 target)
        {
            var start = transform.localScale;
            var duration = Mathf.Max(0.01f, scaleDuration);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                transform.localScale = Vector3.LerpUnclamped(start, target, eased);
                yield return null;
            }

            transform.localScale = target;
            scaleRoutine = null;
        }

        /// <summary>캐릭터 데이터의 내용을 라벨과 배지에 반영합니다.</summary>
        private void ApplyData()
        {
            if (data == null) return;

            if (nameLabel != null) nameLabel.text = data.DisplayName;
            if (roleLabel != null) roleLabel.text = data.RoleName;
            if (accent != null) accent.color = data.ThemeColor;

            if (lockedBadge != null)
            {
                lockedBadge.SetActive(IsLocked);
                var badgeLabel = lockedBadge.GetComponent<TMP_Text>();
                if (badgeLabel != null) badgeLabel.text = data.LockedLabel;
            }
        }

        private void ApplyState(bool selected)
        {
            var locked = IsLocked;

            if (border != null)
                border.color = selected ? borderSelected : borderIdle;

            if (fill != null)
                fill.color = selected ? fillSelected : locked ? fillLocked : fillIdle;

            if (nameLabel != null)
                nameLabel.color = selected ? nameSelected : locked ? nameLocked : nameIdle;

            if (roleLabel != null)
                roleLabel.alpha = selected ? 1f : locked ? 0.6f : 0.78f;

            if (accent != null)
            {
                var color = accent.color;
                color.a = selected ? 1f : locked ? 0.25f : 0.6f;
                accent.color = color;
            }
        }

        private void CollectComponents()
        {
            button ??= GetComponent<Button>();
            border ??= GetComponent<Image>();
            fill ??= FindChildImage("Fill");
            accent ??= FindChildImage("Accent");
            nameLabel ??= FindChildText("Name");
            roleLabel ??= FindChildText("Role");

            if (lockedBadge == null)
            {
                var badge = transform.Find("Locked");
                if (badge != null) lockedBadge = badge.gameObject;
            }
        }

        private Image FindChildImage(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private TMP_Text FindChildText(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        // ------------------------------------------------------------------
        // 생성
        // ------------------------------------------------------------------

        /// <summary>
        /// 버튼 오브젝트 한 벌을 만듭니다.
        /// 에디터 메뉴와 런타임 폴백이 같은 모양을 만들도록 생성 코드를 여기 한곳에 둡니다.
        /// </summary>
        public static CharacterSelectButton Create(
            RectTransform parent,
            CharacterData data,
            TMP_FontAsset font,
            Vector2 size)
        {
            var id = data != null && !string.IsNullOrWhiteSpace(data.CharacterId)
                ? data.CharacterId
                : "unknown";

            var host = new GameObject($"Character_{id}", typeof(RectTransform));
            var root = (RectTransform)host.transform;
            root.SetParent(parent, false);
            root.sizeDelta = size;

            // 바깥 Image가 금색 테두리, 안쪽 Fill이 본체가 된다.
            var border = host.AddComponent<Image>();

            var fill = CreateStretchedImage(root, "Fill", 2f);
            fill.raycastTarget = false;

            var accentHost = new GameObject("Accent", typeof(RectTransform));
            var accentRect = (RectTransform)accentHost.transform;
            accentRect.SetParent(root, false);
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(1f, 0f);
            accentRect.pivot = new Vector2(0.5f, 0f);
            accentRect.offsetMin = new Vector2(2f, 2f);
            accentRect.offsetMax = new Vector2(-2f, 8f);
            var accent = accentHost.AddComponent<Image>();
            accent.raycastTarget = false;

            var nameLabel = CreateLabel(root, "Name", font, 26f,
                new Vector2(0f, 0.50f), new Vector2(1f, 0.84f));
            var roleLabel = CreateLabel(root, "Role", font, 18f,
                new Vector2(0f, 0.28f), new Vector2(1f, 0.50f));
            roleLabel.color = new Color(0.50f, 0.58f, 0.68f, 1f);

            var lockedLabel = CreateLabel(root, "Locked", font, 17f,
                new Vector2(0f, 0.08f), new Vector2(1f, 0.28f));
            lockedLabel.color = new Color(0.72f, 0.79f, 0.88f, 1f);
            lockedLabel.characterSpacing = 6f;

            var button = host.AddComponent<Button>();
            button.targetGraphic = border;
            button.transition = Selectable.Transition.None;

            var component = host.AddComponent<CharacterSelectButton>();
            component.button = button;
            component.border = border;
            component.fill = fill;
            component.accent = accent;
            component.nameLabel = nameLabel;
            component.roleLabel = roleLabel;
            component.lockedBadge = lockedLabel.gameObject;
            component.Bind(data);
            return component;
        }

        private static Image CreateStretchedImage(RectTransform parent, string objectName, float inset)
        {
            var host = new GameObject(objectName, typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return host.AddComponent<Image>();
        }

        private static TMP_Text CreateLabel(
            RectTransform parent,
            string objectName,
            TMP_FontAsset font,
            float size,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var host = new GameObject(objectName, typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(6f, 0f);
            rect.offsetMax = new Vector2(-6f, 0f);

            var label = host.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }
    }
}
