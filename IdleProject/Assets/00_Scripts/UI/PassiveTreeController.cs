using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle
{
    public enum PassiveEffectType
    {
        Strength,
        Vitality,
        Armor,
        CriticalChance,
        CriticalDamage,
        AttackSpeed,
        LifeSteal,
        MaxMana,
        CooldownRecovery,
        AreaDamage
    }

    [DisallowMultipleComponent]
    public sealed class PassiveTreeNodeView : MonoBehaviour
    {
        [SerializeField] private string nodeId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private PassiveEffectType effectType;
        [SerializeField] private float effectValue;
        [SerializeField, Min(1)] private int pointCost = 1;
        [SerializeField] private bool majorNode;
        [SerializeField] private string[] prerequisiteIds = Array.Empty<string>();
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text rankText;

        public string NodeId => nodeId;
        public string DisplayName => displayName;
        public string Description => description;
        public PassiveEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public int PointCost => pointCost;
        public bool MajorNode => majorNode;
        public IReadOnlyList<string> PrerequisiteIds => prerequisiteIds;
        public Button Button => button;

        public void Refresh(bool invested, bool available)
        {
            if (frame != null)
                frame.color = invested
                    ? new Color(1f, .66f, .16f, 1f)
                    : available
                        ? new Color(.48f, .82f, .3f, 1f)
                        : new Color(.28f, .28f, .3f, .78f);
            if (rankText != null) rankText.text = invested ? "1/1" : "0/1";
        }
    }

    [DisallowMultipleComponent]
    public sealed class PassiveTreeConnectionView : MonoBehaviour
    {
        [SerializeField] private string sourceId;
        [SerializeField] private string targetId;
        [SerializeField] private Image line;

        public string SourceId => sourceId;
        public string TargetId => targetId;

        public void Refresh(bool active, bool available)
        {
            if (line != null)
                line.color = active
                    ? new Color(1f, .58f, .12f, 1f)
                    : available
                        ? new Color(.48f, .7f, .24f, .8f)
                        : new Color(.2f, .2f, .22f, .72f);
        }
    }

    [DisallowMultipleComponent]
    public sealed class PassiveTreeController : MonoBehaviour
    {
        [Header("Inspector References")]
        [SerializeField] private RectTransform nodeContent;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text pointText;
        [SerializeField] private TMP_Text selectedNameText;
        [SerializeField] private TMP_Text selectedDescriptionText;
        [SerializeField] private TMP_Text selectedEffectText;
        [SerializeField] private Button investButton;
        [SerializeField] private Button refundButton;

        [Header("Progress")]
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField, Min(0)] private int availablePoints;
        [SerializeField] private List<string> investedNodeIds = new List<string>();

        private readonly Dictionary<string, PassiveTreeNodeView> nodes = new();
        private PassiveTreeConnectionView[] connections = Array.Empty<PassiveTreeConnectionView>();
        private PassiveTreeNodeView selected;

        public event Action<int> LevelChanged;
        public event Action<int> PointsChanged;
        public event Action<PassiveEffectType, float> EffectChanged;

        private void Awake()
        {
            nodes.Clear();
            foreach (var node in GetComponentsInChildren<PassiveTreeNodeView>(true))
                if (!string.IsNullOrWhiteSpace(node.NodeId))
                    nodes[node.NodeId] = node;
            connections = GetComponentsInChildren<PassiveTreeConnectionView>(true);
            foreach (var node in nodes.Values)
            {
                var captured = node;
                if (node.Button != null)
                    node.Button.onClick.AddListener(() => Select(captured));
            }
            if (investButton != null) investButton.onClick.AddListener(InvestSelected);
            if (refundButton != null) refundButton.onClick.AddListener(RefundSelected);
            selected ??= nodes.Values.FirstOrDefault(value => value.NodeId == "origin");
            RefreshAll();
        }

        public void GainLevel()
        {
            level++;
            availablePoints++;
            LevelChanged?.Invoke(level);
            PointsChanged?.Invoke(availablePoints);
            RefreshAll();
        }

        public bool IsInvested(string id) => investedNodeIds.Contains(id);

        public bool CanInvest(PassiveTreeNodeView node)
        {
            if (node == null || IsInvested(node.NodeId) || availablePoints < node.PointCost) return false;
            return node.PrerequisiteIds.Count == 0 ||
                   node.PrerequisiteIds.Any(IsInvested);
        }

        private void Select(PassiveTreeNodeView node)
        {
            selected = node;
            RefreshAll();
        }

        private void InvestSelected()
        {
            if (!CanInvest(selected)) return;
            availablePoints -= selected.PointCost;
            investedNodeIds.Add(selected.NodeId);
            EffectChanged?.Invoke(selected.EffectType, selected.EffectValue);
            PointsChanged?.Invoke(availablePoints);
            RefreshAll();
        }

        private void RefundSelected()
        {
            if (selected == null || !IsInvested(selected.NodeId) || selected.NodeId == "origin") return;
            var hasInvestedDependent = nodes.Values.Any(node =>
                IsInvested(node.NodeId) && node.PrerequisiteIds.Contains(selected.NodeId));
            if (hasInvestedDependent) return;
            investedNodeIds.Remove(selected.NodeId);
            availablePoints += selected.PointCost;
            EffectChanged?.Invoke(selected.EffectType, -selected.EffectValue);
            PointsChanged?.Invoke(availablePoints);
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (levelText != null) levelText.SetText("LEVEL {0}", level);
            if (pointText != null) pointText.SetText("PASSIVE POINTS  {0}", availablePoints);
            foreach (var node in nodes.Values)
                node.Refresh(IsInvested(node.NodeId), CanInvest(node));
            foreach (var connection in connections)
            {
                var active = IsInvested(connection.SourceId) && IsInvested(connection.TargetId);
                var available = IsInvested(connection.SourceId) || IsInvested(connection.TargetId);
                connection.Refresh(active, available);
            }
            if (selectedNameText != null) selectedNameText.text = selected?.DisplayName ?? "Select a node";
            if (selectedDescriptionText != null) selectedDescriptionText.text = selected?.Description ?? string.Empty;
            if (selectedEffectText != null)
                selectedEffectText.text = selected != null
                    ? $"+{selected.EffectValue:0.#} {selected.EffectType}"
                    : string.Empty;
            if (investButton != null) investButton.interactable = CanInvest(selected);
            if (refundButton != null)
                refundButton.interactable = selected != null && IsInvested(selected.NodeId) && selected.NodeId != "origin";
        }
    }
}
