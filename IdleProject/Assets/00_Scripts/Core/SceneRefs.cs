using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleBattle
{
    /// <summary>
    /// 씬 전역 참조를 한 번만 탐색하고 캐싱합니다.
    /// 인스펙터 연결이 없는 코드가 매번 씬 전체를 훑지 않도록 탐색 비용을 씬당 1회로 고정합니다.
    /// 씬을 새로 로드하면 캐시를 비워 파괴된 참조가 남지 않게 합니다.
    /// </summary>
    public static class SceneRefs
    {
        private static readonly Dictionary<Type, Component> components = new();
        private static readonly Dictionary<string, Transform> screens = new();
        private static Canvas rootCanvas;

        /// <summary>게임 화면 루트(Main/Equipment 등)를 담고 있는 Canvas입니다. 비활성 오브젝트도 포함해 찾습니다.</summary>
        public static Canvas RootCanvas
        {
            get
            {
                if (rootCanvas != null) return rootCanvas;

                var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                // Canvas가 여러 개일 때 매번 다른 것이 잡히지 않도록,
                // 화면 루트를 실제로 가지고 있는 Canvas를 우선합니다.
                foreach (var candidate in canvases)
                {
                    if (candidate.transform.Find("Main") == null && candidate.transform.Find("Equipment") == null)
                        continue;
                    rootCanvas = candidate;
                    break;
                }

                if (rootCanvas == null && canvases.Length > 0) rootCanvas = canvases[0];
                return rootCanvas;
            }
        }

        /// <summary>RootCanvas 바로 아래의 화면 루트(Main, Equipment, Skill, Dungeon, Shop)를 캐싱해 돌려줍니다.</summary>
        public static Transform Screen(string screenName)
        {
            if (screens.TryGetValue(screenName, out var cached) && cached != null) return cached;

            var canvas = RootCanvas;
            var found = canvas != null ? canvas.transform.Find(screenName) : null;
            if (found != null) screens[screenName] = found;
            return found;
        }

        /// <summary>씬에 하나만 존재하는 컴포넌트를 캐싱해 돌려줍니다. 찾지 못하면 캐싱하지 않습니다.</summary>
        public static T Get<T>() where T : Component
        {
            if (components.TryGetValue(typeof(T), out var cached) && cached != null) return (T)cached;

            var found = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (found != null) components[typeof(T)] = found;
            return found;
        }

        /// <summary>런타임에 생성/부착한 컴포넌트를 탐색 없이 등록합니다.</summary>
        public static void Register<T>(T instance) where T : Component
        {
            if (instance != null) components[typeof(T)] = instance;
        }

        public static void Clear()
        {
            components.Clear();
            screens.Clear();
            rootCanvas = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            // Domain Reload를 끈 상태에서도 이전 플레이의 파괴된 참조가 남지 않게 합니다.
            Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Clear();
        }
    }
}
