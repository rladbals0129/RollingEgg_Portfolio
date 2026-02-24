using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RollingEgg.UI
{
    /// <summary>
    /// UI 전환 시 페이드 인/아웃 연출을 담당하는 컨트롤러.
    /// CanvasGroup 알파를 직접 제어하므로 어디서든 호출 가능하다.
    /// </summary>
    public class UIFadeController : MonoBehaviour
    {
        [Header("## References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _fadeImage;

        [Header("## Settings")]
        [SerializeField, Min(0f)] private float _defaultDuration = 0.35f;
        [SerializeField] private Ease _fadeInEase = Ease.OutQuad;
        [SerializeField] private Ease _fadeOutEase = Ease.InQuad;
        [SerializeField] private Color _defaultColor = Color.black;

        private CancellationTokenSource _internalCts;

        private void Awake()
        {
            CacheReferences();
            InitializeState();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheReferences();
            if (!Application.isPlaying)
                InitializeState();
        }
#endif

        /// <summary>
        /// 화면을 어둡게 덮는 페이드 아웃.
        /// </summary>
        public async UniTask FadeOutAsync(float? duration = null, Color? overrideColor = null, CancellationToken token = default)
        {
            if (!EnsureCanvasGroup())
                return;

            var fadeCts = RegisterNewFadeCts(token);

            try
            {
                if (_fadeImage != null)
                    _fadeImage.color = overrideColor ?? _defaultColor;

                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;

                await FadeAsync(1f, duration ?? _defaultDuration, _fadeOutEase, fadeCts.Token);
            }
            finally
            {
                ReleaseFadeCts(fadeCts);
            }
        }

        /// <summary>
        /// 화면을 밝히는 페이드 인.
        /// </summary>
        public async UniTask FadeInAsync(float? duration = null, CancellationToken token = default)
        {
            if (!EnsureCanvasGroup())
                return;

            var fadeCts = RegisterNewFadeCts(token);

            try
            {
                await FadeAsync(0f, duration ?? _defaultDuration, _fadeInEase, fadeCts.Token);

                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
            finally
            {
                ReleaseFadeCts(fadeCts);
            }
        }

        private async UniTask FadeAsync(float targetAlpha, float duration, Ease ease, CancellationToken token)
        {
            if (!EnsureCanvasGroup())
                return;

            duration = Mathf.Max(duration, 0f);
            if (duration <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
                return;
            }

            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = ease == Ease.Unset ? t : DOVirtual.EasedValue(0f, 1f, t, ease);
                _canvasGroup.alpha = Mathf.LerpUnclamped(startAlpha, targetAlpha, easedT);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            _canvasGroup.alpha = targetAlpha;
        }

        private void CacheReferences()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_fadeImage == null)
                _fadeImage = GetComponent<Image>();
        }

        private void InitializeState()
        {
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            if (_fadeImage != null)
                _fadeImage.color = _defaultColor;
        }

        private bool EnsureCanvasGroup()
        {
            if (_canvasGroup != null)
                return true;

            Debug.LogError("[UIFadeController] CanvasGroup 레퍼런스가 비어있습니다.");
            return false;
        }

        private CancellationTokenSource RegisterNewFadeCts(CancellationToken externalToken)
        {
            CancelActiveFadeCts();

            _internalCts = externalToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(externalToken)
                : new CancellationTokenSource();

            return _internalCts;
        }

        private void ReleaseFadeCts(CancellationTokenSource target)
        {
            if (_internalCts != target)
                return;

            _internalCts.Dispose();
            _internalCts = null;
        }

        private void CancelActiveFadeCts()
        {
            if (_internalCts == null)
                return;

            try
            {
                if (!_internalCts.IsCancellationRequested)
                    _internalCts.Cancel();
            }
            finally
            {
                _internalCts.Dispose();
                _internalCts = null;
            }
        }

        private void OnDestroy()
        {
            CancelActiveFadeCts();
        }
    }
}

