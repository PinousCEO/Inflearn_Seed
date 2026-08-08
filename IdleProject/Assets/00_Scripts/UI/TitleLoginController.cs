using System;
using System.Threading.Tasks;
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
        private const string MainSceneName = "Main";

        /// <summary>
        /// 테스트용 스위치. true면 타이틀 진입 시 기존 세션(게스트/구글)을 끊어서
        /// 항상 로그인 판넬부터 시작하게 만든다. 실제 배포 시에는 false로 둘 것.
        /// </summary>
        private const bool ForceSignOutForTesting = true;

        private GameObject tapToStart;
        private GameObject loginPanel;
        private GameObject loadingBarObject;
        private TitleLoadingBar loadingBar;
        private Button guestButton;
        private Button googleButton;
        private TMP_Text statusText;
        private bool isSigningIn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnTitleScene()
        {
            if (SceneManager.GetActiveScene().name != TitleSceneName) return;
            if (FindFirstObjectByType<TitleLoginController>() != null) return;

            var host = new GameObject(nameof(TitleLoginController));
            host.AddComponent<TitleLoginController>();
        }

        private async void Start()
        {
            try
            {
                BindView();

                // 초기화가 끝날 때까지는 모두 감춘다.
                SetTapToStartVisible(false);
                SetLoginPanelVisible(false);
                HideLoadingBar();
                SetLoginInteractable(false);

                await FirebaseInitializer.Instance.InitializeAsync();

                if (ForceSignOutForTesting)
                    await FirebaseInitializer.Instance.SignOutForTestingAsync();

                if (FirebaseInitializer.Instance.IsSignedIn)
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
                Debug.LogException(exception, this);
                ShowLoginPanel();
                SetStatus("로그인 서비스를 초기화하지 못했습니다.");
            }
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
                var guestObject = FindChild(root.transform, "GuestBtn")?.gameObject;
                if (guestButton == null && guestObject != null)
                    guestButton = GetOrAddButton(guestObject);
                var googleObject = FindChild(root.transform, "GoogleBtn")?.gameObject;
                if (googleButton == null && googleObject != null)
                    googleButton = GetOrAddButton(googleObject);
            }

            if (tapToStart == null || loginPanel == null || guestButton == null || googleButton == null ||
                loadingBarObject == null)
                throw new MissingReferenceException(
                    "Title scene requires TapToStart, Login, GuestBtn, GoogleBtn, and LoadingBar objects.");

            // 로딩바 오브젝트는 꺼진 상태로 대기하므로, 코루틴은 항상 켜져 있는 이 호스트에서 돌린다.
            loadingBar = gameObject.AddComponent<TitleLoadingBar>();
            if (!loadingBar.Bind(loadingBarObject))
                throw new MissingReferenceException("LoadingBar requires a child Image named 'Fill'.");

            var startButton = GetOrAddButton(tapToStart);
            startButton.onClick.RemoveListener(OnTapToStart);
            startButton.onClick.AddListener(OnTapToStart);

            guestButton.onClick.RemoveListener(OnGuestClicked);
            guestButton.onClick.AddListener(OnGuestClicked);

            googleButton.onClick.RemoveListener(OnGoogleClicked);
            googleButton.onClick.AddListener(OnGoogleClicked);

            statusText = loginPanel.GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>TapToStart를 눌렀을 때 비로소 Main 씬 로딩을 시작한다.</summary>
        private void OnTapToStart()
        {
            if (isSigningIn || loadingBar.IsLoading) return;

            if (!FirebaseInitializer.Instance.IsSignedIn)
            {
                // 로그인이 풀린 예외 상황 -> 다시 로그인 판넬로
                ShowLoginPanel();
                SetStatus("로그인이 필요합니다.");
                return;
            }

            SetTapToStartVisible(false);
            SetLoginPanelVisible(false);
            loadingBar.Load(MainSceneName);
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
                ShowTapToStart();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                isSigningIn = false;
                SetLoginInteractable(true);
                SetStatus(failureMessage);
            }
        }

        private void ShowTapToStart()
        {
            SetLoginPanelVisible(false);
            HideLoadingBar();
            SetTapToStartVisible(true);
        }

        private void ShowLoginPanel()
        {
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
