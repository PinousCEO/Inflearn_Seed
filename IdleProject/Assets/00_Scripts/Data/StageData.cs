using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleBattle
{
    [CreateAssetMenu(
        fileName = "StageData",
        menuName = "Idle Battle/Stage Data")]
    public sealed class StageData : ScriptableObject
    {
        [SerializeField] private List<StageRule> rules = new List<StageRule>();

        [NonSerialized] private StageRule[] sortedRules = Array.Empty<StageRule>();

        public IReadOnlyList<StageRule> Rules => sortedRules;

        public bool TryGetRule(int stage, out StageRule rule)
        {
            rule = null;
            if (stage < 1 || sortedRules.Length == 0)
                return false;

            var low = 0;
            var high = sortedRules.Length - 1;

            while (low <= high)
            {
                var middle = low + (high - low) / 2;
                var candidate = sortedRules[middle];

                if (stage < candidate.MinStage)
                {
                    high = middle - 1;
                }
                else if (candidate.Contains(stage))
                {
                    rule = candidate;
                    return true;
                }
                else
                {
                    low = middle + 1;
                }
            }

            return false;
        }

        public StageRule GetRule(int stage)
        {
            if (TryGetRule(stage, out var rule))
                return rule;

            throw new InvalidOperationException(
                $"Stage {stage}에 적용되는 구간 규칙이 없습니다: {name}");
        }

        private void OnEnable()
        {
            RebuildCache();
        }

        private void OnValidate()
        {
            rules ??= new List<StageRule>();

            foreach (var rule in rules)
                rule?.Validate();

            RebuildCache();
            ValidateRanges();
        }

        private void RebuildCache()
        {
            if (rules == null)
            {
                sortedRules = Array.Empty<StageRule>();
                return;
            }

            var validRules = rules.FindAll(rule => rule != null);
            validRules.Sort((a, b) => a.MinStage.CompareTo(b.MinStage));
            sortedRules = validRules.ToArray();
        }

        private void ValidateRanges()
        {
            if (sortedRules.Length == 0)
                return;

            if (sortedRules[0].MinStage > 1)
            {
                Debug.LogWarning(
                    $"Stage 1~{sortedRules[0].MinStage - 1} 구간이 비어 있습니다.",
                    this);
            }

            for (var i = 1; i < sortedRules.Length; i++)
            {
                var previous = sortedRules[i - 1];
                var current = sortedRules[i];

                if (!previous.HasUpperLimit)
                {
                    Debug.LogError(
                        $"무제한 구간은 마지막이어야 합니다: {previous.RuleId}",
                        this);
                    continue;
                }

                if (current.MinStage <= previous.MaxStage)
                {
                    Debug.LogError(
                        $"구간이 중복됩니다: {previous.RuleId}, {current.RuleId}",
                        this);
                }
                else if (previous.MaxStage < int.MaxValue &&
                         current.MinStage > previous.MaxStage + 1)
                {
                    Debug.LogWarning(
                        $"Stage {previous.MaxStage + 1}~{current.MinStage - 1} 구간이 비어 있습니다.",
                        this);
                }
            }
        }
    }

    [Serializable]
    public sealed class StageRule
    {
        [SerializeField] private string ruleId = "default";

        [Tooltip("이 규칙이 시작되는 Stage. 포함 값입니다.")]
        [SerializeField, Min(1)] private int minStage = 1;

        [Tooltip("이 규칙이 끝나는 Stage. 포함 값이며, 0이면 무제한입니다.")]
        [SerializeField, Min(0)] private int maxStage;

        [Tooltip("보스 전에 진행하는 일반 라운드 수입니다.")]
        [SerializeField, Min(0)] private int normalRoundCount = 3;

        [Tooltip("보스 선택 시스템에서 사용할 그룹 ID입니다.")]
        [SerializeField] private string bossPoolId = "default";

        public string RuleId => ruleId;
        public int MinStage => minStage;
        public int MaxStage => maxStage;
        public int NormalRoundCount => normalRoundCount;
        public int BossRoundNumber => normalRoundCount + 1;
        public string BossPoolId => bossPoolId;
        public bool HasUpperLimit => maxStage > 0;

        public bool Contains(int stage)
        {
            return stage >= minStage &&
                   (!HasUpperLimit || stage <= maxStage);
        }

        internal void Validate()
        {
            minStage = Mathf.Max(1, minStage);
            maxStage = Mathf.Max(0, maxStage);

            if (maxStage > 0 && maxStage < minStage)
                maxStage = minStage;

            normalRoundCount = Mathf.Max(0, normalRoundCount);
            ruleId = string.IsNullOrWhiteSpace(ruleId)
                ? $"stage-{minStage}"
                : ruleId.Trim();
            bossPoolId = string.IsNullOrWhiteSpace(bossPoolId)
                ? "default"
                : bossPoolId.Trim();
        }
    }
}
