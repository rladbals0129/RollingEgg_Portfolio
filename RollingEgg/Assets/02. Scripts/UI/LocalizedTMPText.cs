using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RollingEgg.Core;
using TMPro;
using UnityEngine;

namespace RollingEgg.UI
{
    /// <summary>
    /// Unity Localization 테이블/키를 사용해서 TMP_Text 내용을 갱신하는 컴포넌트
    /// - 런타임 전용: ILocalizationService를 통해 비동기 문자열 조회 후 텍스트 적용
    /// - 로캘 변경 시 자동으로 다시 로드
    /// </summary>
    public sealed class LocalizedTMPText : MonoBehaviour
    {
        [Header("Localization")]
        [SerializeField] private string tableName;
        [SerializeField] private string entryKey;

        [Header("동작 옵션")]
        [Tooltip("로캘 변경 시 자동으로 텍스트를 갱신할지 여부")]
        [SerializeField] private bool autoUpdateOnLocaleChanged = true;

        [Tooltip("문자열 조회 실패 시 키 문자열을 그대로 표시할지 여부")]
        [SerializeField] private bool fallbackToKeyOnError = true;

        private TMP_Text _text;
        private ILocalizationService _localizationService;
        private CancellationTokenSource _cts;

        public string TableName
        {
            get => tableName;
            set
            {
                if (tableName == value) return;
                tableName = value;
                RefreshAsync().Forget();
            }
        }

        public string EntryKey
        {
            get => entryKey;
            set
            {
                if (entryKey == value) return;
                entryKey = value;
                RefreshAsync().Forget();
            }
        }

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!ServiceLocator.HasService<ILocalizationService>())
            {
                Debug.LogWarning("[LocalizedTMPText] ILocalizationService가 등록되어 있지 않습니다.");
                return;
            }

            _localizationService = ServiceLocator.Get<ILocalizationService>();
            if (_localizationService == null)
            {
                Debug.LogWarning("[LocalizedTMPText] ILocalizationService 인스턴스를 가져오지 못했습니다.");
                return;
            }

            if (autoUpdateOnLocaleChanged)
            {
                _localizationService.OnLocaleChanged += HandleLocaleChanged;
            }

            RefreshAsync().Forget();
        }

        private void OnDisable()
        {
            if (Application.isPlaying && _localizationService != null && autoUpdateOnLocaleChanged)
            {
                _localizationService.OnLocaleChanged -= HandleLocaleChanged;
            }

            CancelRefresh();
        }

        private void OnDestroy()
        {
            CancelRefresh();
        }

        private void HandleLocaleChanged()
        {
            RefreshAsync().Forget();
        }

        /// <summary>
        /// 현재 설정된 table/key에 따라 텍스트를 갱신한다.
        /// </summary>
        public UniTask RefreshAsync()
        {
            if (!Application.isPlaying)
                return UniTask.CompletedTask;

            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }

            if (_localizationService == null || string.IsNullOrEmpty(entryKey))
            {
                if (fallbackToKeyOnError && _text != null)
                {
                    _text.text = entryKey ?? string.Empty;
                }
                return UniTask.CompletedTask;
            }

            CancelRefresh();
            _cts = new CancellationTokenSource();
            return RefreshInternalAsync(_cts.Token);
        }

        private async UniTask RefreshInternalAsync(CancellationToken ct)
        {
            try
            {
                string result = await _localizationService.GetAsync(tableName, entryKey);
                if (ct.IsCancellationRequested)
                    return;

                if (_text == null)
                    _text = GetComponent<TMP_Text>();

                if (_text != null)
                {
                    _text.text = string.IsNullOrEmpty(result) && fallbackToKeyOnError
                        ? (entryKey ?? string.Empty)
                        : result;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalizedTMPText] RefreshInternalAsync error: {e.Message}");
                if (_text != null && fallbackToKeyOnError)
                {
                    _text.text = entryKey ?? string.Empty;
                }
            }
        }

        private void CancelRefresh()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}


