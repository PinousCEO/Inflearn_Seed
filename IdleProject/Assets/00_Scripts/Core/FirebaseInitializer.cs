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

        /// <summary>
        /// 랭킹 컬렉션입니다. 로그인한 사람이면 누구나 읽고, 쓰기는 자기 문서만 됩니다.
        /// 세이브 전체가 담긴 <see cref="PlayerCollection"/>과 나눠 두어야 남의 저장 데이터가 새지 않습니다.
        /// </summary>
        private const string RankingCollection = "RANKING";

        /// <summary>랭킹 문서를 정렬하는 필드입니다. 단일 필드 정렬이라 색인을 따로 만들 필요가 없습니다.</summary>
        public const string RankingPowerField = "power";

        /// <summary>읽어 온 랭킹 맵에 문서 ID를 넣어 주는 키입니다.</summary>
        public const string RankingUserIdKey = "uid";

        /// <summary>한 번에 읽어 올 수 있는 랭킹 인원의 상한입니다.</summary>
        public const int RankingPageLimit = 100;

        /// <summary>유저 문서 안에서 우편함이 쓰는 필드 이름입니다.</summary>
        public const string PostField = "POST";

        /// <summary>서버 시각을 찍어 보고 되읽는 칸입니다. 오프라인 보상이 "지금"을 알아내는 데 씁니다.</summary>
        private const string ClockProbeField = "clockProbe";

        /// <summary>마지막으로 게임에 붙어 있던 서버 시각입니다. 오프라인 시간의 기준점입니다.</summary>
        private const string LastSeenField = "lastSeenAt";

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

        public Task InitializeAsync()
        {
            // 실패한 초기화를 그대로 캐싱해 두면 네트워크가 돌아와도 영영 다시 시도하지 못한다.
            if (initializationTask != null && (initializationTask.IsFaulted || initializationTask.IsCanceled))
                initializationTask = null;
            return initializationTask ??= InitializeInternalAsync();
        }

        public async Task<string> SignInAsGuestAsync()
        {
            await NetworkMonitor.RequireOnlineAsync();
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
            await NetworkMonitor.RequireOnlineAsync();
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

            // This method is called only when the explicit title test switch is ON.
            // Sign out anonymous users as well so the next Guest login creates a new UID
            // and therefore routes to Select instead of loading the previous guest save.
            GoogleSignInService.SignOut();

            if (auth.CurrentUser == null) return;

            Debug.Log($"Signing out existing session for title-screen testing. User: {auth.CurrentUser.UserId}", this);
            auth.SignOut();
            IsReady = false;
        }

        public async Task<string> LoadPlayerJsonAsync()
        {
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();
            var snapshot = await GetPlayerDocument().GetSnapshotAsync(Source.Default);
            return snapshot.Exists && snapshot.TryGetValue("payload", out string payload) ? payload : null;
        }

        public async Task SavePlayerJsonAsync(string json, int version)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Save payload is empty.", nameof(json));
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();
            await GetPlayerDocument().SetAsync(new Dictionary<string, object>
            {
                ["payload"] = json,
                ["version"] = version,
                ["updatedAt"] = FieldValue.ServerTimestamp
            }, SetOptions.MergeAll);
        }

        /// <summary>
        /// 유저 문서의 POST 필드(우편 목록)를 통째로 읽습니다.
        /// 문서나 필드가 아직 없으면 null을 돌려줍니다(빈 배열과 구분해서 첫 지급을 판단합니다).
        /// </summary>
        public async Task<List<object>> LoadPostAsync()
        {
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();
            var snapshot = await GetPlayerDocument().GetSnapshotAsync(Source.Default);
            if (!snapshot.Exists) return null;
            return snapshot.TryGetValue(PostField, out List<object> post) ? post : null;
        }

        /// <summary>POST 필드를 통째로 덮어씁니다. 다른 필드(payload 등)는 건드리지 않습니다.</summary>
        public async Task SavePostAsync(List<object> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();
            await GetPlayerDocument().SetAsync(new Dictionary<string, object>
            {
                [PostField] = entries,
                ["postUpdatedAt"] = FieldValue.ServerTimestamp
            }, SetOptions.MergeAll);
        }

        /// <summary>
        /// 받은 우편을 POST에서 지웁니다. 관리자 페이지가 같은 순간에 우편을 넣을 수 있으므로,
        /// 배열을 통째로 덮어쓰지 않고 트랜잭션 안에서 다시 읽어 해당 id만 걷어냅니다.
        /// 남은 배열(그 사이 새로 들어온 우편 포함)을 돌려줍니다.
        /// </summary>
        public async Task<List<object>> ClaimPostEntriesAsync(ICollection<string> claimedIds)
        {
            if (claimedIds == null) throw new ArgumentNullException(nameof(claimedIds));
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();

            var reference = GetPlayerDocument();
            return await firestore.RunTransactionAsync(async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(reference);
                var post = snapshot.Exists && snapshot.TryGetValue(PostField, out List<object> value)
                    ? value
                    : new List<object>();

                var remaining = new List<object>(post.Count);
                foreach (var entry in post)
                    if (!HasClaimedId(entry, claimedIds))
                        remaining.Add(entry);

                transaction.Set(reference, new Dictionary<string, object>
                {
                    [PostField] = remaining,
                    ["postUpdatedAt"] = FieldValue.ServerTimestamp
                }, SetOptions.MergeAll);

                return remaining;
            });
        }

        /// <summary>
        /// 내 랭킹 문서(RANKING/{uid})를 덮어씁니다. <c>updatedAt</c>은 서버 시각으로 여기서 찍습니다.
        /// </summary>
        public async Task SaveRankingEntryAsync(IDictionary<string, object> fields)
        {
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();

            var values = new Dictionary<string, object>(fields)
            {
                [RankingEntry.UpdatedAtKey] = FieldValue.ServerTimestamp
            };
            await GetRankingDocument().SetAsync(values, SetOptions.MergeAll);
        }

        /// <summary>
        /// 전투력이 높은 순으로 상위 <paramref name="limit"/>명을 읽습니다.
        /// 각 맵에는 문서 ID를 <see cref="RankingUserIdKey"/> 키로 넣어 돌려줍니다.
        /// </summary>
        public async Task<List<IDictionary<string, object>>> LoadTopRankingAsync(int limit)
        {
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();

            var snapshot = await firestore.Collection(RankingCollection)
                .OrderByDescending(RankingPowerField)
                .Limit(Mathf.Clamp(limit, 1, RankingPageLimit))
                .GetSnapshotAsync();

            var result = new List<IDictionary<string, object>>(snapshot.Count);
            foreach (var document in snapshot.Documents)
            {
                var map = document.ToDictionary();
                map[RankingUserIdKey] = document.Id;
                result.Add(map);
            }

            return result;
        }

        /// <summary>
        /// 나보다 전투력이 높은 사람 수입니다. 여기에 1을 더하면 내 순위입니다.
        /// 문서를 받아 오지 않고 개수만 세므로, 인원이 몇만 명이 되어도 읽기 비용이 거의 들지 않습니다.
        /// </summary>
        public async Task<long> CountRankingAboveAsync(long power)
        {
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();

            var snapshot = await firestore.Collection(RankingCollection)
                .WhereGreaterThan(RankingPowerField, power)
                .Count
                .GetSnapshotAsync(AggregateSource.Server);

            return snapshot.Count;
        }

        /// <summary>
        /// 지금 서버 시각과, 마지막으로 접속해 있던 서버 시각을 함께 돌려줍니다.
        ///
        /// 단말 시계는 사용자가 얼마든지 바꿀 수 있으므로 한 번도 읽지 않습니다.
        /// 대신 <see cref="FieldValue.ServerTimestamp"/>를 찍고 그 문서를 서버에서 다시 읽어
        /// 서버가 매긴 시각을 그대로 가져옵니다. 캐시가 끼어들면 추정값이 오므로 <see cref="Source.Server"/>로 읽습니다.
        /// </summary>
        /// <returns>(지금 서버 시각, 지난 접속 시각. 첫 접속이면 null)</returns>
        public async Task<(DateTime ServerNow, DateTime? LastSeen)> ProbeServerClockAsync()
        {
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();

            var reference = GetPlayerDocument();
            await reference.SetAsync(new Dictionary<string, object>
            {
                [ClockProbeField] = FieldValue.ServerTimestamp
            }, SetOptions.MergeAll);

            var snapshot = await reference.GetSnapshotAsync(Source.Server);
            if (!snapshot.TryGetValue(ClockProbeField, out Timestamp probe))
                throw new InvalidOperationException("서버 시각을 읽지 못했습니다.");

            DateTime? lastSeen = snapshot.TryGetValue(LastSeenField, out Timestamp seen)
                ? seen.ToDateTime()
                : (DateTime?)null;

            return (probe.ToDateTime(), lastSeen);
        }

        /// <summary>
        /// "지금까지 붙어 있었다"를 서버 시각으로 찍습니다.
        /// 이 값이 다음 접속 때 오프라인 시간의 시작점이 되므로, 보상을 지급한 뒤에만 갱신해야 합니다.
        /// </summary>
        public async Task TouchLastSeenAsync()
        {
            await NetworkMonitor.RequireOnlineAsync();
            await InitializeAsync();
            await GetPlayerDocument().SetAsync(new Dictionary<string, object>
            {
                [LastSeenField] = FieldValue.ServerTimestamp
            }, SetOptions.MergeAll);
        }

        private static bool HasClaimedId(object entry, ICollection<string> claimedIds)
        {
            if (!(entry is IDictionary<string, object> map)) return false;
            return map.TryGetValue("id", out var id) && id is string text && claimedIds.Contains(text);
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

        private DocumentReference GetRankingDocument()
        {
            if (!IsReady || auth?.CurrentUser == null)
                throw new InvalidOperationException("Firebase has not been initialized.");
            return firestore.Collection(RankingCollection).Document(auth.CurrentUser.UserId);
        }
    }
}
