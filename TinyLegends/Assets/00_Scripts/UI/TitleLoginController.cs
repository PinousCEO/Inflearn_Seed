using System;
using System.Threading.Tasks;
using IdleBattle.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    [DisallowMultipleComponent]
    public sealed class TitleLoginController : MonoBehaviour
    {
        private const string TitleSceneName = "Title";
        private const string SelectSceneName = "Select";
        private const string MainSceneName = "Main";

        [Header("테스트 스위치")]
        [Tooltip("켜면 타이틀에 들어올 때마다 남아 있는 세션(게스트/구글)을 끊고 항상 로그인 판넬부터 시작합니다.\n\n" +
                 "게스트 계정은 로그아웃하면 다시 들어갈 방법이 없어서, 그 계정에 쌓인 데이터도 함께 버려집니다.\n" +
                 "이어서 하기를 확인하거나 배포할 때는 꺼 두세요.")]
        [SerializeField] private bool forceSignOutForTesting;

        /// <summary>
        /// 타이틀 진입 시 기존 세션을 끊을지 여부입니다.
        /// Title 씬의 TitleLoginController 오브젝트에서 체크를 끄면 게스트 계정이 유지됩니다.
        /// </summary>
        public bool ForceSignOutForTesting
        {
            get => forceSignOutForTesting;
            set => forceSignOutForTesting = value;
        }

        private const string InitFailurePopupKey = "title-init-failed";
        private const string LoginFailurePopupKey = "title-login-failed";

        private GameObject tapToStart;
        private GameObject loginPanel;
        private GameObject loadingBarObject;
        private GameObject downloadPopup;
        private TMP_Text downloadText;
        private TitleLoadingBar loadingBar;
        private Button guestButton;
        private Button googleButton;
        private TMP_Text statusText;
        private bool isSigningIn;
        private bool isRouting;
        private Task<bool?> characterProbe;
        private string characterProbeUserId;
        private int initializationRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnTitleScene()
        {
            if (SceneManager.GetActiveScene().name != TitleSceneName) return;
            if (FindFirstObjectByType<TitleLoginController>() != null) return;

            var host = new GameObject(nameof(TitleLoginController));
            host.AddComponent<TitleLoginController>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            var run = ++initializationRun;
            _ = InitializeControllerAsync(run);
        }

        private void OnDisable()
        {
            initializationRun++;
            isSigningIn = false;
            isRouting = false;
            characterProbe = null;
            characterProbeUserId = null;
        }

        private async Task InitializeControllerAsync(int run)
        {
            try
            {
                BindView();

                // 초기화가 끝날 때까지는 모두 감춘다.
                SetTapToStartVisible(false);
                SetLoginPanelVisible(false);
                SetDownloadVisible(false);
                HideLoadingBar();
                SetLoginInteractable(false);

                await FirebaseInitializer.Instance.InitializeAsync();
                if (!IsCurrentInitialization(run)) return;

                SetDownloadVisible(true);
                SetDownloadText("다운로드 리소스를 확인하고 있습니다...");
                loadingBar.ShowProgress(0f);
                await AddressablesInitializer.InitializeAndDownloadAsync(progress =>
                {
                    if (!IsCurrentInitialization(run)) return;
                    loadingBar.ShowProgress(progress.Percent);
                    SetDownloadText(FormatDownloadProgress(progress));
                });
                if (!IsCurrentInitialization(run)) return;
                SetDownloadVisible(false);
                HideLoadingBar();

                if (forceSignOutForTesting)
                {
                    Debug.Log("[Title] 테스트 스위치가 켜져 있어 남아 있는 세션을 끊습니다. " +
                              "게스트 계정을 이어서 쓰려면 TitleLoginController의 체크를 끄세요.", this);
                    await FirebaseInitializer.Instance.SignOutForTestingAsync();
                    if (!IsCurrentInitialization(run)) return;
                }

                if (!forceSignOutForTesting && FirebaseInitializer.Instance.IsSignedIn)
                {
                    // 이미 로그인된 상태 -> 바로 TapToStart 노출
                    ShowTapToStart();
                }
                else
                {
                    // 로그인 필요 -> 로그인 판넬 노출
                    ShowLoginPanel();
                }
            }
            catch (Exception exception)
            {
                if (!IsCurrentInitialization(run)) return;
                Debug.LogException(exception, this);
                ShowLoginPanel();
                SetStatus("로그인 서비스를 초기화하지 못했습니다.");

                // 초기화가 실패하면 아무것도 할 수 없으므로, 재시도 버튼 하나짜리 팝업으로 막는다.
                if (!(exception is NetworkUnavailableException))
                    PopupService.Alert(
                        "접속 실패",
                        "서버에 연결하지 못했습니다.\n잠시 후 다시 시도해주세요.",
                        onConfirm: RetryInitialize,
                        confirmLabel: "재시도",
                        key: InitFailurePopupKey);
            }
        }

        private bool IsCurrentInitialization(int run)
        {
            return this != null && isActiveAndEnabled && initializationRun == run;
        }

        /// <summary>초기화 실패 팝업의 재시도. 타이틀 씬을 처음부터 다시 태웁니다.</summary>
        private void RetryInitialize()
        {
            NetworkMonitor.CheckNow(forceProbe: true);
            SceneTransition.Instance.LoadScene(TitleSceneName);
        }

        private void OnDestroy()
        {
            if (tapToStart != null && tapToStart.TryGetComponent(out Button startButton))
                startButton.onClick.RemoveListener(OnTapToStart);
            if (guestButton != null)
                guestButton.onClick.RemoveListener(OnGuestClicked);
            if (googleButton != null)
                googleButton.onClick.RemoveListener(OnGoogleClicked);
        }

        private void BindView()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var root in activeScene.GetRootGameObjects())
            {
                tapToStart ??= FindChild(root.transform, "TapToStart")?.gameObject;
                loginPanel ??= FindChild(root.transform, "Login")?.gameObject;
                loadingBarObject ??= FindChild(root.transform, "LoadingBar")?.gameObject;
                downloadPopup ??= FindChild(root.transform, "Download")?.gameObject;
                var guestObject = FindChild(root.transform, "GuestBtn")?.gameObject;
                if (guestButton == null && guestObject != null)
                    guestButton = GetOrAddButton(guestObject);
                var googleObject = FindChild(root.transform, "GoogleBtn")?.gameObject;
                if (googleButton == null && googleObject != null)
                    googleButton = GetOrAddButton(googleObject);
            }

            if (tapToStart == null || loginPanel == null || guestButton == null || googleButton == null ||
                loadingBarObject == null || downloadPopup == null)
                throw new MissingReferenceException(
                    "Title scene requires TapToStart, Login, GuestBtn, GoogleBtn, Download, and LoadingBar objects.");

            // 로딩바 오브젝트는 꺼진 상태로 대기하므로, 코루틴은 항상 켜져 있는 이 호스트에서 돌린다.
            loadingBar = GetComponent<TitleLoadingBar>();
            if (loadingBar == null) loadingBar = gameObject.AddComponent<TitleLoadingBar>();
            if (!loadingBar.Bind(loadingBarObject))
                throw new MissingReferenceException("LoadingBar requires a child Image named 'Fill'.");
            downloadText = downloadPopup.GetComponentInChildren<TMP_Text>(true);

            var startButton = GetOrAddButton(tapToStart);
            startButton.onClick.RemoveListener(OnTapToStart);
            startButton.onClick.AddListener(OnTapToStart);

            guestButton.onClick.RemoveListener(OnGuestClicked);
            guestButton.onClick.AddListener(OnGuestClicked);

            googleButton.onClick.RemoveListener(OnGoogleClicked);
            googleButton.onClick.AddListener(OnGoogleClicked);

            statusText = loginPanel.GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>
        /// TapToStart를 눌렀을 때 저장된 캐릭터 유무를 보고 분기한다.
        /// 캐릭터가 있으면 Main(로딩바 + 페이드), 없으면 Select(페이드)로 넘어간다.
        /// </summary>
        private async void OnTapToStart()
        {
            if (isSigningIn || isRouting || loadingBar.IsLoading) return;

            if (!FirebaseInitializer.Instance.IsSignedIn)
            {
                // 로그인이 풀린 예외 상황 -> 다시 로그인 판넬로
                ShowLoginPanel();
                SetStatus("로그인이 필요합니다.");
                PopupService.Toast("로그인이 필요합니다.");
                return;
            }

            // 지옥문이 열리는 낮은 종. 타이틀에서 게임으로 넘어가는 순간의 첫인상입니다.
            AudioManager.Play(SfxId.TitleTap);

            isRouting = true;
            SetTapToStartVisible(false);
            SetLoginPanelVisible(false);

            var hasCharacter = await GetCharacterProbeAsync();
            if (this == null) return;

            if (!hasCharacter.HasValue)
            {
                characterProbe = null;
                characterProbeUserId = null;
                isRouting = false;
                SetStatus("저장 데이터를 확인하지 못했습니다. 다시 시도해 주세요.");
                PopupService.Toast("저장 데이터 확인에 실패했습니다. 다시 눌러 주세요.");
                ShowTapToStart();
                return;
            }

            if (hasCharacter.Value)
            {
                // 이미 캐릭터가 있는 사용자 -> Main으로. 무거운 씬이라 로딩바로 진행한다.
                loadingBar.Load(MainSceneName);
            }
            else
            {
                // 신규 사용자 -> Select에서 직업 선택 + 이름 입력.
                loadingBar.Load(SelectSceneName);
            }
        }

        /// <summary>게스트 로그인 성공 시 씬 전환 없이 로그인 판넬만 닫고 TapToStart를 켠다.</summary>
        private void OnGuestClicked()
        {
            _ = SignInAsync(
                () => FirebaseInitializer.Instance.SignInAsGuestAsync(),
                "게스트 로그인 중...",
                "게스트 로그인에 실패했습니다. 다시 시도해주세요.");
        }

        /// <summary>구글 로그인. 게스트와 동일하게 성공 시 TapToStart로 넘어간다.</summary>
        private void OnGoogleClicked()
        {
            if (isSigningIn) return;

            if (!GoogleSignInService.IsAvailable)
            {
                Debug.LogError(GoogleSignInService.PluginMissingMessage, this);
                SetStatus("구글 로그인을 사용할 수 없습니다.");
                return;
            }

            if (Application.isEditor)
            {
                // 플러그인이 네이티브 SDK를 쓰기 때문에 에디터에서는 계정 선택 창이 뜨지 않는다.
                SetStatus("구글 로그인은 안드로이드 빌드에서만 동작합니다.");
                return;
            }

            _ = SignInAsync(
                () => FirebaseInitializer.Instance.SignInWithGoogleAsync(),
                "구글 로그인 중...",
                "구글 로그인에 실패했습니다. 다시 시도해주세요.");
        }

        /// <summary>게스트/구글 로그인이 공유하는 진행 상태 처리.</summary>
        private async Task SignInAsync(Func<Task<string>> signIn, string progressMessage, string failureMessage)
        {
            if (isSigningIn) return;

            isSigningIn = true;
            SetLoginInteractable(false);
            SetStatus(progressMessage);
            try
            {
                await signIn();
                isSigningIn = false;
                SetStatus("로그인되었습니다.");
                AudioManager.Play(SfxId.LoginSuccess);
                ShowTapToStart();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                isSigningIn = false;
                SetLoginInteractable(true);
                SetStatus(failureMessage);
                AudioManager.Play(SfxId.LoginFailed);

                // 네트워크 문제면 이미 끊김 팝업이 떠 있으니 겹쳐 띄우지 않는다.
                if (!(exception is NetworkUnavailableException))
                    PopupService.Alert("로그인 실패", failureMessage, confirmLabel: "확인", key: LoginFailurePopupKey);
            }
        }

        private void ShowTapToStart()
        {
            SetDownloadVisible(false);
            SetLoginPanelVisible(false);
            HideLoadingBar();
            SetTapToStartVisible(true);

            // TapToStart를 누르는 순간 바로 분기할 수 있도록, 저장된 캐릭터 유무를 미리 조회해 둔다.
            characterProbeUserId = FirebaseInitializer.Instance.UserId;
            characterProbe = ProbeHasCharacterAsync();
        }

        private Task<bool?> GetCharacterProbeAsync()
        {
            var currentUserId = FirebaseInitializer.Instance.UserId;
            if (characterProbe == null ||
                !string.Equals(characterProbeUserId, currentUserId, StringComparison.Ordinal))
            {
                characterProbeUserId = currentUserId;
                characterProbe = ProbeHasCharacterAsync();
            }
            return characterProbe;
        }

        /// <summary>Firestore 저장본에 캐릭터(직업)가 이미 있는지 확인한다. 있으면 Main, 없으면 Select로 간다.</summary>
        private async Task<bool?> ProbeHasCharacterAsync()
        {
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var json = await FirebaseInitializer.Instance.LoadPlayerJsonAsync();
                    if (string.IsNullOrWhiteSpace(json)) return false;

                    var probe = JsonUtility.FromJson<CharacterProbe>(json);
                    return probe != null && !string.IsNullOrWhiteSpace(probe.characterId);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"캐릭터 저장 조회 실패 ({attempt}/{maxAttempts}): {exception.Message}", this);
                    if (attempt < maxAttempts)
                        await Task.Delay(350 * attempt);
                }
            }

            return null;
        }

        /// <summary>저장 JSON에서 characterId 하나만 뽑아 보기 위한 최소 구조체.</summary>
        [Serializable]
        private sealed class CharacterProbe
        {
            public string characterId;
        }

        private void ShowLoginPanel()
        {
            SetDownloadVisible(false);
            SetTapToStartVisible(false);
            HideLoadingBar();
            SetLoginPanelVisible(true);
            SetLoginInteractable(true);
        }

        private void HideLoadingBar()
        {
            if (loadingBar != null) loadingBar.Hide();
            else if (loadingBarObject != null) loadingBarObject.SetActive(false);
        }

        private void SetDownloadVisible(bool visible)
        {
            if (downloadPopup != null) downloadPopup.SetActive(visible);
        }

        private void SetDownloadText(string message)
        {
            if (downloadText != null) downloadText.text = message;
        }

        private static string FormatDownloadProgress(AddressablesDownloadProgress progress)
        {
            if (progress.TotalBytes <= 0L)
                return "다운로드할 리소스가 없습니다.\n<color=#FFFF00>100%</color>";
            return $"필요한 리소스를 다운로드합니다.\n<color=#FFFF00>{FormatBytes(progress.DownloadedBytes)}</color>/{FormatBytes(progress.TotalBytes)}";
        }

        private static string FormatBytes(long bytes)
        {
            const double megabyte = 1024d * 1024d;
            if (bytes < megabyte) return $"{bytes / 1024d:F1}KB";
            return $"{bytes / megabyte:F1}MB";
        }

        private void SetTapToStartVisible(bool visible)
        {
            if (tapToStart != null) tapToStart.SetActive(visible);
        }

        private void SetLoginPanelVisible(bool visible)
        {
            if (loginPanel != null) loginPanel.SetActive(visible);
        }

        private void SetLoginInteractable(bool interactable)
        {
            var enabled = interactable && !isSigningIn;
            if (guestButton != null) guestButton.interactable = enabled;
            if (googleButton != null) googleButton.interactable = enabled;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private static Button GetOrAddButton(GameObject target)
        {
            if (target.TryGetComponent(out Button button)) return button;
            button = target.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            return button;
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), objectName);
                if (match != null) return match;
            }

            return null;
        }
    }
}
