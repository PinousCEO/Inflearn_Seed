using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IdleBattle
{
    /// <summary>몬스터 위치에서 코인을 흩뿌리고 플레이어가 가까워지면 자동 획득시킵니다.</summary>
    public sealed class CoinDropSystem : MonoBehaviour
    {
        private readonly List<GameObject> activeCoins = new();
        private Transform player;
        private Material coinMaterial;

        public void Initialize(Transform playerTransform)
        {
            player = playerTransform;
            if (coinMaterial != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            coinMaterial = new Material(shader)
            {
                name = "Runtime Golden Coin",
                color = new Color(1f, .66f, .05f)
            };
            coinMaterial.EnableKeyword("_EMISSION");
            coinMaterial.SetColor("_EmissionColor", new Color(.65f, .24f, .01f));
        }

        public void Drop(Vector3 origin, int totalAmount)
        {
            if (totalAmount <= 0 || player == null) return;
            var count = Mathf.Clamp(Mathf.CeilToInt(totalAmount / 5f), 3, 8);
            var remaining = totalAmount;
            for (var i = 0; i < count; i++)
            {
                var amount = Mathf.CeilToInt(remaining / (float)(count - i));
                remaining -= amount;
                StartCoroutine(AnimateCoin(origin + Vector3.up * .7f, amount, i, count));
            }
        }

        private IEnumerator AnimateCoin(Vector3 origin, int amount, int index, int count)
        {
            var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = $"Coin +{amount}";
            coin.transform.localScale = new Vector3(.22f, .045f, .22f);
            coin.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            coin.GetComponent<Renderer>().sharedMaterial = coinMaterial;
            var collider = coin.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;
            activeCoins.Add(coin);

            var angle = index * Mathf.PI * 2f / count + Random.Range(-.3f, .3f);
            var distance = Random.Range(1.2f, 2.7f);
            var landing = origin + new Vector3(Mathf.Cos(angle) * distance, -.62f, Mathf.Sin(angle) * distance);
            var arcHeight = Random.Range(1.4f, 2.1f);
            const float flightDuration = .62f;
            for (var elapsed = 0f; elapsed < flightDuration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / flightDuration);
                var position = Vector3.Lerp(origin, landing, t);
                position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                coin.transform.position = position;
                coin.transform.Rotate(0f, 0f, 720f * Time.deltaTime, Space.Self);
                yield return null;
            }
            coin.transform.position = landing;

            // 바닥에 닿은 모양이 보인 직후, 거리와 관계없이 플레이어에게 이동합니다.
            yield return new WaitForSeconds(.12f);
            var homingTime = 0f;
            while (coin != null && player != null)
            {
                homingTime += Time.deltaTime;
                var distanceToPlayer = Vector3.Distance(coin.transform.position, player.position);
                var speed = Mathf.Lerp(7f, 24f, Mathf.Clamp01(homingTime / .55f));
                coin.transform.position = Vector3.MoveTowards(
                    coin.transform.position,
                    player.position + Vector3.up * .8f,
                    speed * Time.deltaTime);
                coin.transform.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);
                // 캐릭터 중심에 파고들기 전에 근접 획득 처리합니다.
                if (distanceToPlayer < 1.35f) break;
                yield return null;
            }

            if (coin != null && player != null)
            {
                PlayerDataManager.Instance.AddCoins(amount);
                var start = coin.transform.position;
                var startScale = coin.transform.localScale;
                const float collectDuration = .10f;
                for (var elapsed = 0f; elapsed < collectDuration; elapsed += Time.deltaTime)
                {
                    var t = Mathf.Clamp01(elapsed / collectDuration);
                    coin.transform.position = start + Vector3.up * (t * .35f);
                    coin.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                    yield return null;
                }
            }
            activeCoins.Remove(coin);
            if (coin != null) Destroy(coin);
        }

        private void OnDestroy()
        {
            foreach (var coin in activeCoins)
                if (coin != null) Destroy(coin);
            if (coinMaterial != null) Destroy(coinMaterial);
        }
    }
}
