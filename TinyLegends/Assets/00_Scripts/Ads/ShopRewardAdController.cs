using IdleBattle.Audio;
using IdleBattle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle.Ads
{
    /// <summary>상점의 DailyFree 자리를 리워드 광고 보상에 연결합니다.</summary>
    public sealed class ShopRewardAdController : MonoBehaviour
    {
        private const long RewardCoins = 50000L;
        private Button button;
        private bool showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        private static void Attach()
        {
            if (SceneManager.GetActiveScene().name != "Main" || FindFirstObjectByType<ShopRewardAdController>() != null) return;
            new GameObject(nameof(ShopRewardAdController)).AddComponent<ShopRewardAdController>();
        }

        private void Start()
        {
            var canvas = SceneRefs.RootCanvas;
            var slot = canvas != null ? canvas.transform.Find("Shop/BackGround/SafeArea/Main/Scroll View/Viewport/Content/DailyFree") : null;
            var target = slot != null ? slot.Find("~ClaimBtn") : null;
            if (target == null) { Debug.LogWarning("[AdMob] Shop DailyFree/~ClaimBtn을 찾지 못했습니다.", this); return; }
            button = target.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>();
            if (button.targetGraphic == null && target.TryGetComponent(out Graphic graphic)) button.targetGraphic = graphic;
            button.onClick.AddListener(ShowRewarded);
            var label = target.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "광고 보고 받기";
            var title = slot.Find("~Title")?.GetComponent<TMP_Text>();
            if (title != null) title.text = "광고 보상 골드";
            var desc = slot.Find("~Desc")?.GetComponent<TMP_Text>();
            if (desc != null) desc.text = "광고를 보고 골드를 받으세요";
            var count = slot.Find("~Item/~Count")?.GetComponent<TMP_Text>();
            if (count != null) count.text = RewardCoins.ToString("N0");
            Debug.Log("[AdMob] Shop reward button bound: " + GetPath(target), this);
        }

        private void ShowRewarded()
        {
            if (showing) return;
            showing = true;
            if (button != null) button.interactable = false;
            AdMobService.Instance.ShowRewarded(Complete);
        }

        private void Complete(bool earned)
        {
            showing = false;
            if (button != null) button.interactable = true;
            if (!earned) { AudioManager.Play(SfxId.UiDenied); PopupService.Toast("리워드 광고가 아직 준비되지 않았습니다."); return; }
            PlayerDataManager.Instance.AddCoins(RewardCoins);
            AudioManager.Play(SfxId.RewardClaim);
            PopupService.Toast($"광고 보상 골드 {RewardCoins:N0}을 받았습니다.");
        }

        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(ShowRewarded); }

        private static string GetPath(Transform target)
        {
            var path = target.name;
            while (target.parent != null) { target = target.parent; path = target.name + "/" + path; }
            return path;
        }
    }
}
