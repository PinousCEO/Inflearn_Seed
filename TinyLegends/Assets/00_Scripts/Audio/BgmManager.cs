using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleBattle.Audio
{
    /// <summary>
    /// 배경음악을 씬에 맞춰 자동으로 바꿔 주는 재생기입니다.
    ///
    /// - Title · Select : <c>Resources/Sounds/Title_BGM</c>
    /// - Main           : <c>Resources/Sounds/Main_BGM</c>
    ///
    /// 같은 곡이 이어지는 구간(Title → Select)에서는 다시 틀지 않고 그대로 흘려보내고,
    /// 곡이 바뀔 때만 소리를 줄였다가 곡을 바꿔 다시 올립니다.
    ///
    /// 재생은 씬의 <c>SoundRoot/BGM_Source</c> **하나만** 씁니다.
    /// 인스펙터에서 정해 둔 음량은 기본값으로 물려받습니다(<see cref="AudioRoot"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1500)]
    public sealed class BgmManager : MonoBehaviour
    {
        private const string ResourceRoot = "Sounds/";
        private const string TitleTrack = "Title_BGM";
        private const string MainTrack = "Main_BGM";
        private const string MainSceneName = "Main";

        private const string VolumeKey = "audio_bgm_volume";
        private const string MuteKey = "audio_bgm_muted";

        /// <summary>곡을 바꿀 때 줄였다가 다시 올리는 데 걸리는 전체 시간입니다.</summary>
        private const float SwapDuration = .8f;

        [Header("설정")]
        [Tooltip("처음 켰을 때의 배경음악 음량입니다.\n게임 안에서 음량을 바꾸면 그 값이 저장되어 다음부터는 그쪽이 쓰입니다.")]
        [SerializeField, Range(0f, 1f)] private float defaultVolume = .5f;

        private static BgmManager instance;
        private static bool quitting;

        private AudioSource source;
        private string currentTrack = string.Empty;
        private float volume = .5f;
        private bool muted;
        private Coroutine fadeRoutine;

        public static BgmManager Instance
        {
            get
            {
                if (instance != null) return instance;
                if (quitting) return null;

                instance = FindFirstObjectByType<BgmManager>();
                if (instance == null)
                {
                    // 씬에 놓인 SoundRoot가 있으면 거기에 올라탑니다. 없을 때만 새로 만들어집니다.
                    var root = AudioRoot.Resolve();
                    instance = root.GetComponent<BgmManager>() ?? root.gameObject.AddComponent<BgmManager>();
                }
                return instance;
            }
        }

        // 씬의 SoundRoot를 찾아 쓰려면 첫 씬이 로드된 뒤여야 합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            quitting = false;
            _ = Instance;
        }

        // ------------------------------------------------------------------
        // 공개 API
        // ------------------------------------------------------------------

        /// <summary>0~1. 배경음악 음량입니다. PlayerPrefs에 저장되어 다음 실행에서도 유지됩니다.</summary>
        public static float Volume
        {
            get => Instance != null ? Instance.volume : 0f;
            set
            {
                var manager = Instance;
                if (manager == null) return;
                manager.volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, manager.volume);
                manager.ApplyVolume();
            }
        }

        public static bool Muted
        {
            get => Instance != null && Instance.muted;
            set
            {
                var manager = Instance;
                if (manager == null) return;
                manager.muted = value;
                PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
                manager.ApplyVolume();
            }
        }

        /// <summary>지금 흐르고 있는 곡 이름입니다. 아무것도 틀고 있지 않으면 빈 문자열입니다.</summary>
        public static string CurrentTrack => Instance != null ? Instance.currentTrack : string.Empty;

        /// <summary>
        /// <c>Resources/Sounds</c> 아래의 곡을 틉니다.
        /// 이미 같은 곡이 흐르고 있으면 아무 일도 하지 않아, 씬을 옮겨도 음악이 끊기지 않습니다.
        /// </summary>
        public static void Play(string trackName, bool restartIfSame = false)
        {
            var manager = Instance;
            if (manager != null) manager.PlayInternal(trackName, restartIfSame);
        }

        public static void Stop(float fadeDuration = SwapDuration)
        {
            var manager = Instance;
            if (manager != null) manager.PlayInternal(null, false, fadeDuration);
        }

        // ------------------------------------------------------------------
        // 내부 구현
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                // SoundRoot는 여러 컴포넌트가 함께 쓰는 오브젝트이므로 이 컴포넌트만 걷어 냅니다.
                Destroy(this);
                return;
            }

            instance = this;
            AudioRoot.Resolve();

            BindSource();
            // 게임 안에서 조절해 저장해 둔 값이 있으면 그쪽이 우선입니다.
            volume = PlayerPrefs.GetFloat(VolumeKey, defaultVolume);
            muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            // 첫 씬에서는 sceneLoaded를 놓칠 수 있어, 씬이 다 뜬 뒤 한 번 더 맞춥니다.
            ApplySceneTrack(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        private void OnApplicationQuit() => quitting = true;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            // 새 씬이 자기 SoundRoot를 또 들고 왔다면 쓰이지 않으므로 치웁니다.
            AudioRoot.DiscardDuplicates();
            ApplySceneTrack(scene.name);
        }

        /// <summary>씬 이름에 맞는 곡으로 맞춥니다. Main만 다른 곡이고, 나머지는 타이틀 곡을 이어 씁니다.</summary>
        private void ApplySceneTrack(string sceneName)
        {
            PlayInternal(sceneName == MainSceneName ? MainTrack : TitleTrack, false);
        }

        /// <summary>씬의 BGM_Source를 붙잡습니다. 배경음악은 이 소스 하나로만 재생합니다.</summary>
        private void BindSource()
        {
            source = AudioRoot.GetOrCreateSource(transform, AudioRoot.BgmSourceName);
            if (source == null) return;

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            // 팝업 때문에 음악이 끊기면 어색하므로 계속 흐르게 둡니다.
            source.ignoreListenerPause = true;
            source.Stop();
        }

        private void PlayInternal(string trackName, bool restartIfSame, float fadeDuration = SwapDuration)
        {
            if (source == null) return;

            var target = string.IsNullOrWhiteSpace(trackName) ? string.Empty : trackName;
            if (!restartIfSame && target == currentTrack) return;

            AudioClip clip = null;
            if (target.Length > 0)
            {
                clip = Resources.Load<AudioClip>(ResourceRoot + target);
                if (clip == null)
                {
                    Debug.LogWarning($"[BGM] Resources/{ResourceRoot}{target} 을 찾지 못했습니다.", this);
                    return;
                }
            }

            currentTrack = target;

            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(SwapRoutine(clip, fadeDuration));
        }

        /// <summary>
        /// 소스가 하나뿐이라 두 곡을 겹칠 수 없습니다.
        /// 지금 곡을 줄여서 끈 뒤 새 곡으로 갈아 끼우고 다시 올립니다.
        /// </summary>
        private IEnumerator SwapRoutine(AudioClip clip, float duration)
        {
            var half = Mathf.Max(.01f, duration) * .5f;

            // 흐르던 곡이 있으면 먼저 줄입니다.
            if (source.isPlaying)
            {
                var startVolume = source.volume;
                for (var elapsed = 0f; elapsed < half; elapsed += Time.unscaledDeltaTime)
                {
                    source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / half));
                    yield return null;
                }
            }

            source.Stop();
            source.volume = 0f;
            source.clip = clip;

            if (clip == null)
            {
                fadeRoutine = null;
                yield break;
            }

            source.Play();

            var targetVolume = TargetVolume();
            for (var elapsed = 0f; elapsed < half; elapsed += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(0f, targetVolume, Mathf.Clamp01(elapsed / half));
                yield return null;
            }

            source.volume = targetVolume;
            fadeRoutine = null;
        }

        private void ApplyVolume()
        {
            if (source == null) return;
            // 곡을 바꾸는 중이라면 그쪽이 알아서 목표 음량으로 맞춰 줍니다.
            if (fadeRoutine != null) return;
            source.volume = TargetVolume();
        }

        private float TargetVolume() => muted ? 0f : volume;
    }
}
