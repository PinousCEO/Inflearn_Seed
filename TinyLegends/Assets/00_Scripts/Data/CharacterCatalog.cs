using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdleBattle
{
    /// <summary>
    /// 선택 화면에 세울 캐릭터 목록입니다. 리스트 순서가 곧 화면에 늘어서는 순서입니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterCatalog",
        menuName = "Idle Battle/Character Catalog")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        [SerializeField] private List<CharacterData> characters = new();

        private Dictionary<string, CharacterData> byId;

        public IReadOnlyList<CharacterData> Characters => characters;

        public int Count => characters?.Count ?? 0;

        public CharacterData Get(int index)
        {
            if (characters == null || index < 0 || index >= characters.Count) return null;
            return characters[index];
        }

        public bool TryGet(string characterId, out CharacterData character)
        {
            byId ??= (characters ?? new List<CharacterData>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.CharacterId))
                .GroupBy(value => value.CharacterId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            return byId.TryGetValue(characterId ?? string.Empty, out character);
        }

        /// <summary>선택 가능한 첫 캐릭터입니다. 화면 진입 시 기본 선택으로 씁니다.</summary>
        public CharacterData FirstPlayable()
        {
            if (characters == null) return null;
            foreach (var character in characters)
                if (character != null && character.IsPlayable)
                    return character;

            return characters.FirstOrDefault(value => value != null);
        }

#if UNITY_EDITOR
        public void SetCharacters(IEnumerable<CharacterData> values)
        {
            characters = values.Where(value => value != null).ToList();
            byId = null;
        }
#endif

        private void OnValidate()
        {
            characters ??= new List<CharacterData>();
            byId = null;
        }
    }
}
