using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleBattle.Audio
{
    /// <summary>
    /// 게임 전역 효과음 재생기입니다.
    ///
    /// 씬의 <c>SoundRoot/SFX_Source</c> **하나만** 써서 <see cref="AudioSource.PlayOneShot"/>로 재생합니다.
    /// PlayOneShot은 한 소스에서 소리가 겹쳐 나기 때문에, 채널을 여러 개 만들 필요가 없습니다.
    ///
    /// - <see cref="Play"/>   : 화면 어디서 나든 같은 크기로 들리는 소리(UI, 알림, 레벨업 등).
    /// - <see cref="PlayAt"/> : 월드 좌표에서 나는 소리. 카메라와의 거리만큼 음량을 줄여 멀면 작게 들립니다.
    ///
    /// 같은 소리가 한꺼번에 몰리면(초당 수십 번 터지는 타격음처럼) 뭉개지므로,
    /// <see cref="SfxLibrary.GetProfile"/>의 최소 간격으로 걸러 냅니다.
    /// 음량은 PlayerPrefs에 남아 다음 실행에서도 유지됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1500)]
    public sealed class AudioManager : MonoBehaviour
    {
        /// <summary>이 거리 안에서는 음량을 줄이지 않습니다.</summary>
        private const float FullVolumeDistance = 8f;
        /// <summary>이 거리를 넘으면 들리지 않습니다.</summary>
        private const float SilentDistance = 45f;

        private const string MasterKey = "audio_master_volume";
        private const string SfxKey = "audio_sfx_volume";
        private const string MuteKey = "audio_muted";

        [Header("설정")]
        [Tooltip("처음 켰을 때의 효과음 음량입니다.\n게임 안에서 음량을 바꾸면 그 값이 저장되어 다음부터는 그쪽이 쓰입니다.")]
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = .85f;

        [Tooltip("BGM까지 함께 줄이는 전체 음량입니다. 보통 1로 둡니다.")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;

        private static AudioManager instance;
        private static bool quitting;

        private AudioSource sfxSource;
        private AudioListener ownedListener;
        private Transform listenerTransform;
        private float masterVolume = 1f;
        private float sfxVolume = 1f;
        private bool muted;

        /// <summary>같은 효과음이 너무 촘촘히 겹치지 않도록 다음 허용 시각을 적어 둡니다.</summary>
        private readonly Dictionary<SfxId, float> nextAllowed = new();

        public static AudioManager Instance
        {
            get
            {
                if (instance != null) return instance;
                if (quitting) return null;

                instance = FindFirstObjectByType<AudioManager>();
                if (instance == null)
                {
                    // 씬에 놓인 SoundRoot가 있으면 거기에 올라탑니다. 없을 때만 새로 만들어집니다.
                    var root = AudioRoot.Resolve();
                    instance = root.GetComponent<AudioManager>() ?? root.gameObject.AddComponent<AudioManager>();
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

        /// <summary>화면 전체에 같은 크기로 들리는 소리입니다. UI와 알림에 씁니다.</summary>
        public static void Play(SfxId id, float volumeScale = 1f)
        {
            var manager = Instance;
            if (manager != null) manager.Emit(id, volumeScale, false, Vector3.zero);
        }

        /// <summary>월드 좌표에서 나는 소리입니다. 멀리서 죽은 몬스터는 작게 들립니다.</summary>
        public static void PlayAt(SfxId id, Vector3 worldPosition, float volumeScale = 1f)
        {
            var manager = Instance;
            if (manager != null) manager.Emit(id, volumeScale, true, worldPosition);
        }

        /// <summary>
        /// 클립을 미리 마련해 둡니다. 합성 효과음은 처음 재생할 때 만들어지므로,
        /// 전투가 시작된 뒤 첫 타격에서 순간적으로 끊기지 않도록 씬 진입 때 불러 둡니다.
        /// </summary>
        public static void Prewarm(params SfxId[] ids)
        {
            var manager = Instance;
            if (manager == null || ids == null || ids.Length == 0) return;
            manager.StartCoroutine(manager.PrewarmRoutine(ids));
        }

        /// <summary>0~1. 전체 음량입니다.</summary>
        public static float MasterVolume
        {
            get => Instance != null ? Instance.masterVolume : 1f;
            set
            {
                var manager = Instance;
                if (manager == null) return;
                manager.masterVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterKey, manager.masterVolume);
            }
        }

        /// <summary>0~1. 효과음 음량입니다.</summary>
        public static float SfxVolume
        {
            get => Instance != null ? Instance.sfxVolume : 1f;
            set
            {
                var manager = Instance;
                if (manager == null) return;
                manager.sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxKey, manager.sfxVolume);
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
                if (value) manager.StopAll();
            }
        }

        public static void StopEverything() => Instance?.StopAll();

        // ------------------------------------------------------------------
        // 내부 구현
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                // SoundRoot는 BGM·UI 바인더와 함께 쓰는 오브젝트이므로,
                // 중복일 때 오브젝트째 지우면 안 되고 이 컴포넌트만 걷어 냅니다.
                Destroy(this);
                return;
            }

            instance = this;
            AudioRoot.Resolve();

            BindSource();
            // 게임 안에서 조절해 저장해 둔 값이 있으면 그쪽이 우선입니다.
            masterVolume = PlayerPrefs.GetFloat(MasterKey, defaultMasterVolume);
            sfxVolume = PlayerPrefs.GetFloat(SfxKey, defaultSfxVolume);
            muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;

            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureListener();
        }

        private void Start()
        {
            // 첫 씬에서는 sceneLoaded를 놓칠 수 있어, 씬이 다 뜬 뒤 한 번 더 확인합니다.
            StartCoroutine(EnsureListenerNextFrame());
        }

        private IEnumerator EnsureListenerNextFrame()
        {
            yield return null;
            EnsureListener();
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
            // 이전 씬의 소리가 새 씬으로 새어 나오지 않게 합니다.
            StopAll();
            nextAllowed.Clear();
            EnsureListener();
            StartCoroutine(EnsureListenerNextFrame());
        }

        /// <summary>씬의 SFX_Source를 붙잡습니다. 효과음은 이 소스 하나로만 재생합니다.</summary>
        private void BindSource()
        {
            sfxSource = AudioRoot.GetOrCreateSource(transform, AudioRoot.SfxSourceName);
            if (sfxSource == null) return;

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            // PlayOneShot은 이 소스의 volume을 곱해서 재생하므로 1로 두고,
            // 실제 크기는 재생할 때마다 넘기는 값으로 정합니다.
            sfxSource.volume = 1f;
            sfxSource.spatialBlend = 0f;
            sfxSource.pitch = 1f;
            // 일시정지 연출이 들어와도 UI 소리는 계속 들려야 합니다.
            sfxSource.ignoreListenerPause = true;
            sfxSource.Stop();
        }

        /// <summary>
        /// 씬에 AudioListener가 하나도 없으면 아무 소리도 들리지 않습니다.
        /// 카메라가 없는 화면(타이틀 등)에서도 소리가 나도록 없을 때만 임시로 하나 답니다.
        /// </summary>
        private void EnsureListener()
        {
            var sceneListener = FindSceneListener();
            if (sceneListener != null)
            {
                if (ownedListener != null) Destroy(ownedListener);
                ownedListener = null;
                listenerTransform = sceneListener.transform;
                return;
            }

            if (ownedListener != null) return;

            var camera = Camera.main;
            var host = camera != null ? camera.gameObject : gameObject;
            ownedListener = host.AddComponent<AudioListener>();
            listenerTransform = ownedListener.transform;
        }

        /// <summary>우리가 붙인 것을 뺀, 씬이 원래 들고 있는 리스너입니다.</summary>
        private AudioListener FindSceneListener()
        {
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener != null && listener != ownedListener) return listener;
            return null;
        }

        private void Emit(SfxId id, float volumeScale, bool spatial, Vector3 worldPosition)
        {
            if (id == SfxId.None || muted || sfxSource == null) return;

            var profile = SfxLibrary.GetProfile(id);
            var now = Time.unscaledTime;
            if (nextAllowed.TryGetValue(id, out var allowedAt) && now < allowedAt) return;
            nextAllowed[id] = now + profile.MinInterval;

            var volume = profile.Volume * volumeScale * sfxVolume * masterVolume;
            if (spatial) volume *= DistanceFalloff(worldPosition);
            if (volume <= .003f) return;

            var clip = SfxLibrary.Get(id);
            if (clip == null) return;

            // 소스 하나에서 소리가 겹쳐 나므로, 채널을 따로 두지 않아도 됩니다.
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>
        /// 소스가 2D라 위치로는 거리감이 생기지 않으므로, 카메라와의 거리만큼 음량을 줄입니다.
        /// 멀리서 죽은 몬스터가 코앞에서 죽은 것과 같은 크기로 들리지 않게 하는 것이 목적입니다.
        /// </summary>
        private float DistanceFalloff(Vector3 worldPosition)
        {
            if (listenerTransform == null) return 1f;
            var distance = Vector3.Distance(listenerTransform.position, worldPosition);
            if (distance <= FullVolumeDistance) return 1f;
            return 1f - Mathf.Clamp01((distance - FullVolumeDistance) / (SilentDistance - FullVolumeDistance));
        }

        private void StopAll()
        {
            if (sfxSource != null) sfxSource.Stop();
        }

        /// <summary>한 프레임에 몰아 만들면 그 프레임이 튀므로, 몇 개씩 나눠 만듭니다.</summary>
        private IEnumerator PrewarmRoutine(SfxId[] ids)
        {
            const int perFrame = 3;
            for (var i = 0; i < ids.Length; i++)
            {
                SfxLibrary.Get(ids[i]);
                if ((i + 1) % perFrame == 0) yield return null;
            }
        }
    }
}
