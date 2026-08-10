using System.Collections.Generic;
using UnityEngine;

namespace IdleBattle
{
    [CreateAssetMenu(fileName = "CharacterStatProfile", menuName = "Idle Battle/Stats/Character Stat Profile")]
    public sealed class CharacterStatProfile : ScriptableObject
    {
        [SerializeField] private StatCatalog catalog;
        [SerializeField] private List<StatValue> baseStats = new();

        public StatCatalog Catalog => catalog;
        public IReadOnlyList<StatValue> BaseStats => baseStats;

        public StatValueCollection BuildRuntime(IEnumerable<StatModifier> modifiers = null)
        {
            var result = new StatValueCollection();
            foreach (var stat in baseStats) result.SetBase(stat.type, stat.value);
            if (modifiers != null)
                foreach (var modifier in modifiers) result.Add(modifier);
            return result;
        }
    }
}
