using UnityEngine;

namespace IdleBattle
{
    /// <summary>
    /// 설정창에서 켜고 끄는 값들입니다.
    ///
    /// 음량은 <see cref="Audio.AudioManager"/>·<see cref="Audio.BgmManager"/>가 이미 PlayerPrefs에
    /// 저장하고 있으므로, 여기에는 그쪽에 없는 것만 둡니다.
    /// </summary>
    public static class GameSettings
    {
        private const string ScreenShakeKey = "Settings.ScreenShake";
        private const string LanguageKey = "Settings.Language";

        private static bool? screenShake;
        private static string language;

        /// <summary>고를 수 있는 언어입니다. 설정창 드롭다운의 순서가 이 순서입니다.</summary>
        public static readonly string[] LanguageCodes = { "ko", "ja", "en" };
        public static readonly string[] LanguageNames = { "한국어", "日本語", "English" };

        /// <summary>고른 언어입니다. "ko" · "ja" · "en".</summary>
        public static string Language
        {
            get
            {
                language ??= PlayerPrefs.GetString(LanguageKey, "ko");
                return language;
            }
            set
            {
                language = System.Array.IndexOf(LanguageCodes, value) >= 0 ? value : "ko";
                PlayerPrefs.SetString(LanguageKey, language);
            }
        }

        /// <summary>드롭다운에서 몇 번째인지입니다.</summary>
        public static int LanguageIndex
        {
            get
            {
                var index = System.Array.IndexOf(LanguageCodes, Language);
                return index < 0 ? 0 : index;
            }
            set => Language = LanguageCodes[Mathf.Clamp(value, 0, LanguageCodes.Length - 1)];
        }

        /// <summary>화면 흔들림입니다. 끄면 스킬·치명타에서 카메라가 흔들리지 않습니다.</summary>
        public static bool ScreenShakeEnabled
        {
            get
            {
                screenShake ??= PlayerPrefs.GetInt(ScreenShakeKey, 1) == 1;
                return screenShake.Value;
            }
            set
            {
                screenShake = value;
                PlayerPrefs.SetInt(ScreenShakeKey, value ? 1 : 0);
            }
        }
    }
}
