using System;
using System.Collections.Generic;
using System.Globalization;
using Firebase.Firestore;
using UnityEngine;

namespace IdleBattle
{
    /// <summary>우편 한 통이 주는 보상의 종류입니다.</summary>
    public enum MailRewardType
    {
        None,
        Gold,
        Experience,
        Item
    }

    /// <summary>
    /// 유저 문서(USERS/{uid})의 <c>POST</c> 배열에 들어 있는 우편 한 통입니다.
    ///
    /// 운영자가 Firebase 콘솔에서 손으로 넣을 수 있도록 중첩 없이 평평한 맵으로 둡니다.
    /// <code>
    /// POST: [
    ///   {
    ///     id: "notice-001",          // 비워 두면 불러올 때 자동으로 채웁니다.
    ///     title: "운영자 우편",
    ///     body: "서버 안정화에 협조해 주셔서 감사합니다.",
    ///     rewardType: "gold",        // none | gold | exp | item
    ///     rewardItemId: "",          // rewardType이 item일 때만 씁니다.
    ///     rewardAmount: 50000,
    ///     claimed: false,
    ///     sentAt: 2026-08-09 오전 9:00 (timestamp),
    ///     expiresAt: 2026-08-16 오전 9:00 (timestamp)   // 없으면 만료되지 않습니다.
    ///   }
    /// ]
    /// </code>
    /// 시각은 timestamp가 기본이지만, 콘솔에서 숫자(epoch 밀리초)나 문자열로 넣어도 읽습니다.
    /// </summary>
    public sealed class MailData
    {
        public const string IdKey = "id";
        public const string TitleKey = "title";
        public const string BodyKey = "body";
        public const string RewardTypeKey = "rewardType";
        public const string RewardItemIdKey = "rewardItemId";
        public const string RewardAmountKey = "rewardAmount";
        public const string ClaimedKey = "claimed";
        public const string SentAtKey = "sentAt";
        public const string ExpiresAtKey = "expiresAt";

        public string Id = string.Empty;
        public string Title = string.Empty;
        public string Body = string.Empty;
        public MailRewardType RewardType = MailRewardType.None;
        public string RewardItemId = string.Empty;
        public long RewardAmount;
        public bool Claimed;
        public DateTime SentAtUtc = DateTime.UtcNow;

        /// <summary>만료 시각입니다. <see cref="DateTime.MaxValue"/>면 만료가 없다는 뜻입니다.</summary>
        public DateTime ExpiresAtUtc = DateTime.MaxValue;

        public bool HasExpiry => ExpiresAtUtc < DateTime.MaxValue;
        public bool IsExpired(DateTime utcNow) => HasExpiry && ExpiresAtUtc <= utcNow;

        /// <summary>아직 받지 않았고 기간도 남아 있으면 true입니다.</summary>
        public bool IsClaimable(DateTime utcNow) => !Claimed && !IsExpired(utcNow);

        /// <summary>우편 목록의 남은 기간 문구입니다. 한 줄로 짧게 씁니다.</summary>
        public string DescribeRemaining(DateTime utcNow)
        {
            if (!HasExpiry) return "기간 제한 없음";

            var remaining = ExpiresAtUtc - utcNow;
            if (remaining <= TimeSpan.Zero) return "기간이 지났습니다";
            if (remaining.TotalDays >= 1d) return $"{(int)remaining.TotalDays}일 {remaining.Hours}시간 남음";
            if (remaining.TotalHours >= 1d) return $"{(int)remaining.TotalHours}시간 {remaining.Minutes}분 남음";
            return $"{Math.Max(1, (int)remaining.TotalMinutes)}분 남음";
        }

        // ------------------------------------------------------------------
        // Firestore 변환
        // ------------------------------------------------------------------

        /// <summary>POST 배열의 원소 하나를 우편으로 읽습니다. 형식이 아니면 false를 돌려줍니다.</summary>
        public static bool TryParse(object raw, out MailData mail)
        {
            mail = null;
            if (!(raw is IDictionary<string, object> map)) return false;

            mail = new MailData
            {
                Id = ReadString(map, IdKey),
                Title = ReadString(map, TitleKey, "제목 없음"),
                Body = ReadString(map, BodyKey, "des", "description", "message"),
                RewardType = ParseRewardType(ReadString(map, RewardTypeKey, "type")),
                RewardItemId = ReadString(map, RewardItemIdKey, "itemId"),
                RewardAmount = ReadLong(map, RewardAmountKey, "amount"),
                Claimed = ReadBool(map, ClaimedKey),
                SentAtUtc = ReadDateTime(map, DateTime.UtcNow, SentAtKey, "createdAt"),
                ExpiresAtUtc = ReadDateTime(map, DateTime.MaxValue, ExpiresAtKey, "expireAt")
            };

            // 보상 종류를 적지 않았어도 아이템 아이디가 있으면 아이템 우편으로 봅니다.
            if (mail.RewardType == MailRewardType.None && !string.IsNullOrWhiteSpace(mail.RewardItemId))
                mail.RewardType = MailRewardType.Item;
            if (mail.RewardType != MailRewardType.None && mail.RewardAmount <= 0L)
                mail.RewardAmount = 1L;

            return true;
        }

        /// <summary>Firestore에 다시 써 넣을 맵으로 바꿉니다.</summary>
        public Dictionary<string, object> ToDictionary()
        {
            var map = new Dictionary<string, object>
            {
                [IdKey] = Id ?? string.Empty,
                [TitleKey] = Title ?? string.Empty,
                [BodyKey] = Body ?? string.Empty,
                [RewardTypeKey] = ToToken(RewardType),
                [RewardAmountKey] = RewardAmount,
                [ClaimedKey] = Claimed,
                [SentAtKey] = ToTimestamp(SentAtUtc)
            };

            if (RewardType == MailRewardType.Item)
                map[RewardItemIdKey] = RewardItemId ?? string.Empty;
            // 만료가 없는 우편은 필드 자체를 남기지 않아 콘솔에서 헷갈리지 않게 합니다.
            if (HasExpiry) map[ExpiresAtKey] = ToTimestamp(ExpiresAtUtc);

            return map;
        }

        private static Timestamp ToTimestamp(DateTime value)
            => Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));

        private static string ToToken(MailRewardType type)
        {
            return type switch
            {
                MailRewardType.Gold => "gold",
                MailRewardType.Experience => "exp",
                MailRewardType.Item => "item",
                _ => "none"
            };
        }

        private static MailRewardType ParseRewardType(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return MailRewardType.None;
            return value.Trim().ToLowerInvariant() switch
            {
                "gold" or "coin" or "coins" => MailRewardType.Gold,
                "exp" or "experience" or "xp" => MailRewardType.Experience,
                "item" or "equipment" => MailRewardType.Item,
                _ => MailRewardType.None
            };
        }

        // ------------------------------------------------------------------
        // 값 읽기 (콘솔에서 손으로 넣은 값도 최대한 받아 줍니다)
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

        private static long ReadLong(IDictionary<string, object> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!map.TryGetValue(key, out var value) || value == null) continue;
                switch (value)
                {
                    case long number: return number;
                    case int number: return number;
                    case double number: return (long)number;
                    case float number: return (long)number;
                    case string text when long.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                        return parsed;
                }
            }

            return 0L;
        }

        private static bool ReadBool(IDictionary<string, object> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!map.TryGetValue(key, out var value) || value == null) continue;
                switch (value)
                {
                    case bool flag: return flag;
                    case long number: return number != 0L;
                    case string text when bool.TryParse(text, out var parsed): return parsed;
                }
            }

            return false;
        }

        private static DateTime ReadDateTime(IDictionary<string, object> map, DateTime fallback, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!map.TryGetValue(key, out var value) || value == null) continue;
                switch (value)
                {
                    case Timestamp timestamp: return timestamp.ToDateTime();
                    case DateTime dateTime: return dateTime.ToUniversalTime();
                    case DateTimeOffset offset: return offset.UtcDateTime;
                    // 콘솔에서 숫자로 넣으면 epoch 밀리초로 봅니다.
                    case long milliseconds: return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
                    case double milliseconds: return DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds).UtcDateTime;
                    case string text when DateTime.TryParse(
                        text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out var parsed):
                        return parsed;
                }

                Debug.LogWarning($"[Mailbox] '{key}' 값을 시각으로 읽지 못했습니다: {value}");
            }

            return fallback;
        }
    }
}
