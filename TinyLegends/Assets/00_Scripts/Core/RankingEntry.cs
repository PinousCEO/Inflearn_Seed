using System;
using System.Collections.Generic;
using System.Globalization;
using Firebase.Firestore;

namespace IdleBattle
{
    /// <summary>
    /// 랭킹 컬렉션(<c>RANKING/{uid}</c>) 문서 하나입니다. 랭킹 판넬의 한 줄이 그대로 이 값입니다.
    ///
    /// <code>
    /// RANKING/{uid} {
    ///   name: "플레이어",             // 표시 이름
    ///   characterId: "character-001", // 초상화·직업 표시용
    ///   level: 12,
    ///   power: 48210,                 // 정렬 기준. CombatPower가 계산합니다.
    ///   formulaVersion: 1,            // 어떤 산정 규칙으로 찍힌 점수인지
    ///   updatedAt: &lt;timestamp&gt;       // 서버 시각
    /// }
    /// </code>
    ///
    /// 저장 문서(<c>USERS/{uid}</c>)와 따로 두는 이유는 두 가지입니다.
    /// - 랭킹은 남의 것도 읽어야 하는데, 세이브 전체를 공개할 수는 없습니다.
    /// - <c>power</c> 한 필드로만 정렬해야 상위 100명을 싸게 읽습니다.
    /// </summary>
    public sealed class RankingEntry
    {
        public const string NameKey = "name";
        public const string CharacterIdKey = "characterId";
        public const string LevelKey = "level";
        public const string PowerKey = "power";
        public const string FormulaVersionKey = "formulaVersion";
        public const string UpdatedAtKey = "updatedAt";

        /// <summary>문서 ID입니다. 내 줄을 강조 표시할 때 씁니다.</summary>
        public string UserId = string.Empty;

        public string Name = string.Empty;
        public string CharacterId = string.Empty;
        public int Level = 1;
        public long Power;
        public int FormulaVersion = CombatPower.Version;
        public DateTime UpdatedAtUtc = DateTime.MinValue;

        /// <summary>1부터 시작하는 순위입니다. 서버 문서에는 없고 읽어 온 순서로 매깁니다.</summary>
        public int Rank;

        /// <summary>이름이 비어 있으면 아직 캐릭터를 만들지 않은 계정입니다. 목록에서 뺍니다.</summary>
        public bool IsListable => !string.IsNullOrWhiteSpace(Name) && Power > 0L;

        /// <summary>랭킹 판넬의 보조 문구입니다. 한 줄로 짧게 씁니다.</summary>
        public string DescribeLevel() => $"Lv.{Level}";

        // ------------------------------------------------------------------
        // Firestore 변환
        // ------------------------------------------------------------------

        /// <summary>랭킹 문서 하나를 읽습니다. 형식이 아니면 false를 돌려줍니다.</summary>
        public static bool TryParse(IDictionary<string, object> map, string userId, out RankingEntry entry)
        {
            entry = null;
            if (map == null) return false;

            entry = new RankingEntry
            {
                UserId = userId ?? ReadString(map, "uid"),
                Name = ReadString(map, NameKey, "playerName"),
                CharacterId = ReadString(map, CharacterIdKey),
                Level = (int)Math.Max(1L, ReadLong(map, LevelKey)),
                Power = Math.Max(0L, ReadLong(map, PowerKey)),
                FormulaVersion = (int)ReadLong(map, FormulaVersionKey),
                UpdatedAtUtc = ReadDateTime(map, UpdatedAtKey)
            };

            return true;
        }

        /// <summary>Firestore에 써 넣을 맵입니다. <c>updatedAt</c>은 서버가 찍으므로 넣지 않습니다.</summary>
        public Dictionary<string, object> ToDictionary() => new()
        {
            [NameKey] = Name ?? string.Empty,
            [CharacterIdKey] = CharacterId ?? string.Empty,
            [LevelKey] = (long)Level,
            [PowerKey] = Power,
            [FormulaVersionKey] = (long)FormulaVersion
        };

        // ------------------------------------------------------------------
        // 값 읽기
        // ------------------------------------------------------------------

        private static string ReadString(IDictionary<string, object> map, params string[] keys)
        {
            foreach (var key in keys)
                if (map.TryGetValue(key, out var value) && value != null)
                {
                    var text = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
                }

            return string.Empty;
        }

        private static long ReadLong(IDictionary<string, object> map, string key)
        {
            if (!map.TryGetValue(key, out var value) || value == null) return 0L;
            return value switch
            {
                long number => number,
                int number => number,
                double number => (long)number,
                float number => (long)number,
                string text when long.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0L
            };
        }

        private static DateTime ReadDateTime(IDictionary<string, object> map, string key)
        {
            if (!map.TryGetValue(key, out var value) || value == null) return DateTime.MinValue;
            return value switch
            {
                Timestamp timestamp => timestamp.ToDateTime(),
                DateTime dateTime => dateTime.ToUniversalTime(),
                DateTimeOffset offset => offset.UtcDateTime,
                long milliseconds => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime,
                _ => DateTime.MinValue
            };
        }
    }
}
