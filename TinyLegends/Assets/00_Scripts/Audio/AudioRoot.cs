using UnityEngine;

namespace IdleBattle.Audio
{
    /// <summary>
    /// 사운드 관련 컴포넌트가 모두 올라앉는 오브젝트 하나를 마련합니다.
    ///
    /// 씬에 <c>SoundRoot</c>가 놓여 있으면 그것을 그대로 씁니다.
    /// 그 아래 <c>BGM_Source</c> · <c>SFX_Source</c>가 있으면 인스펙터에서 정한 음량까지 물려받으므로,
    /// 씬에서 만져 둔 설정이 코드에 덮이지 않습니다.
    ///
    /// SoundRoot는 Title 씬에만 있고 Select · Main에는 없기 때문에,
    /// 찾은 것은 씬을 넘어서도 살아남게 만들고 없는 경우에만 같은 이름으로 새로 만듭니다.
    /// </summary>
    internal static class AudioRoot
    {
        public const string RootName = "SoundRoot";
        public const string BgmSourceName = "BGM_Source";
        public const string SfxSourceName = "SFX_Source";

        private static Transform persistent;

        /// <summary>씬에 놓인 SoundRoot(없으면 새로 만든 것)를 돌려줍니다. 항상 씬을 넘어 살아남습니다.</summary>
        public static Transform Resolve()
        {
            if (persistent != null)
            {
                DiscardDuplicates();
                return persistent;
            }

            var found = FindInScene();
            if (found == null)
            {
                found = new GameObject(RootName).transform;
            }
            else if (found.parent != null)
            {
                // DontDestroyOnLoad는 최상위 오브젝트에만 걸 수 있습니다.
                found.SetParent(null, true);
            }

            persistent = found;
            Object.DontDestroyOnLoad(found.gameObject);
            return persistent;
        }

        /// <summary>
        /// 이미 살아남은 SoundRoot가 있는데 새 씬이 자기 것을 또 들고 온 경우, 새로 온 쪽을 치웁니다.
        /// 그대로 두면 쓰이지 않는 AudioSource가 씬마다 하나씩 쌓입니다.
        /// </summary>
        public static void DiscardDuplicates()
        {
            if (persistent == null) return;

            foreach (var candidate in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate == null || candidate == persistent) continue;
                if (candidate.name != RootName || candidate.parent != null) continue;
                Object.Destroy(candidate.gameObject);
            }
        }

        /// <summary>
        /// SoundRoot 아래에서 이름으로 AudioSource를 찾습니다.
        /// 씬에 없을 때만(Main부터 바로 실행하는 경우 등) 같은 이름으로 하나 만듭니다.
        /// 소스는 BGM · SFX 각각 하나뿐이며, 그 위에 더 만들지 않습니다.
        /// </summary>
        public static AudioSource GetOrCreateSource(Transform root, string sourceName)
        {
            if (root == null) return null;

            var child = root.Find(sourceName);
            if (child != null)
            {
                var existing = child.GetComponent<AudioSource>();
                if (existing != null) return existing;
                return child.gameObject.AddComponent<AudioSource>();
            }

            var host = new GameObject(sourceName);
            host.transform.SetParent(root, false);
            return host.AddComponent<AudioSource>();
        }

        private static Transform FindInScene()
        {
            foreach (var candidate in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate != null && candidate.name == RootName) return candidate;
            }
            return null;
        }
    }
}
