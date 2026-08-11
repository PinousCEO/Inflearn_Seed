using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleBattle
{
    /// <summary>
    /// 우편함(POST)의 단일 진입점입니다. 유저 문서(USERS/{uid})의 <c>POST</c> 필드 하나만 읽고 씁니다.
    ///
    /// - 게임을 켜면 <see cref="EnsureLoadedAsync"/>가 한 번 돌아 모든 우편을 받아 둡니다.
    /// - 받은 우편은 POST에서 지우고 보상을 <see cref="PlayerDataManager"/>에 넣습니다.
    /// - 서버 저장이 먼저, 보상 지급이 나중입니다. 저장이 실패하면 목록을 되돌려 두 번 받는 일을 막습니다.
    ///
    /// 필드 형식은 <see cref="MailData"/> 주석을 보세요. 운영자가 콘솔에서 직접 넣어도 됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-950)]
    public sealed class MailboxService : MonoBehaviour
    {
        /// <summary>우편함 정원입니다. 헤더의 "n / 30" 표시에 씁니다.</summary>
        public const int Capacity = 30;

        private const string MainSceneName = "Main";

        private static MailboxService instance;

        private readonly List<MailData> mails = new();
        private ItemCatalog catalog;
        private Task loadTask;
        private bool isBusy;

        public static MailboxService Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<MailboxService>();
                if (instance == null)
                    instance = new GameObject(nameof(MailboxService)).AddComponent<MailboxService>();
                return instance;
            }
        }

        /// <summary>서버에서 받아 온 우편 전부입니다. 최근에 온 것이 앞에 옵니다.</summary>
        public IReadOnlyList<MailData> Mails => mails;

        public bool IsLoaded { get; private set; }

        /// <summary>수령/수령 취소가 진행 중인 동안에는 버튼을 잠급니다.</summary>
        public bool IsBusy => isBusy;

        /// <summary>아직 받지 않았고 기간도 남은 우편 수입니다.</summary>
        public int ClaimableCount
        {
            get
            {
                var now = DateTime.UtcNow;
                return mails.Count(mail => mail.IsClaimable(now));
            }
        }

        /// <summary>아이템 보상을 이름·아이콘으로 풀어 주는 카탈로그입니다. 처음 필요할 때 한 번만 읽습니다.</summary>
        public ItemCatalog Catalog
        {
            get
            {
                if (catalog == null) catalog = AddressableContent.Load<ItemCatalog>("Data/ItemCatalog");
                return catalog;
            }
        }

        /// <summary>목록이 바뀔 때마다(로드·수령) 부릅니다.</summary>
        public event Action Changed;

        /// <summary>
        /// 로그인이 끝난 뒤 처음 들어오는 Main 씬에서 POST를 한 번 받아 둡니다.
        /// 우편함 UI를 열지 않아도 우편 정보가 준비되어 있게 합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadOnMainScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => LoadOnMainScene();

        private static void LoadOnMainScene()
        {
            if (SceneManager.GetActiveScene().name != MainSceneName) return;
            Instance.LoadInBackground();
        }

        /// <summary>실패해도 게임을 막지 않도록, 예외를 삼키고 로그만 남기는 백그라운드 로드입니다.</summary>
        public async void LoadInBackground()
        {
            try
            {
                await EnsureLoadedAsync();
            }
            catch (NetworkUnavailableException)
            {
                // 끊김 알림은 NetworkMonitor가 이미 띄웁니다.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Mailbox] POST를 불러오지 못했습니다: {exception.Message}", this);
            }
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

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------
        // 로드
        // ------------------------------------------------------------------

        /// <summary>아직 안 받아 왔으면 받아 옵니다. 이미 받아 왔으면 통신하지 않습니다.</summary>
        public Task EnsureLoadedAsync()
        {
            // 실패한 로드를 캐싱해 두면 네트워크가 돌아와도 영영 다시 시도하지 못합니다.
            if (loadTask != null && (loadTask.IsFaulted || loadTask.IsCanceled)) loadTask = null;
            return loadTask ??= LoadInternalAsync();
        }

        /// <summary>서버에서 다시 받아 옵니다. 우편함을 열 때처럼 최신 상태가 필요할 때 씁니다.</summary>
        public Task ReloadAsync()
        {
            // 수령 중에 다시 읽으면 방금 지운 우편이 되살아납니다. 그 사이에는 건너뜁니다.
            if (isBusy) return loadTask ?? Task.CompletedTask;
            return loadTask = LoadInternalAsync();
        }

        private async Task LoadInternalAsync()
        {
            var raw = await FirebaseInitializer.Instance.LoadPostAsync();
            if (this == null) return;

            mails.Clear();
            if (raw == null)
            {
                // POST 필드가 아직 없는 계정입니다. 첫 우편을 넣어 두고 그대로 씁니다.
                mails.AddRange(CreateWelcomeMails());
                await SaveAsync();
                if (this == null) return;
            }
            else if (ApplyRaw(raw))
            {
                // id 없이 들어온 우편에 id를 붙였습니다. 서버에도 반영해야 수령할 때 그 우편을 지목할 수 있습니다.
                await SaveAsync();
                if (this == null) return;
            }

            IsLoaded = true;
            Debug.Log($"[Mailbox] POST에서 우편 {mails.Count}통을 받았습니다. (받을 수 있는 우편 {ClaimableCount}통)", this);
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------
        // 수령
        // ------------------------------------------------------------------

        /// <summary>우편 한 통을 받습니다. 받았으면 true입니다.</summary>
        public async Task<bool> ClaimAsync(MailData mail)
        {
            if (mail == null) return false;
            return await ClaimManyAsync(new[] { mail }) > 0;
        }

        /// <summary>받을 수 있는 우편을 모두 받습니다. 받은 통 수를 돌려줍니다.</summary>
        public Task<int> ClaimAllAsync()
        {
            var now = DateTime.UtcNow;
            return ClaimManyAsync(mails.Where(mail => mail.IsClaimable(now)).ToList());
        }

        private async Task<int> ClaimManyAsync(IReadOnlyList<MailData> targets)
        {
            if (isBusy || targets == null || targets.Count == 0) return 0;

            var now = DateTime.UtcNow;
            var claiming = targets.Where(mail => mail != null && mails.Contains(mail) && mail.IsClaimable(now)).ToList();
            if (claiming.Count == 0) return 0;

            isBusy = true;
            var backup = new List<MailData>(mails);
            try
            {
                // 1단계. 보상보다 서버 반영이 먼저입니다. 저장 도중 앱이 죽어도 같은 우편을 두 번 받지 않습니다.
                foreach (var mail in claiming)
                {
                    mail.Claimed = true;
                    mails.Remove(mail);
                }

                try
                {
                    // 배열을 통째로 덮어쓰지 않습니다. 관리자 페이지가 그 사이에 넣은 우편을 지우지 않기 위해서입니다.
                    var remaining = await FirebaseInitializer.Instance.ClaimPostEntriesAsync(
                        claiming.Select(mail => mail.Id).ToList());
                    if (this == null) return 0;

                    // 서버가 돌려준 최신 배열로 목록을 맞춥니다. 방금 도착한 우편도 여기서 함께 들어옵니다.
                    mails.Clear();
                    ApplyRaw(remaining);
                }
                catch (Exception exception)
                {
                    // 서버에 반영되지 않았으니 지웠던 우편을 되돌려 다시 받을 수 있게 합니다.
                    mails.Clear();
                    mails.AddRange(backup);
                    foreach (var mail in claiming) mail.Claimed = false;
                    Debug.LogError($"[Mailbox] 우편 수령을 저장하지 못했습니다: {exception.Message}", this);
                    Changed?.Invoke();
                    return 0;
                }

                // 2단계. 서버에서는 이미 지워졌으므로 여기서 실패해도 목록을 되돌리지 않습니다.
                // 되돌리면 서버에 없는 우편이 남아 다음 로드에서 사라지고, 그동안 두 번 받을 수 있게 됩니다.
                try
                {
                    await PlayerDataManager.Instance.EnsureLoadedAsync();
                    if (this == null) return 0;
                    // "모두 받기"로 여러 통을 받아도 저장은 한 번만 합니다.
                    foreach (var mail in claiming) GrantReward(mail);
                    PlayerDataManager.Instance.Save();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Mailbox] 보상을 지급하지 못했습니다: {exception.Message}", this);
                }

                Changed?.Invoke();
                return claiming.Count;
            }
            finally
            {
                isBusy = false;
            }
        }

        private void GrantReward(MailData mail)
        {
            var player = PlayerDataManager.Instance;
            switch (mail.RewardType)
            {
                case MailRewardType.Gold:
                    player.AddCoins(mail.RewardAmount, save: false);
                    break;
                case MailRewardType.Experience:
                    player.AddExperience((int)Math.Min(mail.RewardAmount, int.MaxValue));
                    break;
                case MailRewardType.Item:
                    if (Catalog != null && Catalog.TryGet(mail.RewardItemId, out var item))
                        player.AddItem(item, (int)Math.Min(Math.Max(mail.RewardAmount, 1L), int.MaxValue), save: false);
                    else
                        Debug.LogWarning($"[Mailbox] 보상 아이템을 찾지 못했습니다: {mail.RewardItemId}", this);
                    break;
            }
        }

        /// <summary>우편 목록의 보상 문구입니다. 아이콘 옆에 짧게 붙습니다.</summary>
        public string DescribeReward(MailData mail)
        {
            if (mail == null) return string.Empty;
            switch (mail.RewardType)
            {
                case MailRewardType.Gold:
                    return mail.RewardAmount.ToString("N0");
                case MailRewardType.Experience:
                    return $"EXP {mail.RewardAmount:N0}";
                case MailRewardType.Item:
                    var name = Catalog != null && Catalog.TryGet(mail.RewardItemId, out var item)
                        ? item.DisplayName
                        : mail.RewardItemId;
                    return mail.RewardAmount > 1L ? $"{name} x{mail.RewardAmount}" : name;
                default:
                    return "보상 없음";
            }
        }

        /// <summary>보상 아이콘입니다. 아이템이 아니면 null이라 우편함이 기본 아이콘을 그대로 씁니다.</summary>
        public Sprite ResolveRewardIcon(MailData mail)
        {
            if (mail == null || mail.RewardType != MailRewardType.Item) return null;
            return Catalog != null && Catalog.TryGet(mail.RewardItemId, out var item) ? item.Icon : null;
        }

        // ------------------------------------------------------------------
        // 저장
        // ------------------------------------------------------------------

        private Task SaveAsync()
        {
            var entries = mails.Select(mail => (object)mail.ToDictionary()).ToList();
            return FirebaseInitializer.Instance.SavePostAsync(entries);
        }

        /// <summary>
        /// POST 배열을 목록으로 옮기고 최신순으로 세웁니다.
        /// id가 없거나 겹치면 채워 넣고 true를 돌려줍니다(그때만 서버에 다시 써야 합니다).
        /// 수령은 id로 지목하기 때문에, 서버 쪽 id가 비어 있으면 그 우편을 영영 지우지 못합니다.
        /// </summary>
        private bool ApplyRaw(IEnumerable<object> raw)
        {
            foreach (var entry in raw)
                if (MailData.TryParse(entry, out var mail))
                    mails.Add(mail);

            mails.Sort((left, right) => right.SentAtUtc.CompareTo(left.SentAtUtc));

            var changed = false;
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < mails.Count; i++)
            {
                var id = mails[i].Id;
                if (!string.IsNullOrWhiteSpace(id) && used.Add(id)) continue;

                id = $"post-{i:00}-{mails[i].SentAtUtc.Ticks}";
                used.Add(id);
                mails[i].Id = id;
                changed = true;
            }

            return changed;
        }

        /// <summary>POST 필드가 없는 새 계정에 넣어 주는 첫 우편입니다.</summary>
        private static IEnumerable<MailData> CreateWelcomeMails()
        {
            var now = DateTime.UtcNow;
            yield return new MailData
            {
                Id = "welcome-gold",
                Title = "운영자 우편",
                Body = "모험의 시작을 응원합니다.",
                RewardType = MailRewardType.Gold,
                RewardAmount = 50000L,
                SentAtUtc = now,
                ExpiresAtUtc = now.AddDays(7d)
            };
            yield return new MailData
            {
                Id = "welcome-exp",
                Title = "첫 접속 보상",
                Body = "우편함에서 보상을 받아 가세요.",
                RewardType = MailRewardType.Experience,
                RewardAmount = 300L,
                SentAtUtc = now.AddSeconds(-1d),
                ExpiresAtUtc = now.AddDays(7d)
            };
        }
    }
}
