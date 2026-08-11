using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace IdleBattle
{
    /// <summary>프로젝트의 Addressables 진입점입니다. 동일 주소는 한 번만 로드하고 플레이 세션 동안 유지합니다.</summary>
    public static class AddressableContent
    {
        public const string SkillLabel = "content.skills";
        public const string ItemLabel = "content.items";

        private static readonly Dictionary<string, AsyncOperationHandle> Handles = new();

        public static T Load<T>(string address) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            var key = typeof(T).FullName + ":" + address;
            if (Handles.TryGetValue(key, out var cached) && cached.IsValid()) return cached.Result as T;

            var handle = Addressables.LoadAssetAsync<T>(address);
            var result = handle.WaitForCompletion();
            if (handle.Status != AsyncOperationStatus.Succeeded || result == null)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                Debug.LogError($"[Addressables] '{address}' ({typeof(T).Name}) 로드에 실패했습니다.");
                return null;
            }

            Handles[key] = handle;
            return result;
        }

        public static T[] LoadAll<T>(string label) where T : UnityEngine.Object
        {
            var key = "list:" + typeof(T).FullName + ":" + label;
            if (Handles.TryGetValue(key, out var cached) && cached.IsValid())
                return ((IList<T>)cached.Result).Where(value => value != null).ToArray();

            var handle = Addressables.LoadAssetsAsync<T>(label, null);
            var result = handle.WaitForCompletion();
            if (handle.Status != AsyncOperationStatus.Succeeded || result == null)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                Debug.LogError($"[Addressables] '{label}' 라벨의 {typeof(T).Name} 목록 로드에 실패했습니다.");
                return Array.Empty<T>();
            }

            Handles[key] = handle;
            return result.Where(value => value != null).ToArray();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (var handle in Handles.Values)
                if (handle.IsValid()) Addressables.Release(handle);
            Handles.Clear();
        }
    }
}
