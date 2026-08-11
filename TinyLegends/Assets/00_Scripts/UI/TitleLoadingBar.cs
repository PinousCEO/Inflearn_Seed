using System.Collections;
using System.Text;
using IdleBattle.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IdleBattle.UI
{
    /// <summary>
    /// 타이틀 화면의 로딩바. TapToStart 이후 Main 씬을 비동기로 로드하면서
    /// Fill(fillAmount) / Loading(문구 애니메이션) / Percentage(소수점 2자리)를 갱신한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleLoadingBar : MonoBehaviour
    {
        /// <summary>실제 로딩이 순식간에 끝나도 게이지가 튀지 않도록 초당 최대 증가량을 제한한다.</summary>
        private const float MaxFillPerSecond = 0.6f;
        private const float CompleteHoldSeconds = 0.25f;

        // "로딩중" 뒤에 붙는 점 애니메이션
        private const int MaxDots = 3;
        private const float DotInterval = 0.28f;

        // 글자 웨이브 애니메이션
        private const float WaveSpeed = 7f;
        private const float WaveAmplitude = 4.5f;
        private const float WavePerCharOffset = 0.5f;

        private GameObject root;
        private Image fill;
        private TMP_Text loadingLabel;
        private TMP_Text percentageLabel;
        private string baseLabel = "로딩중";
        private readonly StringBuilder labelBuilder = new StringBuilder(32);
        private Coroutine loadRoutine;

        public bool IsLoading => loadRoutine != null;

        /// <summary>씬에 배치된 LoadingBar 오브젝트를 연결한다.</summary>
        public bool Bind(GameObject loadingBarRoot)
        {
            root = loadingBarRoot;
            if (root == null) return false;

            fill = FindComponent<Image>(root.transform, "Fill");
            loadingLabel = FindComponent<TMP_Text>(root.transform, "Loading");
            percentageLabel = FindComponent<TMP_Text>(root.transform, "Percentage");

            // 씬에는 "로딩중..." 처럼 점까지 적혀 있습니다. 점은 코드가 붙이므로 떼고 씁니다.
            // 표에 키가 있으면 그 글자를, 없으면 씬에 적힌 글자를 그대로 씁니다.
            var sceneLabel = loadingLabel != null && !string.IsNullOrWhiteSpace(loadingLabel.text)
                ? loadingLabel.text.TrimEnd('.', '·', '…', ' ')
                : baseLabel;
            baseLabel = Localization.GetOr("title.loading", sceneLabel);

            SetProgress(0f);
            return fill != null;
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void ShowProgress(float value)
        {
            if (root != null) root.SetActive(true);
            SetProgress(Mathf.Clamp01(value));
        }

        /// <summary>로딩바를 켜고 대상 씬을 비동기로 로드한다.</summary>
        public void Load(string sceneName)
        {
            if (loadRoutine != null) return;
            loadRoutine = StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            SetProgress(0f);
            if (root != null) root.SetActive(true);

            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                Debug.LogError($"'{sceneName}' 씬을 로드할 수 없습니다. Build Settings를 확인하세요.", this);
                loadRoutine = null;
                yield break;
            }

            operation.allowSceneActivation = false;

            var displayed = 0f;
            var animationTime = 0f;
            while (displayed < 1f)
            {
                var delta = Time.unscaledDeltaTime;
                animationTime += delta;

                // allowSceneActivation이 false면 progress는 0.9에서 멈춘다.
                var target = Mathf.Clamp01(operation.progress / 0.9f);
                displayed = Mathf.MoveTowards(displayed, target, MaxFillPerSecond * delta);

                SetProgress(displayed);
                AnimateLabel(animationTime);
                yield return null;
            }

            SetProgress(1f);
            AnimateLabel(animationTime);
            AudioManager.Play(SfxId.LoadingComplete);

            yield return new WaitForSecondsRealtime(CompleteHoldSeconds);

            // 씬을 활성화하기 직전에 화면을 덮는다. 새 씬은 SceneTransition이 다시 밝힌다(페이드 인).
            yield return SceneTransition.Instance.CoverRoutine();
            operation.allowSceneActivation = true;
        }

        private void SetProgress(float value)
        {
            if (fill != null) fill.fillAmount = value;
            if (percentageLabel != null) percentageLabel.text = (value * 100f).ToString("F2") + "%";
        }

        /// <summary>점 개수 순환 + 글자별 사인 웨이브.</summary>
        private void AnimateLabel(float time)
        {
            if (loadingLabel == null) return;

            var visibleDots = Mathf.FloorToInt(time / DotInterval) % (MaxDots + 1);

            labelBuilder.Clear();
            labelBuilder.Append(baseLabel);
            labelBuilder.Append('.', visibleDots);
            if (visibleDots < MaxDots)
            {
                // 남은 점은 투명 처리해서 글자 폭이 흔들리지 않게 한다.
                labelBuilder.Append("<alpha=#00>");
                labelBuilder.Append('.', MaxDots - visibleDots);
            }

            loadingLabel.text = labelBuilder.ToString();
            loadingLabel.ForceMeshUpdate();

            var textInfo = loadingLabel.textInfo;
            if (textInfo == null || textInfo.characterCount == 0) return;

            for (var i = 0; i < textInfo.characterCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                var offset = new Vector3(
                    0f,
                    Mathf.Sin(time * WaveSpeed - i * WavePerCharOffset) * WaveAmplitude,
                    0f);

                var vertices = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
                var index = charInfo.vertexIndex;
                vertices[index + 0] += offset;
                vertices[index + 1] += offset;
                vertices[index + 2] += offset;
                vertices[index + 3] += offset;
            }

            loadingLabel.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private static T FindComponent<T>(Transform root, string objectName) where T : Component
        {
            if (root.name == objectName && root.TryGetComponent(out T component)) return component;
            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindComponent<T>(root.GetChild(i), objectName);
                if (match != null) return match;
            }

            return null;
        }
    }
}
