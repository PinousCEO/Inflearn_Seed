using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IdleBattle
{
    /// <summary>
    /// 자리를 비운 동안의 보상을 계산하고 지급합니다.
    ///
    /// 시간은 전부 서버가 매깁니다. 단말 시계는 한 번도 읽지 않기 때문에
    /// 기기 시간을 앞으로 돌려도 보상이 늘지 않습니다.
    ///   · 접속할 때  : <see cref="FirebaseInitializer.ProbeServerClockAsync"/>로 (지금, 지난 접속)을 받아 차이를 냅니다.
    ///   · 노는 동안  : <see cref="HeartbeatSeconds"/>마다 lastSeenAt을 서버 시각으로 갱신합니다.
    ///   · 나갈 때    : 백그라운드로 내려가거나 종료할 때 한 번 더 찍습니다.
    ///
    /// lastSeenAt은 보상을 <b>지급한 뒤에만</b> 앞으로 갑니다.
    /// 보상 판넬을 띄운 채로 앱이 죽어도 다음 접속에서 같은 보상을 다시 받습니다.
    ///
    /// 계산은 "1분에 몬스터 <see cref="MonstersPerMinute"/>마리를 잡았다"로 봅니다.
    /// 골드는 스테이지 규칙의 처치 보상을, 아이템은 각 아이템의 드랍률을 그대로 씁니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-940)]
    public sealed class OfflineRewardService : MonoBehaviour
    {
        /// <summary>1분에 잡은 것으로 치는 몬스터 수입니다.</summary>
        public const float MonstersPerMinute = 3f;

        /// <summary>이만큼은 비워야 보상 판넬이 뜹니다.</summary>
        public static readonly TimeSpan Minimum = TimeSpan.FromMinutes(1d);

        /// <summary>보상이 쌓이는 한도입니다. 판넬의 "최대 8시간" 문구와 맞춰 둡니다.</summary>
        public static readonly TimeSpan MaxAccrual = TimeSpan.FromHours(8d);

        /// <summary>
        /// 접속 중에 서버 시각을 다시 찍는 간격입니다. 앱이 강제 종료되면 마지막으로 찍힌 값이 종료 시각이 됩니다.
        /// 짧을수록 정확하지만 그만큼 쓰기 횟수(=비용)가 늘어납니다. 1분 단위로 보상하므로 60초로 맞춰 뒀습니다.
        /// </summary>
        private const float HeartbeatSeconds = 60f;

        /// <summary>보상 계산에 쓸 스테이지입니다. 진행 스테이지를 저장하지 않아 재접속하면 늘 1부터 시작합니다.</summary>
        private const int RewardStage = 1;

        private static OfflineRewardService instance;

        private ItemCatalog catalog;
        private StageData stageData;
        private Coroutine heartbeat;
        private bool settled;

        public static OfflineRewardService Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<OfflineRewardService>();
                if (instance == null)
                    instance = new GameObject(nameof(OfflineRewardService)).AddComponent<OfflineRewardService>();
                return instance;
            }
        }

        /// <summary>보상 칸의 등급 테두리를 고르는 데 씁니다. 처음 필요할 때 한 번만 읽습니다.</summary>
        public ItemCatalog CatalogForDisplay
        {
            get
            {
                if (catalog == null) catalog = AddressableContent.Load<ItemCatalog>("Data/ItemCatalog");
                return catalog;
            }
        }

        /// <summary>보상 한 줄입니다. 골드는 <see cref="Reward.Gold"/>로 따로 들고 있습니다.</summary>
        public readonly struct RewardItem
        {
            public readonly ItemData Item;
            public readonly int Amount;
            public RewardItem(ItemData item, int amount) { Item = item; Amount = amount; }
        }

        public sealed class Reward
        {
            /// <summary>한도를 적용한 뒤의 오프라인 시간입니다. 판넬에 그대로 보여 줍니다.</summary>
            public TimeSpan Offline;
            /// <summary>한도를 적용하기 전의 실제 오프라인 시간입니다.</summary>
            public TimeSpan RawOffline;
            public int Monsters;
            public long Gold;
            public IReadOnlyList<RewardItem> Items = Array.Empty<RewardItem>();

            public bool IsCapped => RawOffline > Offline;
            public bool IsEmpty => Gold <= 0L && Items.Count == 0;
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

        /// <summary>백그라운드로 내려갈 때 종료 시각을 한 번 더 찍어 둡니다.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused) TouchInBackground();
        }

        private void OnApplicationQuit() => TouchInBackground();

        // ------------------------------------------------------------------
        // 계산
        // ------------------------------------------------------------------

        /// <summary>
        /// 서버 시각으로 오프라인 보상을 계산합니다.
        /// 1분이 안 됐거나 첫 접속이면 null을 돌려주고, 그 자리에서 접속 시각만 갱신합니다.
        /// </summary>
        public async Task<Reward> EvaluateAsync()
        {
            var clock = await FirebaseInitializer.Instance.ProbeServerClockAsync();
            if (this == null) return null;

            if (clock.LastSeen == null)
            {
                // 첫 접속입니다. 기준점만 찍어 둡니다.
                await SettleAsync();
                return null;
            }

            var offline = clock.ServerNow - clock.LastSeen.Value;
            if (offline < Minimum)
            {
                await SettleAsync();
                return null;
            }

            var reward = Build(offline);
            if (reward.IsEmpty)
            {
                await SettleAsync();
                return null;
            }

            Debug.Log($"[Offline] 오프라인 {Format(reward.Offline)} · 몬스터 {reward.Monsters}마리 " +
                      $"· 골드 {reward.Gold:N0} · 아이템 {reward.Items.Count}종", this);
            return reward;
        }

        private Reward Build(TimeSpan rawOffline)
        {
            var capped = rawOffline > MaxAccrual ? MaxAccrual : rawOffline;
            var minutes = (int)capped.TotalMinutes;
            var monsters = Mathf.FloorToInt(minutes * MonstersPerMinute);

            return new Reward
            {
                RawOffline = rawOffline,
                Offline = capped,
                Monsters = monsters,
                Gold = (long)monsters * CoinPerMonster(),
                Items = RollItems(monsters)
            };
        }

        private int CoinPerMonster()
        {
            if (stageData == null) stageData = AddressableContent.Load<StageData>("Data/StageData");
            if (stageData != null && stageData.TryGetRule(RewardStage, out var rule)) return rule.CoinPerMonster;

            Debug.LogWarning($"[Offline] Stage {RewardStage} 규칙을 찾지 못해 골드를 0으로 둡니다.", this);
            return 0;
        }

        /// <summary>
        /// 몬스터를 실제로 한 마리씩 잡은 것처럼 드랍 테이블을 굴립니다.
        /// 굴리는 규칙은 전투의 <see cref="ItemDropSystem.RollDrops"/>와 똑같습니다.
        /// 한 마리마다 아이템 전체를 훑으며 <see cref="ItemData.DropRatePercent"/>로 각각 판정합니다.
        /// (8시간 한도라도 1440마리 × 아이템 수 정도라 한 번 계산하는 비용은 무시할 수준입니다.)
        /// </summary>
        private IReadOnlyList<RewardItem> RollItems(int monsters)
        {
            if (monsters <= 0) return Array.Empty<RewardItem>();
            if (catalog == null) catalog = AddressableContent.Load<ItemCatalog>("Data/ItemCatalog");
            if (catalog == null)
            {
                Debug.LogWarning("[Offline] ItemCatalog를 찾지 못해 아이템 보상을 건너뜁니다.", this);
                return Array.Empty<RewardItem>();
            }

            // 드랍률이 걸린 아이템만 미리 추려 둡니다. 마리마다 전체 목록을 훑지 않게 합니다.
            var table = new List<ItemData>();
            foreach (var item in catalog.Items)
                if (item != null && item.DropRatePercent > 0f) table.Add(item);
            if (table.Count == 0) return Array.Empty<RewardItem>();

            var counts = new int[table.Count];
            for (var kill = 0; kill < monsters; kill++)
                for (var i = 0; i < table.Count; i++)
                    if (Random.value * 100f < table[i].DropRatePercent) counts[i]++;

            var rolled = new List<RewardItem>();
            for (var i = 0; i < table.Count; i++)
                if (counts[i] > 0) rolled.Add(new RewardItem(table[i], counts[i]));

            rolled.Sort((left, right) =>
            {
                var byRarity = right.Item.Rarity.CompareTo(left.Item.Rarity);
                return byRarity != 0 ? byRarity : string.CompareOrdinal(left.Item.ItemId, right.Item.ItemId);
            });
            return rolled;
        }

        // ------------------------------------------------------------------
        // 지급
        // ------------------------------------------------------------------

        /// <summary>보상을 실제로 넣고, 접속 시각을 지금으로 옮깁니다.</summary>
        public async Task ClaimAsync(Reward reward)
        {
            if (reward == null) { await SettleAsync(); return; }

            await PlayerDataManager.Instance.EnsureLoadedAsync();
            if (this == null) return;

            // 종류마다 저장하면 아이템 수만큼 Firestore 쓰기가 일어납니다. 다 넣고 한 번만 저장합니다.
            var player = PlayerDataManager.Instance;
            if (reward.Gold > 0L) player.AddCoins(reward.Gold, save: false);
            foreach (var entry in reward.Items)
                if (entry.Item != null && entry.Amount > 0) player.AddItem(entry.Item, entry.Amount, save: false);
            player.Save();

            Debug.Log($"[Offline] 보상 지급 — 골드 {reward.Gold:N0} · 아이템 {reward.Items.Count}종", this);
            await SettleAsync();
        }

        /// <summary>접속 시각을 지금(서버 시각)으로 옮기고 하트비트를 시작합니다.</summary>
        public async Task SettleAsync()
        {
            try
            {
                await FirebaseInitializer.Instance.TouchLastSeenAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Offline] 접속 시각을 기록하지 못했습니다: {exception.Message}", this);
            }

            if (this == null) return;
            settled = true;
            StartHeartbeat();
        }

        // ------------------------------------------------------------------
        // 하트비트
        // ------------------------------------------------------------------

        private void StartHeartbeat()
        {
            if (heartbeat != null) return;
            heartbeat = StartCoroutine(HeartbeatRoutine());
        }

        private IEnumerator HeartbeatRoutine()
        {
            var wait = new WaitForSecondsRealtime(HeartbeatSeconds);
            while (true)
            {
                yield return wait;
                TouchInBackground();
            }
        }

        /// <summary>실패해도 게임을 막지 않는 기록입니다. 다음 하트비트가 다시 시도합니다.</summary>
        private async void TouchInBackground()
        {
            // 보상을 아직 안 받았는데 시각을 밀어 버리면 그 보상이 사라집니다.
            if (!settled) return;

            try
            {
                await FirebaseInitializer.Instance.TouchLastSeenAsync();
            }
            catch (NetworkUnavailableException)
            {
                // 끊김 알림은 NetworkMonitor가 이미 띄웁니다.
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Offline] 접속 시각 갱신 실패: {exception.Message}", this);
            }
        }

        /// <summary>"12시간 5분" 같은 사람이 읽는 문구입니다.</summary>
        public static string Describe(TimeSpan span)
        {
            if (span.TotalHours >= 1d) return $"{(int)span.TotalHours}시간 {span.Minutes}분";
            if (span.TotalMinutes >= 1d) return $"{span.Minutes}분";
            return "1분 미만";
        }

        /// <summary>판넬의 "오프라인 시간  00:00:00" 자리에 넣는 문구입니다.</summary>
        public static string Format(TimeSpan span)
        {
            var hours = (int)span.TotalHours;
            return $"{hours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }
    }
}
