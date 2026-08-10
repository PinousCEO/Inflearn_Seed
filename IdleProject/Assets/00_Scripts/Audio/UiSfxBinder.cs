using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle.Audio
{
    /// <summary>
    /// 씬에 있는 모든 <see cref="Button"/>에 공통 클릭음을 붙입니다.
    /// 버튼 하나하나에 코드를 넣지 않아도 화면 어디를 눌러도 반응이 오게 하는 것이 목적입니다.
    ///
    /// 상황을 알리는 소리(장착 완료, 판넬 열림, 우편 수령 등)는 각 컨트롤러가 따로 냅니다.
    /// 여기서 나는 클릭음은 그 아래에 얇게 깔리는 "눌렸다"는 촉감입니다.
    ///
    /// 런타임에 만들어지는 UI(팝업, 복제한 목록 줄)는 만든 쪽에서 <see cref="Bind"/>를 불러 주세요.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1400)]
    public sealed class UiSfxBinder : MonoBehaviour
    {
        private static UiSfxBinder instance;
        private static bool quitting;

        /// <summary>
        /// 이미 소리를 붙인 버튼입니다.
        ///
        /// 씬이 바뀔 때 통째로 비우면, 컨트롤러가 Awake에서 먼저 붙여 둔 기록까지 지워져
        /// 같은 버튼에 소리가 두 번 걸립니다(sceneLoaded는 씬의 Awake보다 뒤에 옵니다).
        /// 그래서 비우지 않고, 파괴된 버튼만 걸러 냅니다.
        /// </summary>
        private static readonly HashSet<Button> bound = new();

        /// <summary>기본 클릭음 대신 다른 소리를 낼 버튼입니다. 키는 버튼이 붙은 오브젝트입니다.</summary>
        private static readonly Dictionary<GameObject, SfxId> overrides = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            quitting = false;
            // Domain Reload를 끈 상태에서 남아 있는 지난 플레이의 기록만 걷어 냅니다.
            // 통째로 비우면 이 시점 이전에 이미 붙인 첫 씬의 기록까지 지워져 소리가 두 번 걸립니다.
            Prune();

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure()?.ScheduleSceneBind();
        }

        private static UiSfxBinder Ensure()
        {
            if (instance != null) return instance;
            if (quitting) return null;

            instance = FindFirstObjectByType<UiSfxBinder>();
            if (instance == null)
            {
                // 사운드 관련 컴포넌트는 모두 SoundRoot 하나에 모읍니다.
                var root = AudioRoot.Resolve();
                instance = root.GetComponent<UiSfxBinder>() ?? root.gameObject.AddComponent<UiSfxBinder>();
            }
            return instance;
        }

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
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnApplicationQuit() => quitting = true;

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            Ensure()?.ScheduleSceneBind();
        }

        // ------------------------------------------------------------------
        // 공개 API
        // ------------------------------------------------------------------

        /// <summary>이 버튼만 다른 소리를 내게 합니다. <see cref="Bind"/>보다 먼저 불러야 반영됩니다.</summary>
        public static void SetSound(Component target, SfxId id)
        {
            if (target == null) return;
            overrides[target.gameObject] = id;
        }

        /// <summary>버튼이 아예 소리를 내지 않게 합니다.</summary>
        public static void Silence(Component target) => SetSound(target, SfxId.None);

        /// <summary>이 오브젝트 아래의 모든 버튼에 클릭음을 붙입니다. 이미 붙은 버튼은 건너뜁니다.</summary>
        public static void Bind(GameObject root)
        {
            if (root == null) return;
            foreach (var button in root.GetComponentsInChildren<Button>(true)) BindButton(button);
        }

        public static void Bind(Component root)
        {
            if (root != null) Bind(root.gameObject);
        }

        /// <summary>현재 씬 전체를 훑어 붙입니다. 씬 로드 직후 한 번만 일어납니다.</summary>
        public static void BindScene()
        {
            Prune();
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                BindButton(button);
        }

        // ------------------------------------------------------------------
        // 내부 구현
        // ------------------------------------------------------------------

        private static void BindButton(Button button)
        {
            if (button == null) return;
            if (!bound.Add(button)) return;

            var sound = overrides.TryGetValue(button.gameObject, out var custom) ? custom : SfxId.UiClick;
            if (sound == SfxId.None) return;

            button.onClick.AddListener(() => AudioManager.Play(sound));
        }

        /// <summary>파괴된 버튼의 기록을 걷어 냅니다. 씬을 오래 오가도 기록이 쌓이지 않게 합니다.</summary>
        private static void Prune()
        {
            bound.RemoveWhere(button => button == null);

            // Dictionary는 순회 중에 지울 수 없으므로 먼저 모아 둡니다.
            List<GameObject> dead = null;
            foreach (var pair in overrides)
            {
                if (pair.Key != null) continue;
                dead ??= new List<GameObject>();
                dead.Add(pair.Key);
            }

            if (dead == null) return;
            foreach (var key in dead) overrides.Remove(key);
        }

        private void ScheduleSceneBind()
        {
            if (!isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(BindSceneRoutine());
        }

        /// <summary>
        /// 컨트롤러들이 Awake·Start에서 버튼을 만들고 붙이므로, 한 프레임 기다렸다가 훑습니다.
        /// 그래야 코드로 생성된 버튼까지 한 번에 잡힙니다.
        /// </summary>
        private IEnumerator BindSceneRoutine()
        {
            yield return null;
            BindScene();
        }
    }
}
