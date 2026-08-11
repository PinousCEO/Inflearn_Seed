using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IdleBattle
{
    /// <summary>
    /// 글자를 언어별로 갈아 끼우는 곳입니다.
    ///
    /// 표는 <c>Assets/Resources/Localization/Localization.csv</c> 한 장이고, 열은
    /// <c>key,ko,ja,en,note</c> 입니다. note 열은 번역할 때 어디에 쓰이는 글자인지
    /// 알아보기 위한 메모라서 게임에서는 읽지 않습니다.
    ///
    /// 쓰는 쪽:
    /// <code>
    /// label.text = Localization.Get("settings.title");
    /// label.text = Localization.Format("dungeon.floor", floor);
    /// </code>
    ///
    /// 씬에 박아 둔 글자는 <see cref="UI.LocalizedText"/>를 붙여 두면 언어를 바꿀 때
    /// 알아서 따라옵니다. 코드로 매번 넣어 주는 글자는 <see cref="LanguageChanged"/>에
    /// 붙어서 다시 그려 주세요.
    /// </summary>
    public static class Localization
    {
        public const string ResourcePath = "Localization/Localization";

        /// <summary>표에 키가 없을 때 이 문자열 앞에 키를 붙여 돌려줍니다. 눈에 띄어야 빠뜨린 걸 찾습니다.</summary>
        private const string MissingPrefix = "#";

        // 언어 코드 -> (키 -> 글자)
        private static readonly Dictionary<string, Dictionary<string, string>> tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> current;
        private static string currentLanguage;

        /// <summary>언어가 바뀐 뒤에 울립니다. 이미 그려 둔 글자를 다시 넣어 주세요.</summary>
        public static event Action LanguageChanged;

        /// <summary>지금 쓰는 언어 코드입니다. <see cref="GameSettings.Language"/>를 따라갑니다.</summary>
        public static string Language
        {
            get
            {
                EnsureLoaded();
                return currentLanguage;
            }
            set
            {
                if (Array.IndexOf(GameSettings.LanguageCodes, value) < 0) value = GameSettings.LanguageCodes[0];

                EnsureLoaded();
                if (string.Equals(currentLanguage, value, StringComparison.OrdinalIgnoreCase)) return;

                GameSettings.Language = value;
                Select(value);
                Notify();
            }
        }

        /// <summary>표에 이 키가 있는지입니다. 없는 키를 조용히 넘기고 싶을 때 씁니다.</summary>
        public static bool Has(string key)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(key) && current.ContainsKey(key);
        }

        /// <summary>키에 해당하는 글자입니다. 없으면 <c>#키</c>를 돌려줍니다.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            EnsureLoaded();
            return current.TryGetValue(key, out var value) ? value : MissingPrefix + key;
        }

        /// <summary>키가 없을 때 대신 쓸 글자를 직접 정합니다. 씬에 있던 원래 글자를 남기고 싶을 때 씁니다.</summary>
        public static string GetOr(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;

            EnsureLoaded();
            return current.TryGetValue(key, out var value) ? value : fallback;
        }

        /// <summary><c>{0}</c> 자리를 채워 돌려줍니다. 자리 개수가 어긋나면 채우지 않고 원문을 그대로 줍니다.</summary>
        public static string Format(string key, params object[] args)
        {
            var pattern = Get(key);
            if (args == null || args.Length == 0) return pattern;

            try
            {
                return string.Format(pattern, args);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[Localization] '{key}'의 {{0}} 자리와 넘긴 값 {args.Length}개가 맞지 않습니다.");
                return pattern;
            }
        }

        /// <summary>표를 다시 읽습니다. CSV를 고친 뒤 게임을 다시 켜지 않고 확인할 때 씁니다.</summary>
        public static void Reload()
        {
            tables.Clear();
            current = null;
            currentLanguage = null;
            EnsureLoaded();
            Notify();
        }

        /// <summary>
        /// 언어가 바뀌었다고 알립니다.
        ///
        /// 붙어 있는 곳을 하나씩 따로 부릅니다. 한 번에 부르면 그중 하나가 터졌을 때 그 뒤에 붙은 곳들이
        /// 통째로 건너뛰어져서, 화면 절반만 언어가 바뀌는 일이 생깁니다. 터진 곳은 로그로 남기고 나머지는 계속 돕니다.
        /// </summary>
        private static void Notify()
        {
            var handler = LanguageChanged;
            if (handler == null) return;

            foreach (var listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action)listener).Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Localization] 언어를 바꾼 뒤 '{listener.Target}'을(를) 다시 그리다 막혔습니다.\n{exception}");
                }
            }
        }

        // ------------------------------------------------------------------
        // 읽기
        // ------------------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (current != null) return;

            Load();
            Select(GameSettings.Language);
        }

        private static void Load()
        {
            var asset = AddressableContent.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[Localization] Assets/Resources/{ResourcePath}.csv 를 찾지 못했습니다. " +
                                 "글자는 키 그대로 표시됩니다.");
                foreach (var code in GameSettings.LanguageCodes)
                    tables[code] = new Dictionary<string, string>(StringComparer.Ordinal);
                return;
            }

            var rows = ParseCsv(asset.text);
            if (rows.Count == 0) return;

            // 머리글에서 언어 열의 자리를 찾습니다. 열 순서를 바꿔도 따라가게 하기 위해서입니다.
            var header = rows[0];
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Count; i++)
            {
                var name = header[i].Trim();
                if (name.Length > 0 && !columns.ContainsKey(name)) columns[name] = i;
            }

            var order = new List<KeyValuePair<string, int>>();
            foreach (var code in GameSettings.LanguageCodes)
            {
                tables[code] = new Dictionary<string, string>(StringComparer.Ordinal);
                if (columns.TryGetValue(code, out var index)) order.Add(new KeyValuePair<string, int>(code, index));
                else Debug.LogWarning($"[Localization] CSV에 '{code}' 열이 없습니다.");
            }

            var keyColumn = columns.TryGetValue("key", out var k) ? k : 0;

            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count <= keyColumn) continue;

                var key = row[keyColumn].Trim();
                if (key.Length == 0 || key.StartsWith("#", StringComparison.Ordinal)) continue;

                foreach (var pair in order)
                {
                    if (row.Count <= pair.Value) continue;

                    var text = row[pair.Value];
                    if (text.Length == 0) continue;

                    // CSV 안에서는 줄바꿈을 \n 두 글자로 적어 둡니다. 한 줄이 한 항목이라 표에서 고치기 쉽습니다.
                    tables[pair.Key][key] = text.Replace("\\n", "\n");
                }
            }
        }

        private static void Select(string code)
        {
            if (!tables.TryGetValue(code, out var table))
            {
                // ko 표는 항상 있다고 봅니다. 없으면 빈 표로 두어 키가 그대로 보이게 합니다.
                if (!tables.TryGetValue(GameSettings.LanguageCodes[0], out table))
                    table = new Dictionary<string, string>(StringComparer.Ordinal);
                code = GameSettings.LanguageCodes[0];
            }

            current = table;
            currentLanguage = code;
        }

        /// <summary>
        /// 따옴표로 감싼 칸과 그 안의 쉼표·줄바꿈까지 처리하는 CSV 읽기입니다(RFC 4180).
        /// 칸 안에 따옴표를 넣으려면 <c>""</c> 처럼 두 번 적습니다.
        /// </summary>
        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return rows;

            var row = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            var i = 0;

            // BOM은 첫 칸 이름에 붙어 'key'를 못 찾게 만들므로 떼어 냅니다.
            if (text.Length > 0 && text[0] == '﻿') i = 1;

            for (; i < text.Length; i++)
            {
                var c = text[i];

                if (quoted)
                {
                    if (c != '"')
                    {
                        field.Append(c);
                        continue;
                    }

                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                        continue;
                    }

                    quoted = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        field.Clear();
                        if (!IsEmptyRow(row)) rows.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                if (!IsEmptyRow(row)) rows.Add(row);
            }

            return rows;
        }

        private static bool IsEmptyRow(List<string> row)
        {
            foreach (var cell in row)
                if (cell.Trim().Length > 0) return false;

            return true;
        }
    }
}
