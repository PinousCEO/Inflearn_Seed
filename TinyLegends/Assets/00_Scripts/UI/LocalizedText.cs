using TMPro;
using UnityEngine;

namespace IdleBattle.UI
{
    /// <summary>
    /// 씬에 박아 둔 TMP 글자를 <see cref="Localization"/> 표에 묶어 줍니다.
    /// 붙여 두면 언어를 바꿀 때 이 글자도 함께 바뀝니다.
    ///
    /// 씬 전체에 한 번에 붙이려면 Tools/Idle Battle/Localization/씬 글자에 키 붙이기 를 쓰세요.
    /// 값이 계속 바뀌는 글자(체력 숫자, 남은 시간)에는 붙이지 않습니다. 매 프레임 덮어쓰기라 의미가 없습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Localization.csv의 key 열 값입니다.")]
        private string key;

        [SerializeField]
        [Tooltip("표에 키가 없을 때 보여 줄 글자입니다. 키를 붙일 때 씬에 있던 글자가 자동으로 들어갑니다.")]
        private string fallback;

        private TMP_Text label;

        /// <summary>코드에서 키를 갈아 끼울 때 씁니다. 넣는 즉시 글자도 바뀝니다.</summary>
        public string Key
        {
            get => key;
            set
            {
                key = value;
                Apply();
            }
        }

        private void Awake() => label = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            Localization.LanguageChanged += Apply;
            Apply();
        }

        private void OnDisable() => Localization.LanguageChanged -= Apply;

        /// <summary>표에서 글자를 다시 읽어 넣습니다.</summary>
        public void Apply()
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (label == null || string.IsNullOrEmpty(key)) return;

            label.text = string.IsNullOrEmpty(fallback)
                ? Localization.Get(key)
                : Localization.GetOr(key, fallback);
        }

#if UNITY_EDITOR
        /// <summary>에디터에서 키를 심을 때 씁니다. 씬에 있던 글자를 되돌릴 자리로 함께 남겨 둡니다.</summary>
        public void EditorBind(string newKey, string sceneText)
        {
            key = newKey;
            fallback = sceneText;
        }
#endif
    }
}
