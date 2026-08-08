using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

namespace IdleBattle
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1100)]
    public sealed class FirebaseInitializer : MonoBehaviour
    {
        private const string PlayerCollection = "USERS";
        private static FirebaseInitializer instance;
        private Task initializationTask;
        private FirebaseFirestore firestore;
        private FirebaseAuth auth;

        public static FirebaseInitializer Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<FirebaseInitializer>();
                if (instance == null)
                    instance = new GameObject(nameof(FirebaseInitializer)).AddComponent<FirebaseInitializer>();
                return instance;
            }
        }

        public bool IsReady { get; private set; }
        public bool IsSignedIn => auth?.CurrentUser != null;
        public bool IsGuest => auth?.CurrentUser?.IsAnonymous == true;
        public string UserId => auth?.CurrentUser?.UserId;
        public string DisplayName => auth?.CurrentUser?.DisplayName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            _ = Instance.InitializeAsync();
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
            _ = InitializeAsync();
        }

        public Task InitializeAsync() => initializationTask ??= InitializeInternalAsync();

        public async Task<string> SignInAsGuestAsync()
        {
            await InitializeAsync();
            if (auth.CurrentUser == null)
            {
                Debug.Log("Signing in to Firebase as an anonymous guest...", this);
                try
                {
                    await auth.SignInAnonymouslyAsync();
                }
                catch (FirebaseException exception)
                {
                    throw new InvalidOperationException(
                        "Anonymous Firebase login failed. Enable the Anonymous provider in Firebase Console > Authentication > Sign-in method.",
                        exception);
                }
            }

            if (auth.CurrentUser == null)
                throw new InvalidOperationException("Anonymous Firebase login completed without a user.");

            IsReady = true;
            Debug.Log($"Firebase guest login succeeded. User: {auth.CurrentUser.UserId}", this);
            return auth.CurrentUser.UserId;
        }

        /// <summary>
        /// 구글 계정으로 로그인한다. ID 토큰은 GoogleSignInService가 네이티브 플러그인에서 받아온다.
        /// </summary>
        public async Task<string> SignInWithGoogleAsync()
        {
            await InitializeAsync();

            var idToken = await GoogleSignInService.GetIdTokenAsync();
            var credential = GoogleAuthProvider.GetCredential(idToken, null);

            Debug.Log("Signing in to Firebase with a Google credential...", this);
            try
            {
                await auth.SignInAndRetrieveDataWithCredentialAsync(credential);
            }
            catch (FirebaseException exception)
            {
                throw new InvalidOperationException(
                    "Google Firebase login failed. Enable the Google provider in Firebase Console > Authentication > Sign-in method.",
                    exception);
            }

            if (auth.CurrentUser == null)
                throw new InvalidOperationException("Google Firebase login completed without a user.");

            IsReady = true;
            Debug.Log($"Firebase Google login succeeded. User: {auth.CurrentUser.UserId}", this);
            return auth.CurrentUser.UserId;
        }

        /// <summary>
        /// 타이틀 화면 테스트용. 남아 있는 세션(게스트/구글)을 끊어서 항상 로그인 판넬부터 시작하게 한다.
        /// </summary>
        public async Task SignOutForTestingAsync()
        {
            await InitializeAsync();

            // 구글 세션도 같이 끊어야 다음 로그인 때 계정 선택 창이 다시 뜬다.
            GoogleSignInService.SignOut();

            if (auth.CurrentUser == null) return;

            Debug.Log($"Signing out existing session for title-screen testing. User: {auth.CurrentUser.UserId}", this);
            auth.SignOut();
            IsReady = false;
        }

        public async Task<string> LoadPlayerJsonAsync()
        {
            await InitializeAsync();
            var snapshot = await GetPlayerDocument().GetSnapshotAsync(Source.Default);
            return snapshot.Exists && snapshot.TryGetValue("payload", out string payload) ? payload : null;
        }

        public async Task SavePlayerJsonAsync(string json, int version)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Save payload is empty.", nameof(json));
            await InitializeAsync();
            await GetPlayerDocument().SetAsync(new Dictionary<string, object>
            {
                ["payload"] = json,
                ["version"] = version,
                ["updatedAt"] = FieldValue.ServerTimestamp
            }, SetOptions.MergeAll);
        }

        private async Task InitializeInternalAsync()
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
                throw new InvalidOperationException($"Firebase dependencies are unavailable: {status}");

            auth = FirebaseAuth.DefaultInstance;
            firestore = FirebaseFirestore.DefaultInstance;
            IsReady = auth.CurrentUser != null;
            Debug.Log(IsReady
                ? $"Firebase initialized with an existing user. User: {auth.CurrentUser.UserId}"
                : "Firebase initialized. Waiting for a title-screen login.", this);
        }

        private DocumentReference GetPlayerDocument()
        {
            if (!IsReady || auth?.CurrentUser == null)
                throw new InvalidOperationException("Firebase has not been initialized.");
            return firestore.Collection(PlayerCollection).Document(auth.CurrentUser.UserId);
        }
    }
}
