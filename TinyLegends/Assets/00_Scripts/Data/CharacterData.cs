using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace IdleBattle
{
    /// <summary>선택 화면과 전투에서 함께 사용하는 6대 능력치입니다.</summary>
    public enum CharacterStatType
    {
        Attack = 0,
        Survival = 1,
        Defense = 2,
        Mobility = 3,
        Critical = 4,
        Area = 5
    }

    [CreateAssetMenu(
        fileName = "CharacterData",
        menuName = "Idle Battle/Character Data")]
    public sealed class CharacterData : ScriptableObject
    {
        public const int StatCount = 6;
        public const int MinDifficulty = 1;
        public const int MaxDifficulty = 5;
        public const int MinStat = 0;
        public const int MaxStat = 100;
        public const int MinNameLength = 2;
        public const int MaxNameLength = 12;

        /// <summary>한글 완성형·영문·숫자만 허용합니다. 자음/모음 단독 입력과 공백은 막습니다.</summary>
        private static readonly Regex NamePattern = new Regex(
            @"^[가-힣a-zA-Z0-9]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly string[] StatLabels =
        {
            "공격력", "생존력", "방어력", "기동성", "치명타", "광역"
        };

        [Header("기본 정보")]
        [SerializeField] private string characterId;
        [SerializeField] private string displayName = "새 캐릭터";
        [Tooltip("이름표 아래에 표시할 짧은 역할 문구입니다. 예) 원거리 · 민첩")]
        [SerializeField] private string roleName = "근접 · 전투";
        [SerializeField, TextArea(3, 6)] private string description;
        [Tooltip("정보 패널의 태그 칩으로 표시됩니다.")]
        [SerializeField] private string[] tags = Array.Empty<string>();

        [Header("난이도 (1~5)")]
        [SerializeField, Range(MinDifficulty, MaxDifficulty)] private int difficulty = 3;

        [Header("능력치 (0~100)")]
        [SerializeField, Range(MinStat, MaxStat)] private int attack = 50;
        [SerializeField, Range(MinStat, MaxStat)] private int survival = 50;
        [SerializeField, Range(MinStat, MaxStat)] private int defense = 50;
        [SerializeField, Range(MinStat, MaxStat)] private int mobility = 50;
        [SerializeField, Range(MinStat, MaxStat)] private int critical = 50;
        [SerializeField, Range(MinStat, MaxStat)] private int areaPower = 50;

        [Header("연출 리소스")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite portrait;
        [SerializeField] private GameObject characterPrefab;
        [Tooltip("선택 버튼과 강조 연출에 사용할 대표 색상입니다.")]
        [SerializeField] private Color themeColor = new Color(0.85f, 0.73f, 0.43f, 1f);

        [Header("스킬")]
        [Tooltip("이 캐릭터의 5구간 × 4스킬 구성입니다. 스킬 관리 창에서 캐릭터를 고른 뒤 채웁니다.")]
        [SerializeField] private CharacterSkillSetData skillSet;

        [Header("해금")]
        [SerializeField] private bool isPlayable = true;
        [SerializeField] private string lockedLabel = "COMING SOON";
        [SerializeField] private string lockedMessage = "준비 중인 영웅입니다";

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public string RoleName => roleName;
        public string Description => description;
        public IReadOnlyList<string> Tags => tags;
        public int Difficulty => difficulty;
        public int Attack => attack;
        public int Survival => survival;
        public int Defense => defense;
        public int Mobility => mobility;
        public int Critical => critical;
        public int AreaPower => areaPower;
        public Sprite Icon => icon;
        public Sprite Portrait => portrait;
        public GameObject CharacterPrefab => characterPrefab;
        public Color ThemeColor => themeColor;
        public CharacterSkillSetData SkillSet => skillSet;
        public bool IsPlayable => isPlayable;
        public string LockedLabel => lockedLabel;
        public string LockedMessage => lockedMessage;

        /// <summary>선택 화면에서 첫 슬롯에 보여줄 시작 스킬입니다.</summary>
        public SkillData StartingSkill
        {
            get
            {
                if (skillSet == null) return null;
                var section = skillSet.GetSection(0);
                return section == null ? null : section.GetSkill(0);
            }
        }

        public static string GetStatLabel(CharacterStatType type)
        {
            var index = (int)type;
            return index >= 0 && index < StatLabels.Length ? StatLabels[index] : string.Empty;
        }

        public int GetStat(CharacterStatType type)
        {
            return type switch
            {
                CharacterStatType.Attack => attack,
                CharacterStatType.Survival => survival,
                CharacterStatType.Defense => defense,
                CharacterStatType.Mobility => mobility,
                CharacterStatType.Critical => critical,
                CharacterStatType.Area => areaPower,
                _ => 0
            };
        }

        /// <summary>0~5 순서로 능력치를 읽습니다. UI가 Status 슬롯 순서대로 순회할 때 사용합니다.</summary>
        public int GetStat(int index)
        {
            return index >= 0 && index < StatCount ? GetStat((CharacterStatType)index) : 0;
        }

        /// <summary>
        /// 캐릭터 이름 규칙을 검사합니다. 2~12자의 한글·영문·숫자만 허용합니다.
        /// UI와 저장 로직이 같은 규칙을 쓰도록 여기에 둡니다.
        /// </summary>
        public static bool IsValidPlayerName(string value, out string message)
        {
            var trimmed = value?.Trim() ?? string.Empty;

            if (trimmed.Length == 0)
            {
                message = $"{MinNameLength}~{MaxNameLength}자의 한글 · 영문 · 숫자를 사용할 수 있습니다";
                return false;
            }

            if (trimmed.Length < MinNameLength)
            {
                message = $"이름은 {MinNameLength}자 이상이어야 합니다";
                return false;
            }

            if (trimmed.Length > MaxNameLength)
            {
                message = $"이름은 {MaxNameLength}자까지 사용할 수 있습니다";
                return false;
            }

            if (!NamePattern.IsMatch(trimmed))
            {
                message = "특수문자와 공백은 사용할 수 없습니다";
                return false;
            }

            message = $"‘{trimmed}’ · 사용 가능한 이름입니다";
            return true;
        }

        public void Initialize(string id, string initialName)
        {
            characterId = id;
            displayName = initialName;
            roleName = "근접 · 전투";
            description = string.Empty;
            tags = Array.Empty<string>();
            difficulty = 3;
            attack = survival = defense = mobility = critical = areaPower = 50;
            icon = null;
            portrait = null;
            characterPrefab = null;
            skillSet = null;
            isPlayable = true;
            lockedLabel = "COMING SOON";
            lockedMessage = "준비 중인 영웅입니다";
        }

#if UNITY_EDITOR
        public void SetSkillSet(CharacterSkillSetData value)
        {
            skillSet = value;
        }
#endif

        private void OnValidate()
        {
            characterId = characterId?.Trim() ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
            roleName = roleName?.Trim() ?? string.Empty;
            difficulty = Mathf.Clamp(difficulty, MinDifficulty, MaxDifficulty);
            attack = Mathf.Clamp(attack, MinStat, MaxStat);
            survival = Mathf.Clamp(survival, MinStat, MaxStat);
            defense = Mathf.Clamp(defense, MinStat, MaxStat);
            mobility = Mathf.Clamp(mobility, MinStat, MaxStat);
            critical = Mathf.Clamp(critical, MinStat, MaxStat);
            areaPower = Mathf.Clamp(areaPower, MinStat, MaxStat);
            tags ??= Array.Empty<string>();
        }
    }
}
