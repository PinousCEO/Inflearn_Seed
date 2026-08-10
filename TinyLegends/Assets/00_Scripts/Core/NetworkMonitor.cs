using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using IdleBattle.Audio;
using IdleBattle.UI;

namespace IdleBattle
{
    /// <summary>네트워크가 끊긴 상태에서 외부 통신을 시도했을 때 던집니다.</summary>
    public sealed class NetworkUnavailableException : Exception
    {
        public NetworkUnavailableException()
            : base("네트워크에 연결되어 있지 않습니다.") { }
    }

    /// <summary>
    /// 인터넷 연결 상태를 감시합니다. 게임을 켤 때 코드로 한 번 생성되어 모든 씬에서 살아 있습니다.
    ///
    /// 검사는 두 단계입니다.
    /// 1) <see cref="Application.internetReachability"/> — 비용이 0이라 자주 봅니다. 단말의 통신 수단만 알려 줍니다.
    /// 2) HTTP 프로브 — 실제로 밖으로 나가지는지 확인합니다. 데이터/배터리를 쓰므로 드물게 돕니다.
    ///    (와이파이는 잡혔는데 인터넷은 안 되는 상황을 1)만으로는 못 잡습니다.)
    ///
    /// 주기 (인스펙터에서 조절 가능)
    /// - 온라인일 때 : 10초마다 1)만 확인, 60초마다 2)까지 확인
    /// - 오프라인일 때 : 3초마다 2)까지 확인(복구를 빨리 잡기 위해)
    /// - 앱 포커스 복귀 / 절전 해제, 재시도 버튼 : 주기를 무시하고 즉시 2)까지 확인
    /// - Firebase 같은 외부 SDK 호출 직전(<see cref="EnsureOnlineAsync"/>) :
    ///   1)은 항상 확인하고, 2)는 마지막 확인이 60초를 넘었을 때만 확인합니다.
    ///   저장이 아이템마다 일어나기 때문에, 통신마다 프로브를 던지면 데이터와 지연이 커집니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1200)]
    public sealed class NetworkMonitor : MonoBehaviour
    {
        /// <summary>네트워크 끊김 팝업을 중복해서 열지 않도록 쓰는 키입니다.</summary>
        public const string OfflinePopupKey = "network-offline";

        [Header("검사 주기(초)")]
        [Tooltip("온라인일 때 단말 연결 상태만 확인하는 주기입니다. 비용이 없어 짧게 잡아도 됩니다.")]
        [SerializeField, Min(1f)] private float onlinePollInterval = 10f;

        [Tooltip("온라인일 때 실제로 서버까지 닿는지 확인하는 주기입니다. 데이터를 쓰므로 길게 잡습니다.")]
        [SerializeField, Min(5f)] private float onlineProbeInterval = 60f;

        [Tooltip("오프라인일 때 복구를 확인하는 주기입니다. 짧게 잡아 빨리 복구를 잡습니다.")]
        [SerializeField, Min(1f)] private float offlinePollInterval = 3f;

        [Header("프로브")]
        [Tooltip("연결 확인용 주소입니다. 응답 본문이 없는 204 엔드포인트라 가볍습니다.")]
        [SerializeField] private string probeUrl = "https://clients3.google.com/generate_204";

        [SerializeField, Min(1)] private int probeTimeoutSeconds = 5;

        [Tooltip("몇 번 연속으로 실패해야 '끊김'으로 볼지. 순간적인 실패로 팝업이 깜빡이는 것을 막습니다.")]
        [SerializeField, Min(1)] private int failureThreshold = 2;

        [Header("알림")]
        [Tooltip("끊김을 감지했을 때 자동으로 팝업을 띄웁니다.")]
        [SerializeField] private bool showPopupOnDisconnect = true;

        private static NetworkMonitor instance;
        private static int mainThreadId;
        private static bool online = true;

        private int consecutiveFailures;
        private float lastProbeTime = float.NegativeInfinity;
        private Coroutine loopRoutine;
        private Coroutine immediateRoutine;

        public static NetworkMonitor Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<NetworkMonitor>();
                if (instance == null)
                    instance = new GameObject(nameof(NetworkMonitor)).AddComponent<NetworkMonitor>();
                return instance;
            }
        }

        /// <summary>마지막 검사 기준 연결 상태입니다. 검사를 직접 돌리지 않으므로 어느 스레드에서든 읽어도 됩니다.</summary>
        public static bool IsOnline => online;

        /// <summary>연결 상태가 바뀔 때만 호출됩니다(true = 연결됨).</summary>
        public static event Action<bool> StatusChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            online = true;
            _ = Instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable() => loopRoutine ??= StartCoroutine(MonitorRoutine());

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // 다른 앱을 보다 돌아오면 그 사이에 연결이 바뀌었을 수 있다.
            if (hasFocus) CheckNow(forceProbe: true);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) CheckNow(forceProbe: true);
        }

        // ------------------------------------------------------------------
        // 공개 API
        // ------------------------------------------------------------------

        /// <summary>주기를 기다리지 않고 즉시 한 번 검사합니다. 결과는 <see cref="IsOnline"/>에 반영됩니다.</summary>
        public static void CheckNow(bool forceProbe = false)
        {
            var monitor = instance;
            if (monitor == null || !monitor.isActiveAndEnabled) return;
            if (monitor.immediateRoutine != null) return; // 이미 검사 중.
            monitor.immediateRoutine = monitor.StartCoroutine(monitor.ImmediateCheckRoutine(forceProbe));
        }

        /// <summary>
        /// 코루틴에서 통신 직전에 쓰는 검사입니다.
        /// <c>yield return NetworkMonitor.EnsureOnlineRoutine();</c> 뒤에 <see cref="IsOnline"/>을 보면 됩니다.
        /// </summary>
        public static IEnumerator EnsureOnlineRoutine(bool showPopup = true)
        {
            var monitor = instance;
            if (monitor == null) yield break;

            // 통신마다 부르는 자리이므로 프로브를 강제하지 않는다.
            // 비행기 모드는 즉시 잡히고, 실제 프로브는 onlineProbeInterval마다 한 번만 나간다.
            yield return monitor.EvaluateRoutine(forceProbe: false, announce: showPopup);
        }

        /// <summary>
        /// Firebase처럼 async/await로 부르는 외부 SDK 통신 직전에 씁니다.
        /// 오프라인이면 팝업을 띄우고 false를 돌려줍니다.
        /// </summary>
        public static Task<bool> EnsureOnlineAsync(bool showPopup = true)
        {
            var monitor = instance;

            // Firebase 콜백 스레드에서 불릴 수도 있다. 그때는 Unity API를 만지지 않고 마지막 상태만 돌려준다.
            if (monitor == null || !monitor.isActiveAndEnabled ||
                Thread.CurrentThread.ManagedThreadId != mainThreadId)
                return Task.FromResult(online);

            var completion = new TaskCompletionSource<bool>();
            monitor.StartCoroutine(monitor.EnsureOnlineAsyncRoutine(showPopup, completion));
            return completion.Task;
        }

        /// <summary>
        /// 오프라인이면 <see cref="NetworkUnavailableException"/>을 던집니다.
        /// 외부 SDK 호출을 감싸는 쪽에서 한 줄로 쓰기 위한 형태입니다.
        /// </summary>
        public static async Task RequireOnlineAsync(bool showPopup = true)
        {
            if (!await EnsureOnlineAsync(showPopup)) throw new NetworkUnavailableException();
        }

        // ------------------------------------------------------------------
        // 내부 구현
        // ------------------------------------------------------------------

        private IEnumerator EnsureOnlineAsyncRoutine(bool showPopup, TaskCompletionSource<bool> completion)
        {
            yield return EvaluateRoutine(forceProbe: false, announce: showPopup);
            completion.TrySetResult(online);
        }

        private IEnumerator ImmediateCheckRoutine(bool forceProbe)
        {
            yield return EvaluateRoutine(forceProbe, announce: true);
            immediateRoutine = null;
        }

        private IEnumerator MonitorRoutine()
        {
            // 첫 검사는 곧바로 한 번 돌려서 시작부터 상태를 맞춰 둔다.
            yield return EvaluateRoutine(forceProbe: true, announce: true);

            while (true)
            {
                var interval = online ? onlinePollInterval : offlinePollInterval;
                yield return new WaitForSecondsRealtime(interval);

                // 오프라인일 때는 매번 찔러 복구를 빨리 잡고,
                // 온라인일 때는 EvaluateRoutine이 onlineProbeInterval을 보고 알아서 건너뛴다.
                yield return EvaluateRoutine(forceProbe: !online, announce: true);
            }
        }

        /// <summary>한 번의 검사입니다. 단말 연결을 먼저 보고, 필요할 때만 실제 프로브를 던집니다.</summary>
        private IEnumerator EvaluateRoutine(bool forceProbe, bool announce)
        {
            // 1단계: 비행기 모드처럼 통신 수단 자체가 없으면 여기서 끝난다(비용 0).
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                ApplyResult(false, announce);
                yield break;
            }

            // 2단계: 실제로 밖에 닿는지 확인한다. 통신할 때마다 찌르면 데이터도 지연도 커지므로,
            // 마지막 확인이 충분히 최근이면 그 결과를 그대로 쓴다.
            // (오프라인일 때는 간격이 짧아 복구를 금방 잡는다.)
            var minimumGap = online ? onlineProbeInterval : offlinePollInterval;
            if (!forceProbe && Time.realtimeSinceStartup - lastProbeTime < minimumGap) yield break;

            var succeeded = false;
            yield return ProbeRoutine(result => succeeded = result);
            lastProbeTime = Time.realtimeSinceStartup;
            ApplyResult(succeeded, announce);
        }

        private IEnumerator ProbeRoutine(Action<bool> onResult)
        {
            using var request = UnityWebRequest.Head(probeUrl);
            request.timeout = probeTimeoutSeconds;
            request.redirectLimit = 0;
            request.disposeDownloadHandlerOnDispose = true;

            yield return request.SendWebRequest();

            // 204/200은 물론, 프록시가 4xx를 돌려줘도 '밖으로 나가긴 했다'는 뜻이므로 연결로 본다.
            var connected = request.result != UnityWebRequest.Result.ConnectionError &&
                            request.result != UnityWebRequest.Result.DataProcessingError;
            onResult(connected);
        }

        private void ApplyResult(bool succeeded, bool announce)
        {
            if (succeeded)
            {
                consecutiveFailures = 0;
                if (online) return;

                online = true;
                if (announce) OnReconnected();
                StatusChanged?.Invoke(true);
                return;
            }

            consecutiveFailures++;
            if (online && consecutiveFailures < failureThreshold) return;

            var wasOnline = online;
            online = false;
            if (wasOnline)
            {
                Debug.LogWarning("[Network] 인터넷 연결이 끊어졌습니다.", this);
                // 상태가 바뀌는 순간에만 알립니다. 끊긴 채로 검사가 반복되어도 소리는 한 번뿐입니다.
                AudioManager.Play(SfxId.NetworkLost);
            }

            // 아직 끊긴 상태라면 매 검사마다 부른다. 팝업을 닫아 버린 뒤에도 다시 안내하기 위해서다.
            // 같은 key를 쓰므로 이미 떠 있으면 새로 열리지 않는다.
            if (announce) ShowOfflinePopup();
            if (wasOnline) StatusChanged?.Invoke(false);
        }

        private void ShowOfflinePopup()
        {
            if (!showPopupOnDisconnect) return;

            PopupService.Alert(
                "네트워크 연결",
                "네트워크 연결이 해제되었습니다.\n인터넷 상태를 확인해주세요.",
                onConfirm: () => CheckNow(forceProbe: true),
                confirmLabel: "재시도",
                key: OfflinePopupKey);
        }

        private void OnReconnected()
        {
            // 복구는 따로 알리지 않는다. 끊김 팝업이 사라지는 것으로 충분하다.
            Debug.Log("[Network] 인터넷에 다시 연결되었습니다.", this);
            AudioManager.Play(SfxId.NetworkRestored);
            PopupService.CloseKeyed(OfflinePopupKey);
        }
    }
}
