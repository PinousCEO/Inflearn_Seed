using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleBattle
{
    /// <summary>
    /// 랭킹(RANKING 컬렉션)의 단일 진입점입니다.
    ///
    /// 쓰기 — 내 전투력을 <see cref="CombatPower"/>로 계산해 <c>RANKING/{uid}</c>에 올립니다.
    /// 레벨업·장비 착용처럼 값이 바뀔 만한 일이 생기면 자동으로 올라가고,
    /// 실제 점수가 그대로면 통신하지 않습니다. 연달아 바뀔 때는 한 번으로 묶습니다.
    ///
    /// 읽기 — 상위 100명과 내 순위를 받아 옵니다. 순위는 문서를 읽지 않고 개수만 세어 구합니다.
    ///
    /// 캐릭터를 아직 만들지 않았거나(이름이 비었거나) 저장 데이터를 못 불러왔으면 올리지 않습니다.
    /// 빈 이름으로 목록을 채우지 않기 위해서입니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-950)]
    public sealed class RankingService : MonoBehaviour
    {
        private const string MainSceneName = "Main";

        /// <summary>
        /// 값이 바뀐 뒤 실제로 올리기까지 기다리는 시간입니다.
        /// 장비를 연달아 갈아 끼우면 InventoryChanged가 그만큼 오므로, 마지막 것 하나만 올립니다.
        /// </summary>
        private const float SubmitDelay = 1.5f;

        private static RankingService instance;

        private readonly List<RankingEntry> top = new();
        private PlayerDataManager data;
        private ItemCatalog catalog;
        private Task submitTask;
        private Task refreshTask;
        private float submitAt = -1f;
        private long submittedPower = -1L;
        private int submittedLevel = -1;
        private string submittedName;

        public static RankingService Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<RankingService>();
                if (instance == null)
                    instance = new GameObject(nameof(RankingService)).AddComponent<RankingService>();
                return instance;
            }
        }

        /// <summary>지금 내 전투력입니다. 저장 데이터를 아직 못 불러왔으면 0입니다.</summary>
        public long MyPower { get; private set; }

        /// <summary>전투력의 항목별 내역입니다. 툴팁이나 디버그 표시에 씁니다.</summary>
        public CombatPowerResult MyPowerDetail { get; private set; }

        /// <summary>1부터 시작하는 내 순위입니다. 아직 받아 오지 않았으면 0입니다.</summary>
        public int MyRank { get; private set; }

        /// <summary>전투력이 높은 순으로 정렬된 상위 목록입니다. <see cref="RefreshAsync"/> 뒤에 채워집니다.</summary>
        public IReadOnlyList<RankingEntry> Top => top;

        /// <summary>내 전투력이 바뀔 때마다 부릅니다. 인자는 새 전투력입니다.</summary>
        public event Action<long> PowerChanged;

        /// <summary>상위 목록이나 내 순위가 새로 들어올 때마다 부릅니다.</summary>
        public event Action Changed;

        /// <summary>
        /// 로그인이 끝난 뒤 처음 들어오는 Main 씬에서 전투력을 한 번 올려 둡니다.
        /// 랭킹 판넬을 열지 않아도 내 점수가 서버에 남습니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SubmitOnMainScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SubmitOnMainScene();

        private static void SubmitOnMainScene()
        {
            if (SceneManager.GetActiveScene().name != MainSceneName) return;
            Instance.SubmitInBackground();
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

            data = PlayerDataManager.Instance;
            data.InventoryChanged += OnPlayerDataChanged;
            data.ExperienceChanged += OnExperienceChanged;
        }

        private void OnDestroy()
        {
            if (data != null)
            {
                data.InventoryChanged -= OnPlayerDataChanged;
                data.ExperienceChanged -= OnExperienceChanged;
            }

            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (submitAt < 0f || Time.unscaledTime < submitAt) return;

            // 앞선 전송이 아직 안 끝났으면 예약을 뒤로 미룹니다.
            // 전송 중에 바뀐 값이 그 전송에 묻히지 않고 반드시 한 번 더 올라갑니다.
            if (submitTask != null && !submitTask.IsCompleted)
            {
                submitAt = Time.unscaledTime + SubmitDelay;
                return;
            }

            submitAt = -1f;
            SubmitInBackground();
        }

        // ------------------------------------------------------------------
        // 전투력 계산
        // ------------------------------------------------------------------

        /// <summary>아이템 카탈로그입니다. 처음 필요할 때 한 번만 읽습니다.</summary>
        private ItemCatalog Catalog
        {
            get
            {
                if (catalog == null) catalog = Resources.Load<ItemCatalog>("Data/ItemCatalog");
                return catalog;
            }
        }

        /// <summary>전투력을 다시 계산해 <see cref="MyPower"/>에 반영합니다. 통신은 하지 않습니다.</summary>
        public long Recalculate()
        {
            if (data == null || !data.IsLoaded) return MyPower;

            var detail = CombatPower.Evaluate(data, Catalog);
            MyPowerDetail = detail;
            if (detail.Total == MyPower) return MyPower;

            MyPower = detail.Total;
            PowerChanged?.Invoke(MyPower);
            return MyPower;
        }

        // 화면이 곧바로 새 전투력을 읽을 수 있게, 서버에 올리기 전에 먼저 다시 계산해 둡니다.
        private void OnPlayerDataChanged()
        {
            Recalculate();
            SubmitSoon();
        }

        private void OnExperienceChanged(int level, int experience, int required)
        {
            Recalculate();
            SubmitSoon();
        }

        /// <summary>값이 바뀌었을 수 있으니 잠시 뒤에 한 번 올리라고 예약합니다.</summary>
        public void SubmitSoon()
        {
            submitAt = Time.unscaledTime + SubmitDelay;
        }

        // ------------------------------------------------------------------
        // 올리기
        // ------------------------------------------------------------------

        /// <summary>실패해도 게임을 막지 않도록, 예외를 삼키고 로그만 남기는 백그라운드 전송입니다.</summary>
        public async void SubmitInBackground()
        {
            try
            {
                await SubmitAsync();
            }
            catch (NetworkUnavailableException)
            {
                // 끊김 알림은 NetworkMonitor가 이미 띄웁니다. 다음 변화 때 다시 올라갑니다.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Ranking] 전투력을 올리지 못했습니다: {exception.Message}", this);
            }
        }

        /// <summary>
        /// 전투력을 다시 계산해 서버에 올립니다.
        /// 지난번에 올린 값과 전투력·레벨·이름이 모두 같으면 통신하지 않습니다.
        /// </summary>
        public Task SubmitAsync()
        {
            // 앞선 전송이 끝나기 전에 또 부르면 그 전송에 붙습니다. 같은 문서에 두 번 쓰지 않습니다.
            if (submitTask != null && !submitTask.IsCompleted) return submitTask;
            return submitTask = SubmitInternalAsync();
        }

        private async Task SubmitInternalAsync()
        {
            await data.EnsureLoadedAsync();
            if (this == null || !data.IsLoaded) return;

            // 캐릭터를 아직 고르지 않은 계정은 랭킹에 넣지 않습니다.
            if (string.IsNullOrWhiteSpace(data.PlayerName)) return;

            Recalculate();
            if (MyPower <= 0L) return;
            if (MyPower == submittedPower
                && data.Level == submittedLevel
                && string.Equals(data.PlayerName, submittedName, StringComparison.Ordinal))
                return;

            var entry = new RankingEntry
            {
                UserId = FirebaseInitializer.Instance.UserId ?? string.Empty,
                Name = data.PlayerName,
                CharacterId = data.CharacterId,
                Level = data.Level,
                Power = MyPower,
                FormulaVersion = CombatPower.Version
            };

            await FirebaseInitializer.Instance.SaveRankingEntryAsync(entry.ToDictionary());
            if (this == null) return;

            submittedPower = entry.Power;
            submittedLevel = entry.Level;
            submittedName = entry.Name;
        }

        // ------------------------------------------------------------------
        // 읽어 오기
        // ------------------------------------------------------------------

        /// <summary>상위 목록과 내 순위를 받아 옵니다. 랭킹 판넬을 열 때 부르세요.</summary>
        public Task RefreshAsync(int limit = FirebaseInitializer.RankingPageLimit)
        {
            if (refreshTask != null && !refreshTask.IsCompleted) return refreshTask;
            return refreshTask = RefreshInternalAsync(limit);
        }

        private async Task RefreshInternalAsync(int limit)
        {
            var documents = await FirebaseInitializer.Instance.LoadTopRankingAsync(limit);
            if (this == null) return;

            top.Clear();
            foreach (var document in documents)
            {
                if (!RankingEntry.TryParse(document, null, out var entry)) continue;
                if (!entry.IsListable) continue;
                entry.Rank = top.Count + 1;
                top.Add(entry);
            }

            MyRank = await ResolveMyRankAsync();
            if (this == null) return;

            Changed?.Invoke();
        }

        /// <summary>
        /// 내 순위입니다. 상위 목록 안에 내가 있으면 그 자리를 그대로 쓰고,
        /// 밖에 있을 때만 "나보다 높은 사람 수 + 1"을 세어 옵니다.
        /// </summary>
        private async Task<int> ResolveMyRankAsync()
        {
            var myUserId = FirebaseInitializer.Instance.UserId;
            if (!string.IsNullOrEmpty(myUserId))
                foreach (var entry in top)
                    if (string.Equals(entry.UserId, myUserId, StringComparison.Ordinal))
                        return entry.Rank;

            if (MyPower <= 0L) return 0;
            return (int)await FirebaseInitializer.Instance.CountRankingAboveAsync(MyPower) + 1;
        }
    }
}
